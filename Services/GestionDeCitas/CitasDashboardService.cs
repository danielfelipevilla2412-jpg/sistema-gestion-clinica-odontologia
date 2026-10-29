/**
 * ============================================
 * SmileTrack — Módulo: Gestión de Citas
 * Componente: CitasDashboardService
 * ============================================
 * Archivo: Services/GestionDeCitas/CitasDashboardService.cs
 *
 * PROPÓSITO Y JUSTIFICACIÓN:
 * Implementa la agregación de métricas y construcción de KPIs para el Dashboard de Citas.
 * Extrae todas las consultas complejas de agregación LINQ/SQL fuera de los controladores MVC,
 * asegurando alto desempeño y mantenibilidad centralizada de estadísticas clínicas.
 *
 * REGLAS DE NEGOCIO PRINCIPALES:
 * - Filtra únicamente citas no eliminadas (soft delete check).
 * - Calcula porcentajes de ocupación diaria e índices de inasistencia/cancelaciones.
 *
 * DEPENDENCIAS TÉCNICAS:
 * - AppDbContext, ICitaService, CitasDashboardViewModel, EstadoCitaHelper
 * ============================================
 */

using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Helpers;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Models.ViewModels;
using System.Globalization;
using System.Text;

namespace SmileTrack_MVC.Services;

/// <summary>
/// Implementación de <see cref="ICitasDashboardService"/>.
/// Encapsula todas las consultas y agregaciones del dashboard de citas
/// del administrador, dejando el controlador completamente libre de lógica de datos.
/// </summary>
public sealed class CitasDashboardService : ICitasDashboardService
{
    private readonly AppDbContext                         _context;
    private readonly ICitaService                         _citaService;
    private readonly ILogger<CitasDashboardService>       _logger;

    public CitasDashboardService(
        AppDbContext                         context,
        ICitaService                         citaService,
        ILogger<CitasDashboardService>       logger)
    {
        _context     = context;
        _citaService = citaService;
        _logger      = logger;
    }

    // =========================================================================
    // PUNTO DE ENTRADA PÚBLICO
    // =========================================================================

