using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Backend.Auth
{
    // Contraseña de un solo uso basada en el tiempo (TOTP, RFC 6238): SHA-1, 6 dígitos, paso de 30 s.
    // Es el formato que entienden Google Authenticator, Microsoft Authenticator, Authy y similares.
    public static class Totp
    {
        public const int Digitos = 6;
        public const int PasoSegundos = 30;
        public const int VentanaPasos = 1;   // tolera hasta 30 s de diferencia entre el reloj del teléfono y el del servidor

        private const string AlfabetoBase32 = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

        // Secreto de 160 bits en Base32 (el formato que leen las aplicaciones autenticadoras).
        public static string GenerarSecreto() => Base32Codificar(RandomNumberGenerator.GetBytes(20));

        public static long PasoActual(DateTimeOffset ahora) => ahora.ToUnixTimeSeconds() / PasoSegundos;

        public static string CalcularCodigo(byte[] secreto, long paso)
        {
            Span<byte> contador = stackalloc byte[8];
            BinaryPrimitives.WriteInt64BigEndian(contador, paso);
            byte[] hash = HMACSHA1.HashData(secreto, contador);

            int desplazamiento = hash[^1] & 0x0F;
            int binario = ((hash[desplazamiento] & 0x7F) << 24)
                        | (hash[desplazamiento + 1] << 16)
                        | (hash[desplazamiento + 2] << 8)
                        | hash[desplazamiento + 3];

            return (binario % 1_000_000).ToString("D6");
        }

        // true si el código es correcto y su paso de tiempo es posterior al último aceptado
        // (un mismo código no se puede usar dos veces). pasoUsado es el paso que coincidió.
        public static bool Verificar(string secretoBase32, string? codigo, long? ultimoPaso, DateTimeOffset ahora, out long pasoUsado)
        {
            pasoUsado = 0;
            var ingresado = new string((codigo ?? "").Where(char.IsDigit).ToArray());   // admite "123 456"
            if (ingresado.Length != Digitos) return false;

            byte[] secreto = Base32Decodificar(secretoBase32);
            long actual = PasoActual(ahora);
            byte[] ingresadoBytes = Encoding.ASCII.GetBytes(ingresado);

            for (long paso = actual - VentanaPasos; paso <= actual + VentanaPasos; paso++)
            {
                if (ultimoPaso.HasValue && paso <= ultimoPaso.Value) continue;

                byte[] esperado = Encoding.ASCII.GetBytes(CalcularCodigo(secreto, paso));
                if (CryptographicOperations.FixedTimeEquals(esperado, ingresadoBytes))
                {
                    pasoUsado = paso;
                    return true;
                }
            }
            return false;
        }

        // URI que se muestra como código QR para que la aplicación autenticadora agregue la cuenta.
        public static string UriOtpAuth(string emisor, string cuenta, string secretoBase32)
        {
            string e = Uri.EscapeDataString(emisor);
            string c = Uri.EscapeDataString(cuenta);
            return $"otpauth://totp/{e}:{c}?secret={secretoBase32}&issuer={e}&algorithm=SHA1&digits={Digitos}&period={PasoSegundos}";
        }

        public static string Base32Codificar(ReadOnlySpan<byte> datos)
        {
            var sb = new StringBuilder((datos.Length * 8 + 4) / 5);
            int buffer = 0, bits = 0;
            foreach (byte b in datos)
            {
                buffer = (buffer << 8) | b;
                bits += 8;
                while (bits >= 5)
                {
                    sb.Append(AlfabetoBase32[(buffer >> (bits - 5)) & 31]);
                    bits -= 5;
                }
                buffer &= (1 << bits) - 1;
            }
            if (bits > 0) sb.Append(AlfabetoBase32[(buffer << (5 - bits)) & 31]);
            return sb.ToString();
        }

        public static byte[] Base32Decodificar(string texto)
        {
            var salida = new List<byte>();
            int buffer = 0, bits = 0;
            foreach (char c in texto.Trim().TrimEnd('=').ToUpperInvariant())
            {
                if (c == ' ' || c == '-') continue;
                int valor = AlfabetoBase32.IndexOf(c);
                if (valor < 0) throw new FormatException("El secreto TOTP no es un Base32 válido.");
                buffer = (buffer << 5) | valor;
                bits += 5;
                if (bits >= 8)
                {
                    salida.Add((byte)((buffer >> (bits - 8)) & 0xFF));
                    bits -= 8;
                    buffer &= (1 << bits) - 1;
                }
            }
            return salida.ToArray();
        }
    }

    // El secreto TOTP no se guarda en texto plano: se cifra con AES-GCM usando una clave derivada
    // de Jwt:Key. Si esa clave cambia, los secretos guardados dejan de poder leerse y cada usuario
    // debe volver a configurar su autenticador (el administrador puede restablecerlo desde Usuarios).
    public class ProtectorTotp
    {
        private const int LargoNonce = 12;
        private const int LargoTag = 16;
        private readonly byte[] _clave;

        public ProtectorTotp(IConfiguration config)
        {
            string jwt = config["Jwt:Key"] ?? throw new InvalidOperationException("Falta Jwt:Key.");
            _clave = SHA256.HashData(Encoding.UTF8.GetBytes("cilp-totp|" + jwt));
        }

        public string Proteger(string texto)
        {
            byte[] nonce = RandomNumberGenerator.GetBytes(LargoNonce);
            byte[] plano = Encoding.UTF8.GetBytes(texto);
            byte[] cifrado = new byte[plano.Length];
            byte[] tag = new byte[LargoTag];

            using var aes = new AesGcm(_clave, LargoTag);
            aes.Encrypt(nonce, plano, cifrado, tag);

            return Convert.ToBase64String(nonce.Concat(tag).Concat(cifrado).ToArray());
        }

        // null si el valor no existe, está dañado o fue cifrado con otra clave.
        public string? Revelar(string? protegido)
        {
            if (string.IsNullOrEmpty(protegido)) return null;
            try
            {
                byte[] datos = Convert.FromBase64String(protegido);
                if (datos.Length < LargoNonce + LargoTag) return null;

                byte[] nonce = datos[..LargoNonce];
                byte[] tag = datos[LargoNonce..(LargoNonce + LargoTag)];
                byte[] cifrado = datos[(LargoNonce + LargoTag)..];
                byte[] plano = new byte[cifrado.Length];

                using var aes = new AesGcm(_clave, LargoTag);
                aes.Decrypt(nonce, cifrado, tag, plano);
                return Encoding.UTF8.GetString(plano);
            }
            catch (Exception ex) when (ex is CryptographicException or FormatException)
            {
                return null;
            }
        }
    }
}