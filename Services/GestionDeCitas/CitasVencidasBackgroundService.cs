/**
 * ============================================
 * SmileTrack — Módulo: Gestión de Citas
 * Componente: CitasVencidasBackgroundService
 * ============================================
 * Archivo: Services/GestionDeCitas/CitasVencidasBackgroundService.cs
 *
 * PROPÓSITO Y JUSTIFICACIÓN:
 * Servicio en segundo plano que cierra automáticamente las citas que ya vencieron y se quedaron
 * en un estado no final (solicitada / pendiente / agendada / confirmada / en espera). Sin este
 * cierre, la agenda y los reportes siguen mostrando indefinidamente citas "fantasma" de días
 * anteriores que nunca se atendieron ni se cancelaron.
 *
 * REGLAS DE NEGOCIO PRINCIPALES:
 * - Se ejecuta todos los días a las 23:59 (hora local del servidor).
 * - Al arrancar la aplicación ejecuta una puesta al día que cierra las citas de días anteriores.
 * - Las citas de la jornada en curso solo se cierran en la pasada nocturna, nunca durante el día,
 *   para no marcar como inasistencia a un paciente que todavía está en sala de espera.
 * - El estado destino es "No asistio" (estado ya existente en el catálogo dbo.Estado_Cita).
 *
 * DEPENDENCIAS TÉCNICAS:
 * - IServiceScopeFactory (AppDbContext es scoped), ILogger
 * ============================================
 */

using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;

namespace SmileTrack_MVC.Services.GestionDeCitas;

public sealed class CitasVencidasBackgroundService : BackgroundService
{
    /// <summary>Hora local del servidor en la que se cierra la jornada.</summary>
    private static readonly TimeSpan HoraCierreDiario = new(23, 59, 0);

    private const string EstadoNoAsistio = "No asistio";

    /// <summary>
    /// Estados "en espera / pendiente": la cita continúa abierta y su horario ya pasó.
    /// Cualquier otro estado se considera final y no se toca.
    /// </summary>
    private static readonly string[] EstadosPendientes =
    [
        "solicitada", "solicitado", "pendiente",
        "agendada", "agendado", "programada", "programado",
        "confirmada", "confirmado",
        "en espera", "en_sala_de_espera"
    ];

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CitasVencidasBackgroundService> _logger;

    public CitasVencidasBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<CitasVencidasBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Puesta al día: cierra lo que quedó abierto en días anteriores. El corte es el inicio del
        // día actual para no tocar las citas de la jornada en curso.
        await CerrarCitasVencidasAsync(DateTime.Today, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            TimeSpan espera = CalcularEsperaHastaProximoCierre();

            try
            {
                await Task.Delay(espera, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await CerrarCitasVencidasAsync(DateTime.Now, stoppingToken);
        }
    }

    private static TimeSpan CalcularEsperaHastaProximoCierre()
    {
        DateTime ahora = DateTime.Now;
        DateTime proximo = ahora.Date.Add(HoraCierreDiario);

        if (proximo <= ahora)
        {
            proximo = proximo.AddDays(1);
        }

        return proximo - ahora;
    }

    /// <summary>
    /// Marca como "No asistio" todas las citas anteriores a <paramref name="corte"/> que sigan en
    /// un estado no final.
    /// </summary>
    private async Task CerrarCitasVencidasAsync(DateTime corte, CancellationToken ct)
    {
        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            int? idNoAsistio = await db.EstadosCita
                .AsNoTracking()
                .Where(e => e.NombreEstado.ToLower() == "no asistio")
                .Select(e => (int?)e.IdEstado)
                .FirstOrDefaultAsync(ct);

            string[] estadosPendientes = EstadosPendientes;

            var vencidas = db.Citas
                .Where(c => c.FechaHora < corte)
                .Where(c => c.Estado != null && estadosPendientes.Contains(c.Estado!.ToLower()));

            int actualizadas = idNoAsistio.HasValue
                ? await vencidas.ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(c => c.Estado, EstadoNoAsistio)
                        .SetProperty(c => c.IdEstado, idNoAsistio.Value),
                    ct)
                : await vencidas.ExecuteUpdateAsync(
                    setters => setters.SetProperty(c => c.Estado, EstadoNoAsistio),
                    ct);

            if (actualizadas > 0)
            {
                _logger.LogInformation(
                    "Cierre automático de citas vencidas: {Total} cita(s) pasaron a '{Estado}' (corte {Corte:yyyy-MM-dd HH:mm}).",
                    actualizadas,
                    EstadoNoAsistio,
                    corte);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Apagado normal de la aplicación.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No fue posible cerrar las citas vencidas en la pasada programada.");
        }
    }
}
