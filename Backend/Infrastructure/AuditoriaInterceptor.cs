using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Backend.Infrastructure
{
    // Cada vez que EF abre una conexión ejecuta dbo.pr_context_save con el ID de red del usuario
    // logueado. Los DEFAULT y triggers de auditoría leen ese valor con dbo.fn_GetCurrentUser(),
    // así que Audit_Insert_User / Audit_Update_User registran al usuario de la aplicación.
    // Se ejecuta en cada apertura (no una sola vez) porque las conexiones vienen del pool y
    // podrían arrastrar el contexto de otro request. Sin usuario (login) se manda '' y la
    // función vuelve a SYSTEM_USER.
    public class AuditoriaInterceptor : DbConnectionInterceptor
    {
        private const int LargoMaximoUsuario = 50;   // @pUserName VARCHAR(50)

        private readonly IHttpContextAccessor _http;

        public AuditoriaInterceptor(IHttpContextAccessor http)
        {
            _http = http;
        }

        public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
        {
            using var comando = CrearComando(connection);
            comando.ExecuteNonQuery();
        }

        public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            await using var comando = CrearComando(connection);
            await comando.ExecuteNonQueryAsync(cancellationToken);
        }

        private DbCommand CrearComando(DbConnection connection)
        {
            var usuario = _http.HttpContext?.User.Identity?.IsAuthenticated == true
                ? _http.HttpContext.User.Identity.Name ?? ""
                : "";
            if (usuario.Length > LargoMaximoUsuario) usuario = usuario[..LargoMaximoUsuario];

            var comando = connection.CreateCommand();
            comando.CommandText = "dbo.pr_context_save";
            comando.CommandType = CommandType.StoredProcedure;
            var parametro = comando.CreateParameter();
            parametro.ParameterName = "@pUserName";
            parametro.DbType = DbType.AnsiString;
            parametro.Size = LargoMaximoUsuario;
            parametro.Value = usuario;
            comando.Parameters.Add(parametro);
            return comando;
        }
    }
}
