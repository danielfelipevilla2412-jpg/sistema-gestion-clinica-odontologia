using SmileTrack_MVC.Models.DTOs;
using SmileTrack_MVC.Models.Entities;

namespace SmileTrack_MVC.Services;

/// <summary>
/// Contrato base para la lógica de negocio de citas.
/// La implementación concreta debe encapsular validaciones, ownership y persistencia.
/// </summary>
public interface ICitaService
{
    Task<(List<Cita> Items, int TotalRecords)> ObtenerAsync(
        int page,
        int pageSize,
        string? role,
        int? idPaciente = null,
        int? idProfesional = null,
        int? idUsuario = null,
        CancellationToken ct = default);

    Task<Cita?> ObtenerPorIdAsync(
        int id,
        CancellationToken ct = default);

    Task<Cita> CrearAsync(
        CitaApiRequest request,
        CancellationToken ct = default);

    Task<Cita?> ActualizarAsync(
        int id,
        CitaApiUpdateDto request,
        CancellationToken ct = default);

    Task<Cita?> CambiarEstadoAsync(
        int id,
        string nuevoEstado,
        CancellationToken ct = default);

    Task<Cita?> ActualizarNotasAsync(
        int id,
        string notas,
        CancellationToken ct = default);

    Task<bool> CancelarAsync(
        int id,
        CancellationToken ct = default);

    /// <summary>
    /// Validación de conflicto de horario para UN solo recurso (profesional).
    /// Mantenido por compatibilidad; use <see cref="VerificarConflictoCompletoAsync"/>
    /// para validación integral de los tres recursos.
    /// </summary>
    Task<bool> HayConflictoHorarioAsync(
        int? idProfesional,
        DateTime fechaHora,
        int? idCitaExcluir = null,
        CancellationToken ct = default);

    /// <summary>
    /// Verifica conflictos de horario en los tres recursos críticos:
    /// profesional, paciente y consultorio.
    /// Devuelve el primer conflicto encontrado (prioridad: profesional → paciente → consultorio).
    /// </summary>
    /// <param name="idProfesional">ID del profesional asignado.</param>
    /// <param name="idPaciente">ID del paciente.</param>
    /// <param name="idConsultorio">ID del consultorio.</param>
    /// <param name="fechaHora">Fecha y hora de inicio de la cita.</param>
    /// <param name="duracionMinutos">Duración de la cita en minutos (defecto: 60).</param>
    /// <param name="idCitaExcluir">ID de la cita actual a excluir al verificar actualizaciones.</param>
    /// <param name="ct">Token de cancelación.</param>
    Task<ConflictoCitaResult> VerificarConflictoCompletoAsync(
        int? idProfesional,
        int idPaciente,
        int? idConsultorio,
        DateTime fechaHora,
        int duracionMinutos = 60,
        int? idCitaExcluir = null,
        CancellationToken ct = default);

    /// <summary>
    /// Obtiene la duración de cita configurada en <c>Configuracion_General</c>
    /// (clave <c>cita_duracion_minutos</c>). Si no existe o no es un número válido,
    /// devuelve el fallback de 60 minutos.
    /// </summary>
    Task<int> ObtenerDuracionCitaMinutosAsync(CancellationToken ct = default);

    /// <summary>
    /// Valida que <paramref name="fechaHora"/> esté dentro del horario de atención
    /// de la clínica, leyendo los valores de <c>Configuracion_General</c>:
    /// <list type="bullet">
    ///   <item><c>horario_apertura</c> — hora de inicio (HH:mm, defecto 07:00)</item>
    ///   <item><c>horario_cierre</c>   — hora de cierre (HH:mm, defecto 18:00)</item>
    ///   <item><c>dias_atencion</c>    — días separados por coma (1=Lun…7=Dom, defecto 1-6)</item>
    /// </list>
    /// </summary>
    /// <returns>
    /// <c>(true, null)</c> si la fecha es válida;<br/>
    /// <c>(false, mensaje)</c> con el motivo si está fuera del horario permitido.
    /// </returns>
    Task<(bool EsValido, string? Mensaje)> ValidarHorarioClinicaAsync(
        DateTime fechaHora,
        CancellationToken ct = default);
}
