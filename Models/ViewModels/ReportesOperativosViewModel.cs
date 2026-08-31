namespace SmileTrack_MVC.Models.ViewModels
{
    public class ReportesOperativosViewModel
    {
        // Topbar
        public string ReceptionistName { get; set; } = "Recepcionista";
        public string ReceptionistAvatarUrl { get; set; } = string.Empty;

        // KPIs (bento grid)
        public List<KpiOperativoViewModel> Kpis { get; set; } = new();

        // Agenda del día
        public List<AgendaItemViewModel> Agenda { get; set; } = new();

        // Pacientes recientes
        public List<PacienteRecienteViewModel> PacientesRecientes { get; set; } = new();

        // Registro de cobros
        public List<CobroItemViewModel> Cobros { get; set; } = new();

        // Saldos pendientes (morosos)
        public List<SaldoPendienteViewModel> SaldosPendientes { get; set; } = new();

        // Cierre parcial de caja
        public decimal CierreParcialCaja { get; set; }

        public List<OpcionSelectViewModel> PacientesDisponibles { get; set; } = new();
        public List<OpcionSelectViewModel> ProfesionalesDisponibles { get; set; } = new();
        public List<OpcionSelectViewModel> ServiciosDisponibles { get; set; } = new();
    }

    public class OpcionSelectViewModel
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
    }

    public class KpiOperativoViewModel
    {
        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        // "primary" | "secondary" | "tertiary" | "error" -> controla el color del ícono y del valor
        public string ColorVariant { get; set; } = "primary";
    }

    public class AgendaItemViewModel
    {
        public int IdCita { get; set; }
        public int IdPaciente { get; set; }
        public int? IdProfesional { get; set; }
        public int? IdServicio { get; set; }
        public string Time { get; set; } = string.Empty;
        public string Professional { get; set; } = string.Empty;
        public string Service { get; set; } = string.Empty;
        // "Atendida" | "Confirmada" | "Pendiente" | "Cancelada"
        public string Status { get; set; } = string.Empty;
        // "secondary" (atendida) | "primary" (confirmada) | "neutral" (pendiente) | "error" (cancelada)
        public string StatusVariant { get; set; } = "neutral";
        public bool IsCancelled { get; set; }
    }

    public class PacienteRecienteViewModel
    {
        public int IdPaciente { get; set; }
        public string Initials { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        // Puede ser teléfono o email
        public string ContactInfo { get; set; } = string.Empty;
        // "primary" | "secondary"
        public string AvatarColorVariant { get; set; } = "primary";
    }

    public class CobroItemViewModel
    {
        // "Tarjeta" | "Efectivo"
        public string Method { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        // "secondary" | "primary"
        public string IconColorVariant { get; set; } = "primary";
        public string PatientName { get; set; } = string.Empty;
        public string Folio { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Time { get; set; } = string.Empty;
    }

    public class SaldoPendienteViewModel
    {
        public string PatientName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }
}
