namespace SmileTrack_MVC.Models.ViewModels
{
    public class ReportesViewModel
    {
        public string NombreUsuario { get; set; } = "Administrador";
        public string Rol { get; set; } = "Administrador";
        public string FotoPerfilUrl { get; set; } = "/images/default-avatar.png";
        public DateTime UltimaActualizacion { get; set; } = DateTime.Now;

        // KPIs
        public decimal IngresosTotales { get; set; }
        public double VariacionIngresos { get; set; }
        public int NuevosPacientes { get; set; }
        public double VariacionPacientes { get; set; }
        public int CitasCompletadas { get; set; }
        public double VariacionCitas { get; set; }
        public double TasaRetencion { get; set; }
        public double VariacionRetencion { get; set; }
        public double TasaInasistencia { get; set; }
        public double VariacionInasistencia { get; set; }
        public double OcupacionAgenda { get; set; }
        public double VariacionOcupacion { get; set; }

        // Gráficos y Tablas
        public List<CrecimientoItemViewModel> CrecimientoClinica { get; set; } = new();
        public List<DistribucionServicioViewModel> DistribucionServicios { get; set; } = new();
        public List<IngresoProfesionalViewModel> IngresosPorProfesional { get; set; } = new();
        public List<EstadoCitaViewModel> EstadoCitas { get; set; } = new();
        public List<ProcedimientoReporteViewModel> Procedimientos { get; set; } = new();

        public string FiltroCategoria { get; set; } = "todos";
        public List<ReporteRecienteItemViewModel> ReportesRecientes { get; set; } = new();
    }

    public class CrecimientoItemViewModel
    {
        public string Mes { get; set; } = string.Empty;
        public double ValorActual { get; set; }
        public double ValorAnterior { get; set; }
    }

    public class DistribucionServicioViewModel
    {
        public string Nombre { get; set; } = string.Empty;
        public double Porcentaje { get; set; }
        public string Color { get; set; } = "#6366f1";
    }

    public class IngresoProfesionalViewModel
    {
        public string Nombre { get; set; } = string.Empty;
        public decimal Monto { get; set; }
        public double Porcentaje { get; set; }
    }

    public class EstadoCitaViewModel
    {
        public string Estado { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public double Porcentaje { get; set; }
    }

    public class ProcedimientoReporteViewModel
    {
        public string Nombre { get; set; } = string.Empty;
        public string Icono { get; set; } = "medical_services";
        public int Cantidad { get; set; }
        public decimal IngresosGenerados { get; set; }
        public decimal PrecioPromedio { get; set; }
        public double Tendencia { get; set; }
    }

    public class ReporteRecienteItemViewModel
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Categoria { get; set; } = "Financiero";
        public string Icono { get; set; } = "description";
        public DateTime FechaGenerado { get; set; } = DateTime.Now;
        public string Estado { get; set; } = "Listo";
    }
}
