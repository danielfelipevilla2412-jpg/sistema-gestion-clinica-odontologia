using SmileTrack_MVC.Models.Api.Administracion;

namespace SmileTrack_MVC.Services;

public interface IUsuariosApiService
{
    Task<UsuariosApiResult> ObtenerAsync(string? search, string? estado, int? idRol, int page, int pageSize, CancellationToken ct = default);
    Task<UsuarioApiDto?> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task<UsuariosApiResult> CrearAsync(UsuarioApiCreateDto dto, int? operadorId, CancellationToken ct = default);
    Task<UsuariosApiResult> ActualizarAsync(int id, UsuarioApiUpdateDto dto, int? operadorId, CancellationToken ct = default);
    Task<UsuariosApiResult> CambiarEstadoAsync(int id, string estado, int? operadorId, CancellationToken ct = default);
    Task<UsuariosApiResult> RestablecerContrasenaAsync(int id, string contrasenaTemporal, int? operadorId, CancellationToken ct = default);
    Task<UsuariosApiResult> DesbloquearAsync(int id, int? operadorId, CancellationToken ct = default);
    Task<List<RolDisponibleApiDto>> ObtenerRolesAsync(CancellationToken ct = default);
}
