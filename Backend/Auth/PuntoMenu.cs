namespace Backend.Auth
{
    // Ids de t_CasoDeUso: un punto de menú por pantalla (script_initdata_t_casodeuso.sql).
    // El frontend usa los mismos valores en src/auth/puntosMenu.js.
    public static class PuntoMenu
    {
        public const int Pesadas = 1;
        public const int OrdenesTransporte = 2;
        public const int AutorizacionesDescarga = 3;
        public const int OrdenesEntrega = 4;
        public const int Reportes = 5;
        public const int Vehiculos = 6;
        public const int Conductores = 7;
        public const int Productos = 8;
        public const int RazonesSociales = 9;
        public const int Basculas = 10;
        public const int Tanques = 11;
        public const int Puestos = 12;
        public const int Dispositivos = 13;
        public const int PuntosMenu = 14;
        public const int Roles = 15;
        public const int Usuarios = 16;
    }

    // Ids de t_Rol usados en código (script_initdata_t_rol.sql).
    public static class RolBase
    {
        public const int AdministradorSistema = 1;
    }
}
