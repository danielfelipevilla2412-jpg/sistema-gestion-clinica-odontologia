using SmileTrack_MVC.Models.ViewModels;

namespace SmileTrack_MVC.Services;

public interface IUsuarioAdminService
{
    Task<(bool Success, string Message, object? Data)> CrearAsync(
        CrearUsuarioAdminRequest request,
        int? creadoPor,
        CancellationToken ct = default);

    Task<(bool Success, string Message, object? Data)> ActualizarAsync(
        int idUsuario,
        ActualizarUsuarioAdminRequest request,
        int? usuarioOperador,
        CancellationToken ct = default);

    Task<(bool Success, string Message, object? Data)> CambiarEstadoAsync(
        int idUsuario,
        CambiarEstadoUsuarioRequest request,
        int? usuarioOperador,
        CancellationToken ct = default);
}
