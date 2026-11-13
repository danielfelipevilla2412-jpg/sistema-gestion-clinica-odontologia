namespace SmileTrack_MVC.Services.Email;

public sealed record FacturaRecordatorioEmail(
    string NumeroFactura,
    DateTime FechaFactura,
    decimal SaldoPendiente);
