/**
 * ============================================
 * SmileTrack — Módulo: Gestión de Citas
 * Componente: ICitasDashboardService (Interfaz)
 * ============================================
 * Archivo: Services/GestionDeCitas/ICitasDashboardService.cs
 *
 * PROPÓSITO Y JUSTIFICACIÓN:
 * Contrato de servicio para la agregación de métricas, indicadores operacionales (KPIs)
 * y resúmenes ejecutivos del panel principal de citas administrativas y de recepción.
 *
 * REGLAS DE NEGOCIO PRINCIPALES:
 * - Cálculo de métricas del día (Citas del día, Confirmadas, Pendientes, En Atención, Finalizadas).
 *
 * DEPENDENCIAS TÉCNICAS:
 * - CitasDashboardViewModel, CancellationToken
 * ============================================
 */

using SmileTrack_MVC.Models.ViewModels;

namespace SmileTrack_MVC.Services;

/// <summary>
/// Contrato para obtener los datos del dashboard de citas administrativo.
/// Separa la lógica de agregación del controlador MVC.
/// </summary>
public interface ICitasDashboardService
{
    /// <summary>
    /// Devuelve el ViewModel completo del dashboard calculado a partir de
    /// <paramref name="fechaReferencia"/> (normalmente DateTime.Today).
    /// </summary>
    Task<CitasDashboardViewModel> ObtenerDashboardAsync(
        DateTime          fechaReferencia,
        CancellationToken cancellationToken = default);
}
