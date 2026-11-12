using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Helpers;
using SmileTrack_MVC.Services;

namespace SmileTrack_MVC.Services.GestionDeCitas;

public sealed class CitasEnCursoBackgroundService : BackgroundService
{
    private static readonly TimeSpan IntervaloRevision = TimeSpan.FromSeconds(30);
    private static readonly string[] EstadosIniciables =
    [
        "agendada", "agendado", "programada", "programado",
        "confirmada", "confirmado", "en sala de espera", "en_sala_de_espera"
    ];

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CitasEnCursoBackgroundService> _logger;

    public CitasEnCursoBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<CitasEnCursoBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await IniciarCitasProgramadasAsync(stoppingToken);

            try
            {
                await Task.Delay(IntervaloRevision, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task IniciarCitasProgramadasAsync(CancellationToken ct)
    {
        try
        {
            DateTime ahora = ColombiaClock.Now;
            DateTime inicioDia = ahora.Date;
            using IServiceScope scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var citaService = scope.ServiceProvider.GetRequiredService<ICitaService>();

            int[] citasVencidas = await db.Citas
                .AsNoTracking()
                .Where(c => c.FechaHora >= inicioDia &&
                            c.FechaHora <= ahora &&
                            EstadosIniciables.Contains(c.Estado.Trim().ToLower()))
                .Select(c => c.IdCita)
                .ToArrayAsync(ct);

            foreach (int idCita in citasVencidas)
            {
                try
                {
                    await citaService.CambiarEstadoAsync(idCita, "en_proceso", ct: ct);
                    _logger.LogInformation(
                        "La cita {IdCita} pasó automáticamente a En consulta al llegar su hora programada.",
                        idCita);
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogWarning(
                        ex,
                        "No se pudo iniciar automáticamente la cita {IdCita}; puede haber cambiado de estado o faltar un estado en el catálogo.",
                        idCita);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al iniciar las citas que alcanzaron su hora programada.");
        }
    }
}
