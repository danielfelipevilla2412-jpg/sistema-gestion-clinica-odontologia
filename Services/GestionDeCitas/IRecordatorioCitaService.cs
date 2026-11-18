namespace SmileTrack_MVC.Services.GestionDeCitas;

public interface IRecordatorioCitaService
{
    Task<(int Enviados, int Fallidos, List<int> IdsEnviados)> EnviarAsync(
        IReadOnlyCollection<int> idsCitas,
        string? mensajePersonalizado = null,
        CancellationToken ct = default);
}
