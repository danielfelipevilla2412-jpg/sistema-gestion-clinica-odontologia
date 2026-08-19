namespace SmileTrack_MVC.Models.ViewModels
{
    public class ReporteClinicoViewModel
    {
        public int Id { get; set; }
        public string NombrePaciente { get; set; } = string.Empty;
        public string Documento { get; set; } = string.Empty;
        public int Edad { get; set; }
        public DateTime UltimaConsulta { get; set; }
        public string Diagnostico { get; set; } = string.Empty;
        public string ProfesionalNombre { get; set; } = string.Empty;
        public DateTime? ProximaCita { get; set; }
        public string? Alerta { get; set; }
        public string Avatar { get; set; } = string.Empty;
        public string Color { get; set; } = "blue";
    }

    public class AgendaDiaViewModel
    {
        public DateTime Fecha { get; set; }
        public string NombreDia { get; set; } = string.Empty;
        public string NumeroDia { get; set; } = string.Empty;
        public bool EsHoy { get; set; }
        public bool Cerrado { get; set; }
        public List<AgendaCitaViewModel> Citas { get; set; } = [];
    }

    public class AgendaCitaViewModel
    {
        public int Id { get; set; }
        public int IdPaciente { get; set; }
        public int IdProfesional { get; set; }
        public int IdConsultorio { get; set; }
        public int IdServicio { get; set; }
        public DateTime Fecha { get; set; }
        public string Hora { get; set; } = string.Empty;
        public string HoraInicio { get; set; } = string.Empty;
        public string HoraFin { get; set; } = string.Empty;
        public string Paciente { get; set; } = string.Empty;
        public string Servicio { get; set; } = string.Empty;
        public string Consultorio { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public string ClaseEstado { get; set; } = "confirmed";
        public string Notas { get; set; } = string.Empty;
    }
}
