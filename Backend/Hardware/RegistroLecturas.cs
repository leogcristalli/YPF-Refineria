using System.Collections.Concurrent;

namespace Backend.Hardware
{
    // Guarda la última lectura que entregó cada báscula para cada planificación.
    // Permite que el backend verifique que una pesada "automática" tenga exactamente
    // el peso que leyó la báscula; si el peso se tipeó o se editó, debe registrarse como manual.
    public class RegistroLecturas
    {
        // Una lectura vieja no sirve: el camión pudo haberse movido de la báscula.
        public static readonly TimeSpan Vigencia = TimeSpan.FromMinutes(10);

        private readonly ConcurrentDictionary<(int IdBascula, long IdPlanificacion), LecturaPeso> _lecturas = new();

        public void Guardar(int idBascula, long idPlanificacion, LecturaPeso lectura) =>
            _lecturas[(idBascula, idPlanificacion)] = lectura;

        // Devuelve la lectura si existe y está vigente.
        public LecturaPeso? Obtener(int idBascula, long idPlanificacion)
        {
            if (!_lecturas.TryGetValue((idBascula, idPlanificacion), out var lectura)) return null;
            return DateTime.Now - lectura.Fecha <= Vigencia ? lectura : null;
        }

        // Una lectura se usa para una sola pesada.
        public void Consumir(int idBascula, long idPlanificacion) =>
            _lecturas.TryRemove((idBascula, idPlanificacion), out _);
    }
}
