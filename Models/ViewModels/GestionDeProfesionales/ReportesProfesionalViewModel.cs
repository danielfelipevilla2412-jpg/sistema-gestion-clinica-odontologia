namespace SmileTrack_MVC.Models.ViewModels
{
    public class ReportesProfesionalViewModel
    {
        public string DoctorName { get; set; } = "Profesional";
        public string DoctorRole { get; set; } = "Odontólogo(a)";

        // KPIs principales
        public int TotalCitas { get; set; }
        public double TendenciaCitas { get; set; }
        public decimal Ingresos { get; set; }
        public double TendenciaIngresos { get; set; }
        public int PacientesUnicos { get; set; }
        public int Cancelaciones { get; set; }
        public double TasaCancelacion { get; set; }

        // KPIs secundarios (rendimiento profesional)
        public string DuracionPromedio { get; set; } = "0 min";
        public double TasaPuntualidad { get; set; }
        public string SatisfaccionPaciente { get; set; } = "0/5";
        public int ResenasTotales { get; set; }
        public int CitasHoy { get; set; }
        public int CitasManana { get; set; }

        // Listas
        public List<TratamientoProfViewModel> Tratamientos { get; set; } = new();
        public List<PacienteFrecuenteViewModel> PacientesFrecuentes { get; set; } = new();
        public List<ProximoDiaViewModel> ProximosDias { get; set; } = new();
        public List<CitaRecienteViewModel> UltimasCitas { get; set; } = new();
    }

    public class TratamientoProfViewModel
    {
        public string Nombre { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public double Porcentaje { get; set; }
    }

    public class PacienteFrecuenteViewModel
    {
        public string Iniciales { get; set; } = string.Empty;
        public string Color { get; set; } = "blue";
        public string Nombre { get; set; } = string.Empty;
        public string Tipo { get; set; } = "Recurrente";
        public string TipoClase { get; set; } = "patient-badge--recurrent";
        public int Citas { get; set; }
        public string Periodo { get; set; } = string.Empty;
    }

    public class ProximoDiaViewModel
    {
        public string DiaNombre { get; set; } = string.Empty;
        public string Fecha { get; set; } = string.Empty;
        public int CantidadCitas { get; set; }
        public bool EsHoy { get; set; }
        public bool EsPico { get; set; }
    }

    public class CitaRecienteViewModel
    {
        public string NombrePaciente { get; set; } = string.Empty;
        public int IdPaciente { get; set; }
        public string Servicio { get; set; } = string.Empty;
        public string Hora { get; set; } = string.Empty;
        public string Estado { get; set; } = "Completada";
        public string EstadoClase { get; set; } = "status--completed";
    }
}
