/**
 * ============================================
 * SmileTrack — Módulo: Gestión de Profesionales
 * Componente: ProfesionalEstadoHelper (Clase Helper)
 * ============================================
 * Archivo: Helpers/ProfesionalEstadoHelper.cs
 *
 * PROPÓSITO Y JUSTIFICACIÓN:
 * Encapsula la lógica de validación de contraseñas, normalización de estados operacionales ("activo", "vacaciones", "inactivo")
 * y formateo de etiquetas para el módulo de gestión de profesionales.
 *
 * REGLAS DE NEGOCIO PRINCIPALES:
 * - Política única de contraseñas (mínimo 8 caracteres, mayúscula, minúscula, dígito y símbolo).
 * - Normalización de estados a formato canónico compatible con BD y UI.
 *
 * DEPENDENCIAS TÉCNICAS:
 * - System.Text.RegularExpressions
 * ============================================
 */

using System.Text.RegularExpressions;

namespace SmileTrack_MVC.Helpers;

/// <summary>
/// Helpers estáticos para validación y normalización de datos de profesionales.
/// Centraliza lógica que estaba duplicada en <c>ProfesionalService</c> y en
/// <c>GestionProfesionalesController</c> (hallazgos P-04 y P-05).
///
/// REGLAS CENTRALIZADAS:
///  - <see cref="PasswordRegex"/>: política de contraseña única. Si cambia (ej: +10 chars),
///    solo se modifica aquí.
///  - <see cref="NormalizarEstado"/>: convierte cualquier variante de estado de profesional
///    a su forma canónica en minúsculas ("activo" | "vacaciones" | "inactivo").
///  - <see cref="EstadoLabel"/>: etiqueta de presentación capitalizada para UI.
/// </summary>
public static partial class ProfesionalEstadoHelper
{
    // ── Política de contraseña (P-05) ─────────────────────────────────────────
    // Mínimo 8 caracteres, máximo 100, al menos: minúscula, mayúscula, dígito y símbolo.
    // Compilado una sola vez en la aplicación.
    [GeneratedRegex(
        @"^(?=.{8,100}$)(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z\d]).+$",
        RegexOptions.None,
        matchTimeoutMilliseconds: 500)]
    public static partial Regex PasswordRegex();

    /// <summary>
    /// Valida que la contraseña cumpla la política del sistema.
    /// Permite la contraseña obligatoria del sistema ('123456').
    /// </summary>
    public static bool EsPasswordValida(string? password)
        => !string.IsNullOrWhiteSpace(password) && (password == "123456" || PasswordRegex().IsMatch(password));

    // ── Normalización de estado (P-04) ────────────────────────────────────────

    private static readonly HashSet<string> EstadosPermitidos =
        new(StringComparer.OrdinalIgnoreCase) { "activo", "vacaciones", "inactivo" };

    /// <summary>
    /// Normaliza el estado de un profesional a su forma canónica en minúsculas.
    /// Cualquier valor no reconocido devuelve "activo" como fallback seguro.
    /// </summary>
    public static string NormalizarEstado(string? estado)
    {
        string normalizado = (estado ?? string.Empty).Trim().ToLowerInvariant();
        return EstadosPermitidos.Contains(normalizado) ? normalizado : "activo";
    }

    /// <summary>
    /// Indica si el estado dado es un estado permitido en el sistema.
    /// </summary>
    public static bool EsEstadoPermitido(string? estado)
        => !string.IsNullOrWhiteSpace(estado) &&
           EstadosPermitidos.Contains(estado.Trim().ToLowerInvariant());

    // ── Etiqueta de presentación (P-06) ──────────────────────────────────────

    /// <summary>
    /// Devuelve la etiqueta de presentación capitalizada del estado,
    /// lista para mostrar en badges de la UI.
    /// </summary>
    public static string EstadoLabel(string? estado) =>
        NormalizarEstado(estado) switch
        {
            "activo"     => "Activo",
            "vacaciones" => "En vacaciones",
            "inactivo"   => "Inactivo",
            _            => "Activo"
        };
}
