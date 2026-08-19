using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.ViewModels;

namespace SmileTrack_MVC.Controllers;

[Authorize]
public class ReportesController : Controller
{
    private readonly AppDbContext _context;

    public ReportesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [Route("reportes/vista-admin")]
    [Route("reportes")]
    public async Task<IActionResult> VistaAdmin(string? categoria)
    {
        string userName = User.Identity?.Name ?? "Administrador";
        string userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "Administrador";

        int totalPacientes = await _context.Pacientes.CountAsync();
        int totalCitas = await _context.Citas.CountAsync();
        int citasCompletadas = await _context.Citas.CountAsync(c => c.Estado == "Atendida" || c.Estado == "Completada");
        decimal totalIngresos = await _context.Facturas.Where(f => f.Estado == "pagada").SumAsync(f => (decimal?)f.Total) ?? 0m;

        // Distribución de servicios de BD
        var serviciosDb = await _context.Servicios.ToListAsync();
        int totalServicios = serviciosDb.Count;
        var distribucion = serviciosDb.Select((s, idx) => new DistribucionServicioViewModel
        {
            Nombre = s.Nombre,
            Porcentaje = totalServicios > 0 ? Math.Round(100.0 / totalServicios, 1) : 0,
            Color = idx % 5 == 0 ? "#6366f1" : idx % 5 == 1 ? "#10b981" : idx % 5 == 2 ? "#f59e0b" : idx % 5 == 3 ? "#8b5cf6" : "#ec4899"
        }).ToList();

        if (!distribucion.Any())
        {
            distribucion = new List<DistribucionServicioViewModel>
            {
                new() { Nombre = "Limpieza dental", Porcentaje = 35, Color = "#6366f1" },
                new() { Nombre = "Ortodoncia", Porcentaje = 25, Color = "#10b981" },
                new() { Nombre = "Endodoncia", Porcentaje = 20, Color = "#f59e0b" },
                new() { Nombre = "Blanqueamiento", Porcentaje = 12, Color = "#8b5cf6" },
                new() { Nombre = "Otros", Porcentaje = 8, Color = "#ec4899" }
            };
        }

        // Ingresos por profesional
        var profesionales = await _context.Profesionales.ToListAsync();
        var ingresosProf = profesionales.Select(p => new IngresoProfesionalViewModel
        {
            Nombre = $"{p.Nombres} {p.Apellidos}",
            Monto = totalIngresos > 0 ? Math.Round(totalIngresos / Math.Max(1, profesionales.Count), 2) : 15000000m,
            Porcentaje = profesionales.Count > 0 ? Math.Round(100.0 / profesionales.Count, 1) : 33.3
        }).ToList();

        var model = new ReportesViewModel
        {
            NombreUsuario = userName,
            Rol = userRole,
            FotoPerfilUrl = "/images/Imagenes/Logos/logo.jpg",
            UltimaActualizacion = DateTime.Now,

            IngresosTotales = totalIngresos > 0 ? totalIngresos : 45200000m,
            VariacionIngresos = 12.5,
            NuevosPacientes = totalPacientes > 0 ? totalPacientes : 48,
            VariacionPacientes = 8.2,
            CitasCompletadas = citasCompletadas > 0 ? citasCompletadas : (totalCitas > 0 ? totalCitas : 342),
            VariacionCitas = 5.4,
            TasaRetencion = 89.0,
            VariacionRetencion = 2.1,
            TasaInasistencia = 4.5,
            VariacionInasistencia = -1.2,
            OcupacionAgenda = 92.0,
            VariacionOcupacion = 3.8,

            FiltroCategoria = string.IsNullOrWhiteSpace(categoria) ? "todos" : categoria.ToLower(),

            CrecimientoClinica = new List<CrecimientoItemViewModel>
            {
                new() { Mes = "Ene", ValorActual = 65, ValorAnterior = 50 },
                new() { Mes = "Feb", ValorActual = 72, ValorAnterior = 55 },
                new() { Mes = "Mar", ValorActual = 80, ValorAnterior = 60 },
                new() { Mes = "Abr", ValorActual = 78, ValorAnterior = 62 },
                new() { Mes = "May", ValorActual = 88, ValorAnterior = 70 },
                new() { Mes = "Jun", ValorActual = 95, ValorAnterior = 75 },
                new() { Mes = "Jul", ValorActual = 92, ValorAnterior = 80 }
            },

            DistribucionServicios = distribucion,
            IngresosPorProfesional = ingresosProf,

            EstadoCitas = new List<EstadoCitaViewModel>
            {
                new() { Estado = "Completadas", Cantidad = citasCompletadas, Porcentaje = totalCitas > 0 ? Math.Round(citasCompletadas * 100.0 / totalCitas, 1) : 75.6 },
                new() { Estado = "Programadas", Cantidad = await _context.Citas.CountAsync(c => c.Estado == "Agendada" || c.Estado == "Programada"), Porcentaje = 15.8 },
                new() { Estado = "Canceladas", Cantidad = await _context.Citas.CountAsync(c => c.Estado == "Cancelada"), Porcentaje = 4.9 },
                new() { Estado = "Inasistencias", Cantidad = await _context.Citas.CountAsync(c => c.Estado == "No asistio"), Porcentaje = 3.7 }
            },

            Procedimientos = serviciosDb.Select(s => new ProcedimientoReporteViewModel
            {
                Nombre = s.Nombre,
                Icono = "dentistry",
                Cantidad = 25,
                IngresosGenerados = s.Precio * 25,
                PrecioPromedio = s.Precio,
                Tendencia = 10.0
            }).ToList(),

            ReportesRecientes = new List<ReporteRecienteItemViewModel>
            {
                new() { Id = 1, Nombre = "Consolidado Mensual de Ingresos", Categoria = "Financiero", Icono = "payments", FechaGenerado = DateTime.Now.AddDays(-1), Estado = "Listo" },
                new() { Id = 2, Nombre = "Estadísticas de Retención y Pacientes", Categoria = "Pacientes", Icono = "group", FechaGenerado = DateTime.Now.AddDays(-3), Estado = "Listo" },
                new() { Id = 3, Nombre = "Eficiencia de Consultorios", Categoria = "Operativo", Icono = "meeting_room", FechaGenerado = DateTime.Now.AddDays(-5), Estado = "Listo" },
                new() { Id = 4, Nombre = "Inventario de Insumos Dentales", Categoria = "Logística", Icono = "inventory_2", FechaGenerado = DateTime.Now.AddDays(-7), Estado = "Listo" }
            }
        };

        if (!string.IsNullOrWhiteSpace(categoria) && !categoria.Equals("todos", StringComparison.OrdinalIgnoreCase))
        {
            model.ReportesRecientes = model.ReportesRecientes
                .Where(r => r.Categoria.Equals(categoria, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return View("~/Views/Reportes/vista_admin/index.cshtml", model);
    }

    [HttpGet]
    [Route("reportes/vista-prof")]
    public async Task<IActionResult> VistaProf()
    {
        string userName = User.Identity?.Name ?? "Profesional";
        int totalPacientes = await _context.Pacientes.CountAsync();
        int totalCitas = await _context.Citas.CountAsync();
        decimal totalIngresos = await _context.Facturas.Where(f => f.Estado == "pagada").SumAsync(f => (decimal?)f.Total) ?? 0m;

        var citasRecientes = await _context.Citas
            .Include(c => c.Paciente)
            .Include(c => c.Servicio)
            .OrderByDescending(c => c.FechaHora)
            .Take(5)
            .Select(c => new CitaRecienteViewModel
            {
                NombrePaciente = c.Paciente != null ? $"{c.Paciente.Nombres} {c.Paciente.Apellidos}" : "Paciente",
                IdPaciente = c.IdPaciente,
                Servicio = c.Servicio != null ? c.Servicio.Nombre : "Consulta general",
                Hora = c.FechaHora.ToString("hh:mm tt"),
                Estado = c.Estado,
                EstadoClase = c.Estado == "Atendida" ? "status--completed" : c.Estado == "Cancelada" ? "status--cancelled" : "status--pending"
            })
            .ToListAsync();

        var model = new ReportesProfesionalViewModel
        {
            DoctorName = userName,
            DoctorRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "Odontólogo(a)",

            TotalCitas = totalCitas > 0 ? totalCitas : 128,
            TendenciaCitas = 8.4,
            Ingresos = totalIngresos > 0 ? totalIngresos : 18500000m,
            TendenciaIngresos = 12.1,
            PacientesUnicos = totalPacientes > 0 ? totalPacientes : 64,
            Cancelaciones = await _context.Citas.CountAsync(c => c.Estado == "Cancelada"),
            TasaCancelacion = 4.2,

            DuracionPromedio = "30 min",
            TasaPuntualidad = 96,
            SatisfaccionPaciente = "4.9/5",
            ResenasTotales = 57,
            CitasHoy = await _context.Citas.CountAsync(c => c.FechaHora.Date == DateTime.Today),
            CitasManana = await _context.Citas.CountAsync(c => c.FechaHora.Date == DateTime.Today.AddDays(1)),

            Tratamientos = new List<TratamientoProfViewModel>
            {
                new() { Nombre = "Ortodoncia", Cantidad = 42, Porcentaje = 33 },
                new() { Nombre = "Limpieza dental", Cantidad = 35, Porcentaje = 27 },
                new() { Nombre = "Endodoncia", Cantidad = 18, Porcentaje = 14 },
                new() { Nombre = "Blanqueamiento", Cantidad = 12, Porcentaje = 9 }
            },

            PacientesFrecuentes = new List<PacienteFrecuenteViewModel>
            {
                new() { Iniciales = "JR", Color = "blue",   Nombre = "Julián Restrepo", Tipo = "Fiel",       TipoClase = "patient-badge--loyal",     Citas = 14, Periodo = "últimos 6 meses" },
                new() { Iniciales = "LT", Color = "purple", Nombre = "Lucía Torres",    Tipo = "Recurrente", TipoClase = "patient-badge--recurrent", Citas = 9,  Periodo = "últimos 6 meses" }
            },

            ProximosDias = new List<ProximoDiaViewModel>
            {
                new() { DiaNombre = "Hoy",     Fecha = DateTime.Now.ToString("dd MMM"),            CantidadCitas = 6, EsHoy = true },
                new() { DiaNombre = "Mañana",  Fecha = DateTime.Now.AddDays(1).ToString("dd MMM"), CantidadCitas = 8 },
                new() { DiaNombre = "Siguiente", Fecha = DateTime.Now.AddDays(2).ToString("dd MMM"), CantidadCitas = 5 }
            },

            UltimasCitas = citasRecientes
        };

        return View("~/Views/Reportes/vista_prof/index.cshtml", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("reportes/descargar/{id}")]
    public IActionResult DescargarReporte(int id)
    {
        string contenidoDummy = $"SmileTrack Reporte #{id}\nGenerado el: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\nEstado: Exportado exitosamente.";
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(contenidoDummy);
        return File(bytes, "text/csv", $"Reporte_{id}_{DateTime.Now:yyyyMMdd}.csv");
    }

    [HttpGet]
    [Route("reportes/procedimientos")]
    public IActionResult TodosProcedimientos()
    {
        return RedirectToAction(nameof(VistaAdmin));
    }

    [HttpGet]
    [Route("reportes/lista")]
    public IActionResult TodosReportes()
    {
        return RedirectToAction(nameof(VistaAdmin));
    }
}
