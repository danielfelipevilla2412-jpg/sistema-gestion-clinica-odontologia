/**
 * ============================================
 * SmileTrack — Módulo: Gestión de Citas
 * Componente: IAgendaService (Interfaz)
 * ============================================
 * Archivo: Services/GestionDeCitas/IAgendaService.cs
 *
 * PROPÓSITO Y JUSTIFICACIÓN:
 * Interfaz que define las operaciones para la consulta y estructuración de la agenda
 * semanal/diaria clínica, proveyendo la información de citas proyectada por consultorios u odontólogos.
 *
 * REGLAS DE NEGOCIO PRINCIPALES:
 * - Cálculo de rangos semanales y consolidación de slots de tiempo por profesional/consultorio.
 *
 * DEPENDENCIAS TÉCNICAS:
 * - AgendaViewModel, CancellationToken
 * ============================================
 */

using SmileTrack_MVC.Models.ViewModels;

namespace SmileTrack_MVC.Services;

public interface IAgendaService
{
    Task<AgendaViewModel> ObtenerAgendaAsync(
        DateTime? weekStart,
        int? professionalId,
        int? officeId,
        CancellationToken cancellationToken = default);
}
