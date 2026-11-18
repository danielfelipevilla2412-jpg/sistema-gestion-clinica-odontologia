using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Helpers;

namespace SmileTrack_MVC.Services.GestionDeCitas;

public sealed class RecordatoriosCitasBackgroundService : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(5);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RecordatoriosCitasBackgroundService> _logger;

    public RecordatoriosCitasBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<RecordatoriosCitasBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcesarAsync(stoppingToken);
            try
            {
                await Task.Delay(Intervalo, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task ProcesarAsync(CancellationToken ct)
    {
        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var recordatorios = scope.ServiceProvider.GetRequiredService<IRecordatorioCitaService>();
            DateTime ahora = ColombiaClock.Now;
            DateTime limite = ahora.AddHours(24);

            var ids = await context.Citas
                .AsNoTracking()
                .Where(c => c.FechaHora >= ahora
                    && c.FechaHora <= limite
                    && !context.RecordatoriosCita.Any(r =>
                        r.IdCita == c.IdCita
                        && r.Estado == "enviado"
                        && r.ProgramadoPara.Date == ahora.Date))
                .Select(c => c.IdCita)
                .Take(100)
                .ToListAsync(ct);

            if (ids.Count > 0)
            {
                await recordatorios.EnviarAsync(ids, ct: ct);
                _logger.LogInformation(
                    "Proceso automático de recordatorios completado para {Cantidad} citas.",
                    ids.Count);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al procesar recordatorios automáticos.");
        }
    }
}
