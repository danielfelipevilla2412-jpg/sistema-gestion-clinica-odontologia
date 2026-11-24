using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Helpers;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Models.ViewModels;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;
using System.Text;
using System.Text.Json;

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
        var userName = User.Identity?.Name ?? "Administrador";
        var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "Administrador";
        var periodoActual = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var periodoSiguiente = periodoActual.AddMonths(1);
        var periodoAnterior = periodoActual.AddMonths(-1);
        var estadisticas = await ObtenerEstadisticasPeriodoAsync(periodoActual, periodoSiguiente);
        var estadisticasAnteriores = await ObtenerEstadisticasPeriodoAsync(periodoAnterior, periodoActual);
        var serviciosDb = await _context.Servicios.AsNoTracking().ToListAsync();
        var colores = new[] { "#6366f1", "#10b981", "#f59e0b", "#8b5cf6", "#ec4899", "#0ea5e9" };

        var citasPorServicio = await _context.Citas.AsNoTracking()
            .Where(c => c.IdServicio != null &&
                        c.FechaHora >= periodoActual &&
                        c.FechaHora < periodoSiguiente &&
                        c.Estado != "Cancelada")
            .GroupBy(c => c.IdServicio)
            .Select(g => new { IdServicio = g.Key, Cantidad = g.Count() })
            .OrderByDescending(x => x.Cantidad)
            .ToListAsync();
        var citasPorServicioAnterior = await _context.Citas.AsNoTracking()
            .Where(c => c.IdServicio != null &&
                        c.FechaHora >= periodoAnterior &&
                        c.FechaHora < periodoActual &&
                        c.Estado != "Cancelada")
            .GroupBy(c => c.IdServicio)
            .Select(g => new { IdServicio = g.Key, Cantidad = g.Count() })
            .ToDictionaryAsync(x => x.IdServicio!.Value, x => x.Cantidad);

        var nombresServicios = serviciosDb.ToDictionary(s => s.IdServicio, s => s.Nombre);
        var totalCitasServicio = citasPorServicio.Sum(x => x.Cantidad);
        var distribucionBase = citasPorServicio
            .Where(x => x.IdServicio.HasValue && nombresServicios.ContainsKey(x.IdServicio.Value))
            .Take(5)
            .Select(x => new
            {
                Nombre = nombresServicios[x.IdServicio!.Value],
                x.Cantidad
            })
            .ToList();
        var otros = citasPorServicio
            .Skip(5)
            .Sum(x => x.Cantidad);
        var distribucion = distribucionBase
            .Select((x, index) => new DistribucionServicioViewModel
            {
                Nombre = x.Nombre,
                Cantidad = x.Cantidad,
                Porcentaje = totalCitasServicio > 0 ? Math.Round(x.Cantidad * 100.0 / totalCitasServicio, 1) : 0,
                Color = colores[index % colores.Length]
            })
            .ToList();
        if (otros > 0)
        {
            distribucion.Add(new DistribucionServicioViewModel
            {
                Nombre = "Otros",
                Cantidad = otros,
                Porcentaje = totalCitasServicio > 0 ? Math.Round(otros * 100.0 / totalCitasServicio, 1) : 0,
                Color = colores[5]
            });
        }

        var facturasPagadas = await _context.Facturas.AsNoTracking()
            .Where(f => f.Estado == "pagada" &&
                        (f.FechaPago ?? f.FechaFactura) >= periodoActual &&
                        (f.FechaPago ?? f.FechaFactura) < periodoSiguiente)
            .Select(f => new { f.IdProfesional, f.IdCita, f.Total })
            .ToListAsync();
        var idsCitasFactura = facturasPagadas
            .Where(f => f.IdCita.HasValue)
            .Select(f => f.IdCita!.Value)
            .Distinct()
            .ToList();
        var profesionalesPorCita = idsCitasFactura.Count == 0
            ? new Dictionary<int, int?>()
            : await _context.Citas.AsNoTracking()
                .Where(c => idsCitasFactura.Contains(c.IdCita))
                .Select(c => new { c.IdCita, c.IdProfesional })
                .ToDictionaryAsync(c => c.IdCita, c => c.IdProfesional);
        var ingresosPorProfDb = facturasPagadas
            .Select(f => new
            {
                IdProf = f.IdProfesional ??
                         (f.IdCita.HasValue && profesionalesPorCita.TryGetValue(f.IdCita.Value, out var idProf)
                             ? idProf
                             : null),
                f.Total
            })
            .Where(x => x.IdProf != null)
            .GroupBy(x => x.IdProf)
            .Select(g => new { IdProfesional = g.Key!.Value, Monto = g.Sum(x => x.Total) })
            .ToList();
        var profesionales = await _context.Profesionales.AsNoTracking().ToListAsync();
        var totalAtribuido = ingresosPorProfDb.Sum(x => x.Monto);

        var ingresosProf = ingresosPorProfDb
            .Join(profesionales,
                i => i.IdProfesional,
                p => p.IdProfesional,
                (i, p) => new IngresoProfesionalViewModel
                {
                    Nombre = $"{p.Nombres} {p.Apellidos}",
                    Monto = i.Monto,
                    Porcentaje = totalAtribuido > 0 ? Math.Round((double)(i.Monto * 100m / totalAtribuido), 1) : 0
                })
            .OrderByDescending(x => x.Monto)
            .ToList();

        var anioActual = DateTime.Today.Year;
        var citasMensuales = await _context.Citas.AsNoTracking()
            .Where(c => c.Estado != "Cancelada" &&
                        c.FechaHora >= new DateTime(anioActual - 1, 1, 1) &&
                        c.FechaHora < new DateTime(anioActual + 1, 1, 1))
            .GroupBy(c => new { c.FechaHora.Year, c.FechaHora.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Cantidad = g.Count() })
            .ToListAsync();
        var nombresMes = new[] { "Ene", "Feb", "Mar", "Abr", "May", "Jun", "Jul", "Ago", "Sep", "Oct", "Nov", "Dic" };
        var crecimiento = Enumerable.Range(1, DateTime.Today.Month)
            .Select(m => new CrecimientoItemViewModel
            {
                Mes = nombresMes[m - 1],
                ValorActual = citasMensuales.FirstOrDefault(x => x.Year == anioActual && x.Month == m)?.Cantidad ?? 0,
                ValorAnterior = citasMensuales.FirstOrDefault(x => x.Year == anioActual - 1 && x.Month == m)?.Cantidad ?? 0
            })
            .ToList();

        var estadosPeriodo = await _context.Citas.AsNoTracking()
            .Where(c => c.FechaHora >= periodoActual && c.FechaHora < periodoSiguiente)
            .Select(c => c.Estado)
            .ToListAsync();
        var conteosEstados = estadosPeriodo
            .GroupBy(NormalizarEstadoCita)
            .ToDictionary(g => g.Key, g => g.Count());
        var totalEstados = estadosPeriodo.Count;
        var estadoCitas = new[]
        {
            ("Completadas", "completada"),
            ("Programadas", "programada"),
            ("En proceso", "en proceso"),
            ("Canceladas", "cancelada"),
            ("Inasistencias", "inasistencia"),
            ("Otros", "otro")
        }
        .Select(x =>
        {
            var cantidad = conteosEstados.GetValueOrDefault(x.Item2);
            return new EstadoCitaViewModel
            {
                Estado = x.Item1,
                Cantidad = cantidad,
                Porcentaje = totalEstados > 0 ? Math.Round(cantidad * 100.0 / totalEstados, 1) : 0
            };
        })
        .Where(x => x.Cantidad > 0)
        .ToList();

        var conteosProcedimientos = citasPorServicio
            .Where(x => x.IdServicio.HasValue)
            .ToDictionary(x => x.IdServicio!.Value, x => x.Cantidad);
        var ingresosPorServicio = await _context.DetallesFactura.AsNoTracking()
            .Where(d => d.IdServicio != null &&
                        d.Factura != null &&
                        d.Factura.Estado == "pagada" &&
                        (d.Factura.FechaPago ?? d.Factura.FechaFactura) >= periodoActual &&
                        (d.Factura.FechaPago ?? d.Factura.FechaFactura) < periodoSiguiente)
            .GroupBy(d => d.IdServicio!.Value)
            .Select(g => new { IdServicio = g.Key, Ingresos = g.Sum(d => d.SubtotalLinea) })
            .ToDictionaryAsync(x => x.IdServicio, x => x.Ingresos);
        var procedimientos = serviciosDb
            .Select(servicio =>
            {
                var cantidad = conteosProcedimientos.GetValueOrDefault(servicio.IdServicio);
                var ingreso = ingresosPorServicio.GetValueOrDefault(servicio.IdServicio);
                var anterior = citasPorServicioAnterior.GetValueOrDefault(servicio.IdServicio);
                return new ProcedimientoReporteViewModel
                {
                    Nombre = servicio.Nombre,
                    Icono = "dentistry",
                    Cantidad = cantidad,
                    IngresosGenerados = ingreso,
                    PrecioPromedio = cantidad > 0 ? ingreso / cantidad : 0,
                    Tendencia = CambioPorcentual((double)cantidad, anterior) ?? 0
                };
            })
            .OrderByDescending(x => x.Cantidad)
            .ToList();

        var filtroCategoria = NormalizarCategoria(categoria) ?? "todos";
        var reportesQuery = _context.ReportesGenerados.AsNoTracking().AsQueryable();
        if (filtroCategoria != "todos")
        {
            reportesQuery = reportesQuery.Where(r => r.Categoria == filtroCategoria);
        }
        var reportesRecientes = await reportesQuery
            .OrderByDescending(r => r.FechaGeneracionUtc)
            .Take(20)
            .Select(r => new ReporteRecienteItemViewModel
            {
                Id = r.IdReporte,
                Nombre = r.Nombre,
                Categoria = r.Categoria,
                Icono = IconoCategoria(r.Categoria),
                FechaGenerado = r.FechaGeneracionUtc,
                Estado = "Listo"
            })
            .ToListAsync();

        var model = new ReportesViewModel
        {
            NombreUsuario = userName,
            Rol = userRole,
            FotoPerfilUrl = "/images/Imagenes/Logos/logo.jpg",
            UltimaActualizacion = DateTime.Now,
            IngresosTotales = estadisticas.IngresosPagados,
            VariacionIngresos = CambioPorcentual(estadisticas.IngresosPagados, estadisticasAnteriores.IngresosPagados),
            NuevosPacientes = estadisticas.NuevosPacientes,
            VariacionPacientes = CambioPorcentual((double)estadisticas.NuevosPacientes, estadisticasAnteriores.NuevosPacientes),
            CitasCompletadas = estadisticas.CitasCompletadas,
            VariacionCitas = CambioPorcentual((double)estadisticas.CitasCompletadas, estadisticasAnteriores.CitasCompletadas),
            TasaRetencion = estadisticas.TasaRetencion,
            VariacionRetencion = CambioPorcentual(estadisticas.TasaRetencion, estadisticasAnteriores.TasaRetencion),
            TasaInasistencia = estadisticas.TasaInasistencia,
            VariacionInasistencia = CambioPorcentual(estadisticas.TasaInasistencia, estadisticasAnteriores.TasaInasistencia),
            CitasDelPeriodo = estadisticas.TotalCitas,
            VariacionCitasPeriodo = CambioPorcentual((double)estadisticas.TotalCitas, estadisticasAnteriores.TotalCitas),
            FiltroCategoria = filtroCategoria.ToLowerInvariant(),

            CrecimientoClinica = crecimiento,
            DistribucionServicios = distribucion,
            IngresosPorProfesional = ingresosProf,
            EstadoCitas = estadoCitas,
            Procedimientos = procedimientos,
            ReportesRecientes = reportesRecientes
        };

        return View("~/Views/Reportes/vista_admin/index.cshtml", model);
    }

    [HttpGet]
    [Authorize(Roles = "Profesional")]
    [Route("reportes/vista-prof")]
    public async Task<IActionResult> VistaProf()
    {
        if (!int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var userId))
        {
            return Forbid();
        }

        var profesional = await _context.Profesionales.AsNoTracking()
            .FirstOrDefaultAsync(p => p.IdUsuario == userId);
        if (profesional is null)
        {
            return NotFound();
        }

        var ahora = ColombiaClock.Now;
        var inicioMes = new DateTime(ahora.Year, ahora.Month, 1);
        var inicioMesSiguiente = inicioMes.AddMonths(1);
        var inicioMesAnterior = inicioMes.AddMonths(-1);
        var citasProfesional = _context.Citas.AsNoTracking()
            .Where(c => c.IdProfesional == profesional.IdProfesional);
        var citasMes = citasProfesional
            .Where(c => c.FechaHora >= inicioMes && c.FechaHora < inicioMesSiguiente);
        var citasMesAnterior = citasProfesional
            .Where(c => c.FechaHora >= inicioMesAnterior && c.FechaHora < inicioMes);

        var totalCitas = await citasMes.CountAsync();
        var totalCitasMesAnterior = await citasMesAnterior.CountAsync();
        var pacientesUnicos = await citasMes.Select(c => c.IdPaciente).Distinct().CountAsync();
        var cancelaciones = await citasMes.CountAsync(c => c.Estado.ToLower() == "cancelada");
        var facturasPagadas = _context.Facturas.AsNoTracking()
            .Where(f => f.IdProfesional == profesional.IdProfesional && f.Estado.ToLower() == "pagada");
        var ingresos = await facturasPagadas
            .Where(f => f.FechaFactura >= inicioMes && f.FechaFactura < inicioMesSiguiente)
            .SumAsync(f => (decimal?)f.Total) ?? 0m;
        var ingresosMesAnterior = await facturasPagadas
            .Where(f => f.FechaFactura >= inicioMesAnterior && f.FechaFactura < inicioMes)
            .SumAsync(f => (decimal?)f.Total) ?? 0m;

        var tratamientosPorServicio = await citasMes
            .Where(c => c.IdServicio != null &&
                (c.Estado.ToLower() == "atendida" || c.Estado.ToLower() == "completada" || c.Estado.ToLower() == "finalizada"))
            .GroupBy(c => c.IdServicio!.Value)
            .Select(g => new { IdServicio = g.Key, Cantidad = g.Count() })
            .OrderByDescending(g => g.Cantidad)
            .Take(5)
            .ToListAsync();
        var idsServicios = tratamientosPorServicio.Select(t => t.IdServicio).ToArray();
        var nombresServicios = idsServicios.Length == 0
            ? new Dictionary<int, string>()
            : await _context.Servicios.AsNoTracking()
                .Where(s => idsServicios.Contains(s.IdServicio))
                .ToDictionaryAsync(s => s.IdServicio, s => s.Nombre);
        var totalTratamientos = tratamientosPorServicio.Sum(t => t.Cantidad);
        var tratamientos = tratamientosPorServicio
            .Where(t => nombresServicios.ContainsKey(t.IdServicio))
            .Select(t => new TratamientoProfViewModel
            {
                Nombre = nombresServicios[t.IdServicio],
                Cantidad = t.Cantidad,
                Porcentaje = totalTratamientos == 0 ? 0 : Math.Round(t.Cantidad * 100d / totalTratamientos, 1)
            })
            .ToList();

        var pacientesFrecuentesStats = await citasProfesional
            .Where(c => c.FechaHora >= ahora.AddMonths(-6) && c.FechaHora <= ahora)
            .GroupBy(c => c.IdPaciente)
            .Select(g => new { IdPaciente = g.Key, Citas = g.Count() })
            .OrderByDescending(g => g.Citas)
            .Take(5)
            .ToListAsync();
        var idsPacientes = pacientesFrecuentesStats.Select(p => p.IdPaciente).ToArray();
        var pacientes = idsPacientes.Length == 0
            ? new Dictionary<int, Paciente>()
            : await _context.Pacientes.AsNoTracking()
                .Where(p => idsPacientes.Contains(p.IdPaciente))
                .ToDictionaryAsync(p => p.IdPaciente);
        var pacientesFrecuentes = pacientesFrecuentesStats
            .Where(p => pacientes.ContainsKey(p.IdPaciente))
            .Select((p, index) =>
            {
                var paciente = pacientes[p.IdPaciente];
                var nombre = $"{paciente.Nombres} {paciente.Apellidos}".Trim();
                var iniciales = string.Concat(nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Take(2)
                    .Select(parte => parte[0]))
                    .ToUpperInvariant();
                return new PacienteFrecuenteViewModel
                {
                    Iniciales = string.IsNullOrWhiteSpace(iniciales) ? "P" : iniciales,
                    Color = index % 2 == 0 ? "blue" : "purple",
                    Nombre = nombre,
                    Tipo = "Paciente frecuente",
                    TipoClase = "patient-badge--recurrent",
                    Citas = p.Citas,
                    Periodo = "últimos 6 meses"
                };
            })
            .ToList();

        var proximosDias = new List<ProximoDiaViewModel>();
        for (var offset = 0; offset < 3; offset++)
        {
            var fecha = ahora.Date.AddDays(offset);
            var cantidad = await citasProfesional.CountAsync(c =>
                c.FechaHora >= fecha && c.FechaHora < fecha.AddDays(1));
            proximosDias.Add(new ProximoDiaViewModel
            {
                DiaNombre = offset switch { 0 => "Hoy", 1 => "Mañana", _ => "Siguiente" },
                ClaveDia = offset switch
                {
                    0 => "reportesprof.day_today",
                    1 => "reportesprof.day_tomorrow",
                    _ => "reportesprof.day_next"
                },
                Fecha = fecha.ToString("dd MMM", CultureInfo.GetCultureInfo("es-CO")),
                CantidadCitas = cantidad,
                EsHoy = offset == 0
            });
        }

        var citasRecientes = await citasProfesional
            .Include(c => c.Paciente)
            .Include(c => c.Servicio)
            .OrderByDescending(c => c.FechaHora)
            .Take(5)
            .ToListAsync();
        var citasRecientesViewModel = citasRecientes.Select(c => new CitaRecienteViewModel
            {
                NombrePaciente = c.Paciente != null ? $"{c.Paciente.Nombres} {c.Paciente.Apellidos}" : "Paciente",
                IdPaciente = c.IdPaciente,
                Servicio = c.Servicio != null ? c.Servicio.Nombre : "Consulta general",
                Hora = c.FechaHora.ToString("hh:mm tt", CultureInfo.GetCultureInfo("es-CO")),
                Estado = c.Estado,
                EstadoClase = NormalizarEstadoCita(c.Estado) switch
                {
                    "completada" => "completada",
                    "programada" => "programada",
                    "en proceso" => "en-proceso",
                    "cancelada" => "cancelada",
                    "inasistencia" => "no-asistio",
                    _ => "neutral"
                },
                ClaveEstado = NormalizarEstadoCita(c.Estado) switch
                {
                    "completada" => "reportesprof.status_completed",
                    "programada" => "reportesprof.status_scheduled",
                    "en proceso" => "reportesprof.status_in_progress",
                    "cancelada" => "reportesprof.status_cancelled",
                    "inasistencia" => "reportesprof.status_no_show",
                    _ => "reportesprof.status_other"
                }
            })
            .ToList();

        var model = new ReportesProfesionalViewModel
        {
            DoctorName = User.Identity?.Name ?? profesional.NombreProfesional,
            DoctorRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "Odontólogo(a)",
            TotalCitas = totalCitas,
            TendenciaCitas = CambioPorcentual((double)totalCitas, (double)totalCitasMesAnterior),
            Ingresos = ingresos,
            TendenciaIngresos = CambioPorcentual(ingresos, ingresosMesAnterior),
            PacientesUnicos = pacientesUnicos,
            Cancelaciones = cancelaciones,
            TasaCancelacion = totalCitas == 0 ? 0 : Math.Round(cancelaciones * 100d / totalCitas, 1),
            CitasHoy = proximosDias[0].CantidadCitas,
            CitasManana = proximosDias[1].CantidadCitas,
            Tratamientos = tratamientos,
            PacientesFrecuentes = pacientesFrecuentes,
            ProximosDias = proximosDias,
            UltimasCitas = citasRecientesViewModel
        };

        return View("~/Views/Reportes/vista_prof/index.cshtml", model);
    }

    [HttpGet]
    [Authorize(Roles = "Recepcionista,Administrador")]
    [Route("reportes/vista-recepcion")]
    public async Task<IActionResult> VistaRecepcion(CancellationToken ct = default)
    {
        var userName = User.Identity?.Name ?? "Recepcionista";
        var hoy = ColombiaClock.Today;
        var manana = hoy.AddDays(1);
        ViewData["FechaOperativa"] = hoy;

        var citasHoy = await _context.Citas
            .AsNoTracking()
            .Where(c => c.FechaHora >= hoy && c.FechaHora < manana)
            .OrderBy(c => c.FechaHora)
            .Select(c => new
            {
                c.IdCita,
                c.IdPaciente,
                c.IdProfesional,
                c.IdServicio,
                c.FechaHora,
                Estado = c.EstadoCita != null ? c.EstadoCita.NombreEstado : c.Estado,
                ProfesionalNombre = c.Profesional != null ? c.Profesional.Nombres + " " + c.Profesional.Apellidos : "Sin asignar",
                ServicioNombre = c.Servicio != null ? c.Servicio.Nombre : "Consulta general"
            })
            .ToListAsync(ct);

        var pacientesRecientesDb = await _context.Pacientes
            .OrderByDescending(p => p.IdPaciente)
            .Take(4)
            .ToListAsync(ct);


        var cultura = CultureInfo.GetCultureInfo("es-CO");
        var estadosProgramados = new[] { "agendada", "agendado", "programada", "programado", "confirmada", "confirmado" };
        var siguientes = await _context.Citas.AsNoTracking()
            .Where(c => c.FechaHora >= manana && estadosProgramados.Contains(
                ((c.EstadoCita != null ? c.EstadoCita.NombreEstado : c.Estado) ?? "").Trim().ToLower()))
            .OrderBy(c => c.FechaHora).ThenBy(c => c.IdCita)
            .Take(3)
            .Select(c => new
            {
                c.IdCita,
                c.FechaHora,
                Profesional = c.Profesional != null ? c.Profesional.Nombres + " " + c.Profesional.Apellidos : "Sin asignar",
                Servicio = c.Servicio != null ? c.Servicio.Nombre : "Consulta general"
            })
            .ToListAsync(ct);

        var proximasCitas = siguientes.Select(c => new AgendaItemViewModel
        {
            IdCita = c.IdCita,
            Time = c.FechaHora.ToString("d MMM yyyy · HH:mm", cultura),
            Professional = c.Profesional,
            Service = c.Servicio
        }).ToList();

        var atendidas = citasHoy.Count(c => string.Equals(c.Estado, "Atendida", StringComparison.OrdinalIgnoreCase));
        var canceladas = citasHoy.Count(c => string.Equals(c.Estado, "Cancelada", StringComparison.OrdinalIgnoreCase));
        var model = new ReportesOperativosViewModel
        {
            ReceptionistName = userName,
            ProximasCitas = proximasCitas,
            Kpis =
            [
                new() { Label = "Citas del día", Value = citasHoy.Count.ToString(), Icon = "calendar_month" },
                new() { Label = "Atendidas", Value = atendidas.ToString(), Icon = "check_circle" },
                new() { Label = "Canceladas", Value = canceladas.ToString(), Icon = "event_busy" }
            ],
            Agenda = citasHoy.Select(c => new AgendaItemViewModel
            {
                IdCita = c.IdCita,
                Time = c.FechaHora.ToString("HH:mm", cultura),
                Professional = c.ProfesionalNombre,
                Service = c.ServicioNombre,
                Status = c.Estado ?? "Sin estado",
                StatusVariant = c.Estado?.ToLowerInvariant() switch
                {
                    "atendida" => "green",
                    "confirmada" => "blue",
                    "cancelada" => "red",
                    _ => "neutral"
                }
            }).ToList(),
            PacientesRecientes = pacientesRecientesDb.Select(p => new PacienteRecienteViewModel
            {
                IdPaciente = p.IdPaciente,
                Name = $"{p.Nombres} {p.Apellidos}",
                ContactInfo = !string.IsNullOrWhiteSpace(p.Telefono) ? p.Telefono : p.Correo ?? ""
            }).ToList()
        };

        return View("~/Views/Reportes/vista_recepcion/index.cshtml", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("reportes/generar")]
    public async Task<IActionResult> GenerarReporte(ReporteGeneradoRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var categoria = NormalizarCategoria(request.Categoria);
        if (categoria is null || categoria is "General" or "todos")
        {
            ModelState.AddModelError(nameof(request.Categoria), "Selecciona una categoría válida.");
            return ValidationProblem(ModelState);
        }

        if (!TryParsePeriodo(request.Periodo, out var periodoInicio) ||
            periodoInicio > new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1))
        {
            ModelState.AddModelError(nameof(request.Periodo), "El periodo debe ser un mes válido y no futuro.");
            return ValidationProblem(ModelState);
        }

        var nombre = request.Nombre.Trim();
        if (nombre.Length == 0)
        {
            ModelState.AddModelError(nameof(request.Nombre), "El nombre del reporte es obligatorio.");
            return ValidationProblem(ModelState);
        }

        var notas = request.Notas?.Trim();
        var snapshot = await CrearSnapshotAsync(nombre, categoria, request.Periodo, notas);
        var usuarioId = int.TryParse(
            User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            out var parsedUsuarioId) ? parsedUsuarioId : (int?)null;
        var reporte = new ReporteGenerado
        {
            Nombre = nombre,
            Categoria = categoria,
            Periodo = request.Periodo,
            Notas = notas,
            FechaGeneracionUtc = DateTime.UtcNow,
            GeneradoPor = usuarioId,
            DatosJson = JsonSerializer.Serialize(snapshot)
        };

        _context.ReportesGenerados.Add(reporte);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            id = reporte.IdReporte,
            nombre = reporte.Nombre,
            categoria = reporte.Categoria,
            periodo = reporte.Periodo,
            fechaGeneracionUtc = reporte.FechaGeneracionUtc,
            descargarCsvUrl = $"/reportes/descargar/{reporte.IdReporte}?formato=csv",
            descargarPdfUrl = $"/reportes/descargar/{reporte.IdReporte}?formato=pdf"
        });
    }

    [HttpGet]
    [Route("reportes/descargar/{id:int}")]
    public async Task<IActionResult> DescargarReporte(int id, string formato = "csv")
    {
        if (string.IsNullOrWhiteSpace(formato))
        {
            return BadRequest("El formato debe ser csv o pdf.");
        }

        var reporte = await _context.ReportesGenerados.AsNoTracking()
            .FirstOrDefaultAsync(r => r.IdReporte == id);
        if (reporte is null)
        {
            return NotFound();
        }

        if (!TryDeserializeSnapshot(reporte.DatosJson, out var snapshot))
        {
            return Problem("El contenido guardado del reporte no es válido.", statusCode: 500);
        }

        if (formato.Equals("csv", StringComparison.OrdinalIgnoreCase))
        {
            return File(CrearCsv(ObtenerFilasCsv(reporte, snapshot), true),
                "text/csv; charset=utf-8", $"Reporte_{id}_{reporte.Periodo}.csv");
        }

        if (formato.Equals("pdf", StringComparison.OrdinalIgnoreCase))
        {
            return File(CrearPdf(snapshot, true, true),
                "application/pdf", $"Reporte_{id}_{reporte.Periodo}.pdf");
        }

        return BadRequest("El formato debe ser csv o pdf.");
    }

    [HttpGet]
    [Route("reportes/exportar")]
    public async Task<IActionResult> Exportar(
        string formato,
        bool todos = false,
        bool encabezados = true,
        bool incluirGraficos = true,
        bool incluirResumen = true)
    {
        if (string.IsNullOrWhiteSpace(formato))
        {
            return BadRequest("El formato debe ser csv o pdf.");
        }

        if (formato.Equals("csv", StringComparison.OrdinalIgnoreCase))
        {
            List<CsvReporteRow> filas;
            if (todos)
            {
                var reportes = await _context.ReportesGenerados.AsNoTracking()
                    .OrderByDescending(r => r.FechaGeneracionUtc)
                    .ToListAsync();
                filas = reportes.SelectMany(reporte =>
                {
                    if (!TryDeserializeSnapshot(reporte.DatosJson, out var snapshot))
                    {
                        throw new InvalidOperationException($"El reporte {reporte.IdReporte} tiene un contenido guardado no válido.");
                    }

                    return ObtenerFilasCsv(reporte, snapshot);
                }).ToList();
            }
            else
            {
                var periodo = DateTime.Today.ToString("yyyy-MM", CultureInfo.InvariantCulture);
                var snapshot = await CrearSnapshotAsync("Resumen actual", "General", periodo, null);
                filas = ObtenerFilasCsv(null, snapshot);
            }

            return File(CrearCsv(filas, encabezados), "text/csv; charset=utf-8",
                $"Reportes_{DateTime.Today:yyyyMMdd}.csv");
        }

        if (formato.Equals("pdf", StringComparison.OrdinalIgnoreCase))
        {
            var periodo = DateTime.Today.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            var snapshot = await CrearSnapshotAsync("Resumen actual", "General", periodo, null);
            return File(CrearPdf(snapshot, incluirResumen, incluirGraficos), "application/pdf",
                $"Reporte_{periodo}.pdf");
        }

        return BadRequest("El formato debe ser csv o pdf.");
    }

    private async Task<ReporteGeneradoSnapshot> CrearSnapshotAsync(
        string nombre,
        string categoria,
        string periodo,
        string? notas)
    {
        if (!TryParsePeriodo(periodo, out var inicio))
        {
            throw new ArgumentException("El periodo del reporte no es válido.", nameof(periodo));
        }

        var fin = inicio.AddMonths(1);
        var metricas = new List<ReporteMetricaSnapshot>();
        var cultura = CultureInfo.GetCultureInfo("es-CO");

        if (categoria is "Financiero" or "General")
        {
            var facturas = await _context.Facturas.AsNoTracking()
                .Where(f => f.Estado == "pagada" &&
                            (f.FechaPago ?? f.FechaFactura) >= inicio &&
                            (f.FechaPago ?? f.FechaFactura) < fin)
                .Select(f => f.Total)
                .ToListAsync();
            metricas.Add(new() { Indicador = "Ingresos pagados", Valor = facturas.Sum().ToString("C0", cultura) });
            metricas.Add(new() { Indicador = "Facturas pagadas", Valor = facturas.Count.ToString("N0", cultura) });
        }

        if (categoria is "Pacientes" or "General")
        {
            var nuevos = await _context.Pacientes.AsNoTracking()
                .CountAsync(p => p.FechaRegistro >= inicio && p.FechaRegistro < fin);
            var activos = await _context.Pacientes.AsNoTracking()
                .CountAsync(p => p.Estado.ToLower() == "activo");
            metricas.Add(new() { Indicador = "Pacientes nuevos en el periodo", Valor = nuevos.ToString("N0", cultura) });
            metricas.Add(new() { Indicador = "Pacientes activos", Valor = activos.ToString("N0", cultura) });
        }

        if (categoria is "Operativo" or "General")
        {
            var estadisticas = await ObtenerEstadisticasPeriodoAsync(inicio, fin);
            metricas.Add(new() { Indicador = "Citas en el periodo", Valor = estadisticas.TotalCitas.ToString("N0", cultura) });
            metricas.Add(new() { Indicador = "Citas completadas", Valor = estadisticas.CitasCompletadas.ToString("N0", cultura) });
            metricas.Add(new() { Indicador = "Inasistencias", Valor = estadisticas.CitasNoAsistidas.ToString("N0", cultura) });
            metricas.Add(new() { Indicador = "Tasa de retención", Valor = estadisticas.TasaRetencion.ToString("N1", cultura) + "%" });
        }

        if (categoria is "Logística" or "General")
        {
            var serviciosActivos = await _context.Servicios.AsNoTracking()
                .CountAsync(s => s.Estado.ToLower() == "activo");
            var inventario = await _context.Inventarios.AsNoTracking()
                .Select(i => new { i.StockActual, i.StockMinimo, i.PrecioUnitario })
                .ToListAsync();
            metricas.Add(new() { Indicador = "Servicios activos", Valor = serviciosActivos.ToString("N0", cultura) });
            metricas.Add(new() { Indicador = "Artículos de inventario", Valor = inventario.Count.ToString("N0", cultura) });
            metricas.Add(new()
            {
                Indicador = "Artículos bajo el mínimo",
                Valor = inventario.Count(i => i.StockActual <= i.StockMinimo).ToString("N0", cultura)
            });
            metricas.Add(new()
            {
                Indicador = "Valor estimado del inventario",
                Valor = inventario.Sum(i => i.StockActual * i.PrecioUnitario).ToString("C0", cultura)
            });
        }

        return new ReporteGeneradoSnapshot
        {
            Nombre = nombre,
            Categoria = categoria,
            Periodo = periodo,
            Notas = notas,
            FechaGeneracionUtc = DateTime.UtcNow,
            Metricas = metricas
        };
    }

    private async Task<EstadisticasPeriodo> ObtenerEstadisticasPeriodoAsync(DateTime inicio, DateTime fin)
    {
        var citas = await _context.Citas.AsNoTracking()
            .Where(c => c.FechaHora >= inicio && c.FechaHora < fin)
            .Select(c => new { c.Estado, c.IdPaciente })
            .ToListAsync();
        var completadas = citas.Where(c => NormalizarEstadoCita(c.Estado) == "completada").ToList();
        var pacientesConCitaCompletada = completadas.Select(c => c.IdPaciente).Distinct().Count();
        var pacientesRecurrentes = completadas.GroupBy(c => c.IdPaciente).Count(g => g.Count() > 1);
        var noAsistidas = citas.Count(c => NormalizarEstadoCita(c.Estado) == "inasistencia");
        var ingresos = await _context.Facturas.AsNoTracking()
            .Where(f => f.Estado == "pagada" &&
                        (f.FechaPago ?? f.FechaFactura) >= inicio &&
                        (f.FechaPago ?? f.FechaFactura) < fin)
            .SumAsync(f => (decimal?)f.Total) ?? 0m;
        var nuevosPacientes = await _context.Pacientes.AsNoTracking()
            .CountAsync(p => p.FechaRegistro >= inicio && p.FechaRegistro < fin);

        return new EstadisticasPeriodo(
            ingresos,
            nuevosPacientes,
            citas.Count,
            completadas.Count,
            noAsistidas,
            pacientesConCitaCompletada > 0
                ? pacientesRecurrentes * 100.0 / pacientesConCitaCompletada
                : 0,
            citas.Count > 0 ? noAsistidas * 100.0 / citas.Count : 0);
    }

    private static double? CambioPorcentual(double actual, double anterior)
    {
        if (anterior == 0)
        {
            return null;
        }

        return Math.Round((actual - anterior) * 100.0 / anterior, 1);
    }

    private static double? CambioPorcentual(decimal actual, decimal anterior)
    {
        return CambioPorcentual((double)actual, (double)anterior);
    }

    private static string NormalizarEstadoCita(string? estado)
    {
        var normalizado = QuitarDiacriticos(estado ?? string.Empty).Trim().ToLowerInvariant();
        if (normalizado is "atendida" or "completada" or "finalizada")
        {
            return "completada";
        }

        if (normalizado is "agendada" or "programada" or "confirmada" or "pendiente")
        {
            return "programada";
        }

        if (normalizado.Contains("proceso") || normalizado == "en curso")
        {
            return "en proceso";
        }

        if (normalizado.Contains("cancel"))
        {
            return "cancelada";
        }

        if (normalizado.Contains("inasist") || normalizado.Contains("no asist") || normalizado == "no show")
        {
            return "inasistencia";
        }

        return "otro";
    }

    private static string? NormalizarCategoria(string? categoria)
    {
        return QuitarDiacriticos(categoria ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "financiero" => "Financiero",
            "pacientes" => "Pacientes",
            "operativo" => "Operativo",
            "logistica" => "Logística",
            "general" => "General",
            "todos" or "" => "todos",
            _ => null
        };
    }

    private static string IconoCategoria(string categoria) => NormalizarCategoria(categoria) switch
    {
        "Financiero" => "payments",
        "Pacientes" => "group",
        "Operativo" => "monitoring",
        "Logística" => "inventory_2",
        _ => "description"
    };

    private static string QuitarDiacriticos(string value)
    {
        var descompuesto = value.Normalize(NormalizationForm.FormD);
        var resultado = new StringBuilder(descompuesto.Length);
        foreach (var caracter in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) != UnicodeCategory.NonSpacingMark)
            {
                resultado.Append(caracter);
            }
        }

        return resultado.ToString().Normalize(NormalizationForm.FormC);
    }

    private static bool TryParsePeriodo(string periodo, out DateTime inicio)
    {
        return DateTime.TryParseExact(
            periodo + "-01",
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out inicio);
    }

    private static bool TryDeserializeSnapshot(string json, out ReporteGeneradoSnapshot snapshot)
    {
        snapshot = JsonSerializer.Deserialize<ReporteGeneradoSnapshot>(json)!;
        return snapshot is not null;
    }

    private static List<CsvReporteRow> ObtenerFilasCsv(ReporteGenerado? reporte, ReporteGeneradoSnapshot snapshot)
    {
        return snapshot.Metricas.Select(m => new CsvReporteRow(
            reporte?.Nombre ?? snapshot.Nombre,
            reporte?.Categoria ?? snapshot.Categoria,
            reporte?.Periodo ?? snapshot.Periodo,
            reporte?.FechaGeneracionUtc ?? snapshot.FechaGeneracionUtc,
            m.Indicador,
            m.Valor)).ToList();
    }

    private static byte[] CrearCsv(IEnumerable<CsvReporteRow> filas, bool incluirEncabezados)
    {
        var csv = new StringBuilder();
        if (incluirEncabezados)
        {
            csv.AppendLine("Reporte,Categoría,Periodo,Fecha de generación,Indicador,Valor");
        }

        foreach (var fila in filas)
        {
            csv.AppendLine(string.Join(",", new[]
            {
                fila.Nombre,
                fila.Categoria,
                fila.Periodo,
                fila.FechaGeneracionUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                fila.Indicador,
                fila.Valor
            }.Select(EscaparCsv)));
        }

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
    }

    private static string EscaparCsv(string? value)
    {
        var safeValue = value ?? string.Empty;
        if (safeValue.Length > 0 && safeValue[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
        {
            safeValue = "'" + safeValue;
        }

        return "\"" + safeValue.Replace("\"", "\"\"") + "\"";
    }

    private static byte[] CrearPdf(ReporteGeneradoSnapshot snapshot, bool incluirResumen, bool incluirMetricas)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(style => style.FontSize(10).FontColor("#1f2937"));
                page.Header().Column(header =>
                {
                    header.Item().Text("SmileTrack").FontSize(10).FontColor("#64748b");
                    header.Item().Text(snapshot.Nombre).FontSize(20).Bold().FontColor("#123b72");
                });
                page.Content().PaddingVertical(18).Column(column =>
                {
                    if (incluirResumen)
                    {
                        column.Item().Text($"Categoría: {snapshot.Categoria}");
                        column.Item().Text($"Periodo: {snapshot.Periodo}");
                        column.Item().Text($"Generado: {snapshot.FechaGeneracionUtc.ToLocalTime():dd/MM/yyyy HH:mm}");
                    }

                    if (incluirMetricas)
                    {
                        column.Item().PaddingTop(16).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(1);
                            });
                            table.Header(headerRow =>
                            {
                                headerRow.Cell().Background("#123b72").Padding(6).Text("Indicador").FontColor("#ffffff").Bold();
                                headerRow.Cell().Background("#123b72").Padding(6).Text("Valor").FontColor("#ffffff").Bold();
                            });
                            foreach (var metrica in snapshot.Metricas)
                            {
                                table.Cell().BorderBottom(1).BorderColor("#e2e8f0").Padding(6).Text(metrica.Indicador);
                                table.Cell().BorderBottom(1).BorderColor("#e2e8f0").Padding(6).Text(metrica.Valor);
                            }
                        });
                    }
                });
                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("SmileTrack · Reporte agregado · ");
                    text.CurrentPageNumber();
                });
            });
        }).GeneratePdf();
    }

    private sealed record EstadisticasPeriodo(
        decimal IngresosPagados,
        int NuevosPacientes,
        int TotalCitas,
        int CitasCompletadas,
        int CitasNoAsistidas,
        double TasaRetencion,
        double TasaInasistencia);

    private sealed record CsvReporteRow(
        string Nombre,
        string Categoria,
        string Periodo,
        DateTime FechaGeneracionUtc,
        string Indicador,
        string Valor);

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