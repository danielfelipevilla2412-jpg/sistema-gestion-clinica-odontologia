using SmileTrack_MVC.Models.Api.HistoriasClinicas;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Models.ViewModels;

namespace SmileTrack_MVC.Services;

public interface IHistoriaClinicaService
{
    // ── Lógica interna compartida entre MVC y API ───────────────────────────
    // (antes vivía como métodos privados en HistoriaClinicaController)

    Task<HistoriaClinica> CrearHistoriaClinicaAsync(int pacienteId, CancellationToken ct = default);

    Task<(bool Success, string Message, int? HistoriaId)> GuardarOdontogramaInternoAsync(
        int pacienteId,
        OdontogramaGuardarRequest request,
        int? idProfesional,
        CancellationToken ct = default);

    Task<(bool Success, string Message, NotaClinicaApiDto? Nota)> RegistrarNotaClinicaAsync(
        int pacienteId,
        NotaClinicaGuardarRequest request,
        int? idProfesional,
        string doctorNombre,
        CancellationToken ct = default);

    Task<(bool Success, string Message)> RegistrarControlPostoperatorioAsync(
        ControlPostoperatorioGuardarRequest request,
        CancellationToken ct = default);

    // ── Endpoints de la API REST (api/v1/historias-clinicas) ───────────────

    Task<HistoriaClinicaApiDto?> ObtenerHistoriaApiAsync(int pacienteId, CancellationToken ct = default);

    Task<HistoriaClinicaApiOperationResult> CrearHistoriaApiAsync(int pacienteId, CancellationToken ct = default);

    Task<List<NotaClinicaApiDto>> ObtenerNotasApiAsync(int pacienteId, CancellationToken ct = default);

    Task<HistoriaClinicaApiOperationResult> CrearNotaApiAsync(
        int pacienteId,
        NotaClinicaApiCreateDto dto,
        int? idProfesional,
        string doctorNombre,
        CancellationToken ct = default);

    Task<List<OdontogramaPiezaApiDto>> ObtenerOdontogramaApiAsync(int pacienteId, CancellationToken ct = default);

    Task<HistoriaClinicaApiOperationResult> GuardarOdontogramaApiAsync(
        int pacienteId,
        OdontogramaGuardarRequest request,
        int? idProfesional,
        CancellationToken ct = default);

    Task<HistoriaClinicaApiOperationResult> CambiarEstadoApiAsync(int idHistoria, bool activa, CancellationToken ct = default);

    // ── Ownership (rol Paciente) ────────────────────────────────────────────

    Task<bool> EsPropioPacienteAsync(int pacienteId, int idUsuario, CancellationToken ct = default);
}