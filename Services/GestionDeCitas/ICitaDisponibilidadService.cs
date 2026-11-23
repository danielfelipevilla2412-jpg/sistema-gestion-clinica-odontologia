using SmileTrack_MVC.Models.DTOs;
using SmileTrack_MVC.Models.Entities;

namespace SmileTrack_MVC.Services;

public interface ICitaDisponibilidadService
{
    Task<int> ObtenerDuracionCitaMinutosAsync(CancellationToken ct = default);

    Task<int> ObtenerDuracionServicioAsync(int idServicio, CancellationToken ct = default);

    Task<(bool EsValido, string? Mensaje)> ValidarHorarioClinicaAsync(
        DateTime fechaHora,
        int duracionMinutos = 60,
        CancellationToken ct = default);

    Task<(bool EsValida, string? Mensaje)> ValidarDisponibilidadProfesionalAsync(
        int idProfesional,
        int idServicio,
        DateTime fechaHora,
        int duracionMinutos,
        CancellationToken ct = default);

    Task<List<Profesional>> ObtenerProfesionalesElegiblesPorServicioAsync(
        int idServicio,
        CancellationToken ct = default);

    Task<List<TimeSpan>> ObtenerHorariosSolicitudPacienteAsync(
        int idPaciente,
        int idServicio,
        int? idProfesionalPreferido,
        DateTime fecha,
        CancellationToken ct = default);

    Task<List<TimeSpan>> ObtenerHorariosDisponiblesPacienteAsync(
        int idPaciente,
        int idServicio,
        int idProfesional,
        DateTime fecha,
        CancellationToken ct = default);

    Task<List<ProfesionalDisponibleDto>> ObtenerProfesionalesPorServicioAsync(
        int idServicio,
        CancellationToken ct = default);

    Task<List<ProfesionalDisponibleDto>> ObtenerProfesionalesDisponiblesAsync(
        DateTime fecha,
        TimeSpan horaInicio,
        int duracionMinutos = 60,
        int? idServicio = null,
        CancellationToken ct = default);
}
