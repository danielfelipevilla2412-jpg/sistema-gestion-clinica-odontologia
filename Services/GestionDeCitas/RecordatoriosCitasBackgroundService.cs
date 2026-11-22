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
            try { await Task.Delay(Intervalo, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    private async Task ProcesarAsync(CancellationToken ct)
    {
        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            var recordatorios = scope.ServiceProvider.GetRequiredService<IRecordatorioCitaService>();
            var resultado = await recordatorios.ProcesarPendientesAsync(ct);
            if (resultado.Enviados > 0 || resultado.Fallidos > 0)
                _logger.LogInformation("Recordatorios procesados: enviados={Enviados}, fallidos={Fallidos}.",
                    resultado.Enviados, resultado.Fallidos);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex) { _logger.LogError(ex, "Error al procesar recordatorios automáticos."); }
    }
}
