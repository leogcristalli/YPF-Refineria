using Backend.Models;

namespace Backend.Data
{
    // Criterio único de "orden pendiente de pesaje", compartido por la búsqueda por patente (CU077),
    // la detección con cámara y el modal de Registrar Pesada (CU078).
    public static class ConsultasPesaje
    {
        // Planificaciones cuya OT y orden siguen abiertas (no cerradas ni canceladas)
        // y que todavía no tienen sus dos pesadas (TARA y BRUTO).
        public static IQueryable<Planificacion> PendientesDePesaje(this AppDbContext context) =>
            context.Planificaciones.Where(p =>
                p.Activo
                && p.OrdenTransporte!.Activo
                && p.OrdenTransporte.OrdenDetalle!.Orden!.Activo
                && p.OrdenTransporte.OrdenDetalle.Orden.FechaCierre == null
                && context.Pesadas.Count(x => x.Id_Planificacion == p.Id_Planificacion) < 2);
    }
}
