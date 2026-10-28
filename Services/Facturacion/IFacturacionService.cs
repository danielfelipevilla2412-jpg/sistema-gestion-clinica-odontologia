using SmileTrack_MVC.Models.Api.Facturacion;

namespace SmileTrack_MVC.Services.Facturacion;

public interface IFacturacionService
{
    Task<IReadOnlyList<FacturaListItemDto>> ListarAsync(string? buscar, string? estado, DateTime? desde, DateTime? hasta, int? idCita, int? idProfesional, CancellationToken ct);
    Task<FacturaDto?> ObtenerAsync(int id, CancellationToken ct);
    Task<CitaFacturacionDto?> ObtenerContextoCitaAsync(int idCita, CancellationToken ct);
    Task<FacturaDto> CrearAsync(CrearFacturaRequest request, int idUsuario, CancellationToken ct);
<<<<<<< HEAD
<<<<<<< HEAD
=======
    Task<FacturaDto> CrearConPagoAsync(CrearFacturaConPagoRequest request, int idUsuario, CancellationToken ct);
>>>>>>> 83a1479ed269637a52ff5b05687963f999b92b23
    Task<FacturaDto> CrearDesdeCitaAsync(int idCita, int idUsuario, CancellationToken ct);
    Task<(int Enviados, int Fallidos)> EnviarRecordatoriosFacturasVencidasAsync(int idUsuario, CancellationToken ct);
    Task<FacturaDto?> ActualizarAsync(int id, ActualizarFacturaRequest request, int idUsuario, CancellationToken ct);
    Task<bool> EliminarAsync(int id, int idUsuario, CancellationToken ct);
    Task<IReadOnlyList<PagoDto>?> ListarPagosAsync(int id, CancellationToken ct);
    Task<FacturaDto?> RegistrarPagoAsync(int id, RegistrarPagoRequest request, int idUsuario, CancellationToken ct);
    Task<FacturaDto?> AnularAsync(int id, AnularFacturaRequest? request, int idUsuario, CancellationToken ct);
<<<<<<< HEAD
=======
    Task<FacturaDto> CrearConPagoAsync(CrearFacturaConPagoRequest request, int idUsuario, CancellationToken ct);
    Task<FacturaDto?> ActualizarAsync(int id, ActualizarFacturaRequest request, CancellationToken ct);
    Task<bool> EliminarAsync(int id, CancellationToken ct);
    Task<FacturaDto?> RegistrarPagoAsync(int id, RegistrarPagoRequest request, int idUsuario, CancellationToken ct);
    Task<FacturaDto?> AnularAsync(int id, AnularFacturaRequest? request, CancellationToken ct);
>>>>>>> origin/facturacion_ayuda
=======
>>>>>>> 83a1479ed269637a52ff5b05687963f999b92b23
}