    public async Task<CitasDashboardViewModel> ObtenerDashboardAsync(
        DateTime          fechaReferencia,
        CancellationToken cancellationToken = default)
    {
        var hoy       = fechaReferencia.Date;
        var inicioDia = hoy;
        var finDia    = hoy.AddDays(1);

        var inicioMes = new DateTime(hoy.Year, hoy.Month, 1);
        var finMes    = inicioMes.AddMonths(1);

        // Semana actual (lunes – hoy) y semana anterior completa para la variación.
        var lunes              = hoy.DayOfWeek == DayOfWeek.Sunday
                                    ? hoy.AddDays(-6)
                                    : hoy.AddDays(-(int)hoy.DayOfWeek + 1);
        var inicioSemana       = lunes;
        var inicioSemanaAnterior = lunes.AddDays(-7);
        var finSemanaAnterior  = lunes;

        try
        {
            // EF Core no permite lanzar varias consultas concurrentes sobre la misma
            // instancia de DbContext. Se ejecutan de forma secuencial para mantener
            // la seguridad y la estabilidad del dashboard sin romper transacciones ni
            // el tracking interno del contexto.
            var pacientesActivos = await ContarPacientesActivosAsync(cancellationToken);
            var profesionalesActivos = await ContarProfesionalesActivosAsync(cancellationToken);
            var citasHoy = await ContarCitasHoyAsync(inicioDia, finDia, cancellationToken);
            var resumen = await ObtenerResumenCitasMesAsync(inicioMes, finMes, cancellationToken);
            var semanaActual = await ContarCitasActivasAsync(inicioSemana, hoy.AddDays(1), cancellationToken);
            var semanaAnterior = await ContarCitasActivasAsync(inicioSemanaAnterior, finSemanaAnterior, cancellationToken);
            var topProfesionales = await ObtenerTopProfesionalesAsync(inicioMes, finMes, cancellationToken);
            var ocupacion = await CalcularOcupacionAsync(inicioMes, finMes, cancellationToken);

            return new CitasDashboardViewModel
            {
                Kpis = new DashboardKpiViewModel
                {
                    PacientesActivos = pacientesActivos,
                    CitasHoy = citasHoy,
                    ProfesionalesActivos = profesionalesActivos,
                    ValorEstimadoAtendidoMes = resumen.ValorAtendido,
                    VariacionCitasSemana = semanaActual - semanaAnterior
                },

                Estados = new DashboardEstadosViewModel
                {
                    Total = resumen.Total,
                    Atendidas = resumen.Atendidas,
                    Confirmadas = resumen.Confirmadas,
                    Programadas = resumen.Programadas,
                    Canceladas = resumen.Canceladas
                },

                OcupacionPorcentaje = ocupacion,
                TopProfesionales = topProfesionales
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error calculando dashboard de citas para {Fecha}.", hoy);
            throw;
        }
    }

    // =========================================================================
    // CONSULTAS PRIVADAS
    // =========================================================================

    private Task<int> ContarPacientesActivosAsync(CancellationToken ct) =>
        _context.Pacientes
                .AsNoTracking()
                .CountAsync(p => p.Estado == "activo", ct);

    private Task<int> ContarProfesionalesActivosAsync(CancellationToken ct) =>
        _context.Profesionales
                .AsNoTracking()
                .CountAsync(p => p.Estado == "activo", ct);

    /// <summary>
    /// Cuenta citas NO canceladas del día usando rango de fechas sargable
    /// (evita FechaHora.Date que no puede usar el índice IX_Cita_Estado_Fecha).
    /// </summary>
    private Task<int> ContarCitasHoyAsync(
        DateTime inicioDia, DateTime finDia, CancellationToken ct) =>
        _context.Citas
                .AsNoTracking()
                .CountAsync(c =>
                    c.FechaHora >= inicioDia &&
                    c.FechaHora < finDia    &&
                    c.Estado != "Cancelada" &&
                    c.Estado != "cancelada" &&
                    c.Estado != "Cancelado" &&
                    c.Estado != "cancelado",
                    ct);

    /// <summary>
    /// Cuenta citas activas (no canceladas) en un rango de fechas.
    /// Usado para calcular la variación semanal.
    /// </summary>
    private Task<int> ContarCitasActivasAsync(
        DateTime inicio, DateTime fin, CancellationToken ct) =>
        _context.Citas
                .AsNoTracking()
                .CountAsync(c =>
                    c.FechaHora >= inicio &&
                    c.FechaHora < fin     &&
                    c.Estado != "Cancelada" &&
                    c.Estado != "cancelada" &&
                    c.Estado != "Cancelado" &&
                    c.Estado != "cancelado",
                    ct);

    private sealed record ResumenMes(
        int Total, int Atendidas, int Confirmadas,
        int Programadas, int Canceladas, decimal ValorAtendido);

    private async Task<ResumenMes> ObtenerResumenCitasMesAsync(
        DateTime inicioMes, DateTime finMes, CancellationToken ct)
    {
        // Proyección ligera — solo lo que necesitamos, sin Include completos.
        var filas = await _context.Citas
            .AsNoTracking()
            .Where(c => c.FechaHora >= inicioMes && c.FechaHora < finMes)
            .Select(c => new
            {
                Estado          = c.EstadoCita != null ? c.EstadoCita.NombreEstado : c.Estado,
                PrecioServicio  = c.Servicio != null ? c.Servicio.Precio : 0m
            })
            .ToListAsync(ct);

        int     total       = 0, atendidas = 0, confirmadas = 0, programadas = 0, canceladas = 0;
        decimal valorAtendido = 0m;

        foreach (var fila in filas)
        {
            total++;
            switch (NormalizarEstado(fila.Estado))
            {
                case "atendida":
                    atendidas++;
                    valorAtendido += fila.PrecioServicio;
                    break;
                case "confirmada":
                    confirmadas++;
                    break;
                case "programada":
                    programadas++;
                    break;
                case "cancelada":
                    canceladas++;
                    break;
            }
        }

        return new ResumenMes(total, atendidas, confirmadas, programadas, canceladas, valorAtendido);
    }

    private async Task<IReadOnlyList<TopProfesionalViewModel>> ObtenerTopProfesionalesAsync(
        DateTime inicioMes, DateTime finMes, CancellationToken ct)
    {
        var top = await _context.Citas
            .AsNoTracking()
            .Where(c =>
                c.FechaHora >= inicioMes &&
                c.FechaHora <  finMes   &&
                (c.Estado == "Atendida" ||
                 c.Estado == "atendida" ||
                 c.Estado == "Completada" ||
                 c.Estado == "completada" ||
                 c.Estado == "Realizada" ||
                 c.Estado == "realizada" ||
                 (c.EstadoCita != null &&
                  (c.EstadoCita.NombreEstado == "Atendida" ||
                   c.EstadoCita.NombreEstado == "atendida" ||
                   c.EstadoCita.NombreEstado == "Completada" ||
                   c.EstadoCita.NombreEstado == "completada" ||
                   c.EstadoCita.NombreEstado == "Realizada" ||
                   c.EstadoCita.NombreEstado == "realizada"))) &&
                c.IdProfesional.HasValue)
            .GroupBy(c => c.IdProfesional)
            .Select(g => new { IdProfesional = g.Key!.Value, Total = g.Count() })
            .OrderByDescending(x => x.Total)
            .Take(3)
            .ToListAsync(ct);

        if (top.Count == 0)
            return [];

        var ids = top.Select(x => x.IdProfesional).ToArray();

        var profesionales = await _context.Profesionales
            .AsNoTracking()
            .Include(p => p.Usuario)
            .Include(p => p.Especialidades)
                .ThenInclude(pe => pe.Especialidad)
            .Where(p => ids.Contains(p.IdProfesional))
            .ToListAsync(ct);

        return top.Select(item =>
        {
            var p = profesionales.FirstOrDefault(x => x.IdProfesional == item.IdProfesional);

            var nombre = string.Concat(p?.Nombres, " ", p?.Apellidos).Trim();
            if (string.IsNullOrWhiteSpace(nombre))
                nombre = string.Concat(p?.Usuario?.Nombre, " ", p?.Usuario?.Apellidos).Trim();

            var especialidad = p?.Especialidades
                .FirstOrDefault(e => e.Principal)?.Especialidad?.Nombre
                ?? p?.Especialidades.FirstOrDefault()?.Especialidad?.Nombre;

            return new TopProfesionalViewModel
            {
                IdProfesional = item.IdProfesional,
                Nombre        = string.IsNullOrWhiteSpace(nombre)       ? "Sin nombre" : nombre,
                Especialidad  = string.IsNullOrWhiteSpace(especialidad) ? "General"   : especialidad,
                TotalCitas    = item.Total
            };
        }).ToList();
    }

    /// <summary>
    /// Calcula el porcentaje de ocupación de la agenda para el mes.
    /// Denominador = minutos disponibles según Horario_Profesional activos / duración de cita.
    /// Numerador   = citas activas del mes (excluye canceladas).
    /// </summary>
    private async Task<int> CalcularOcupacionAsync(
        DateTime inicioMes, DateTime finMes, CancellationToken ct)
    {
        var horarios = await _context.HorariosProfesional
            .AsNoTracking()
            .Where(h => h.Activo)
            .ToListAsync(ct);

        if (horarios.Count == 0)
            return 0;

        int duracion = await _citaService.ObtenerDuracionCitaMinutosAsync(ct);
        if (duracion <= 0)
            return 0;

        double minutosDisponibles = horarios.Sum(h =>
            MinutosHorarioEnMes(h, inicioMes, finMes));

        if (minutosDisponibles <= 0)
            return 0;

        double capacidad = minutosDisponibles / duracion;

        // Citas activas (no canceladas) del mes como numerador.
        int citasActivas = await _context.Citas
            .AsNoTracking()
            .CountAsync(c =>
                c.FechaHora >= inicioMes &&
                c.FechaHora <  finMes   &&
                c.Estado != "Cancelada" &&
                c.Estado != "cancelada" &&
                c.Estado != "Cancelado" &&
                c.Estado != "cancelado",
                ct);

        return Math.Clamp(
            (int)Math.Round(citasActivas / capacidad * 100d),
            0, 100);
    }

    // =========================================================================
    // HELPERS ESTÁTICOS
    // =========================================================================

    /// <summary>
    /// Suma los minutos de disponibilidad de un horario dentro de un mes.
    /// Itera día a día para manejar correctamente meses con diferente número de días.
    /// </summary>
    private static double MinutosHorarioEnMes(
        HorarioProfesional horario,
        DateTime           inicioMes,
        DateTime           finMes)
    {
        double minutosDiarios =
            (horario.HoraFin - horario.HoraInicio).TotalMinutes;

        if (minutosDiarios <= 0)
            return 0;

        double total = 0;
        for (var fecha = inicioMes.Date; fecha < finMes.Date; fecha = fecha.AddDays(1))
        {
            if (NormalizarTexto(NombreDia(fecha.DayOfWeek)) ==
                NormalizarTexto(horario.DiaSemana))
            {
                total += minutosDiarios;
            }
        }
        return total;
    }

    private static string NombreDia(DayOfWeek dia) => dia switch
    {
        DayOfWeek.Monday    => "Lunes",
        DayOfWeek.Tuesday   => "Martes",
        DayOfWeek.Wednesday => "Miércoles",
        DayOfWeek.Thursday  => "Jueves",
        DayOfWeek.Friday    => "Viernes",
        DayOfWeek.Saturday  => "Sábado",
        _                   => "Domingo"
    };

    /// <summary>
    /// Elimina diacríticos y convierte a minúsculas para comparar nombres de días
    /// sin importar si la BD guarda "Miercoles" o "Miércoles".
    /// </summary>
    private static string NormalizarTexto(string valor)
    {
        var normalizado = valor
            .Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark);
        return new string(normalizado.ToArray()).ToLowerInvariant();
    }

    /// <summary>Normaliza alias de estados al token canónico del sistema.</summary>
    private static string NormalizarEstado(string? estado) =>
        EstadoCitaHelper.Normalize(estado);
}
