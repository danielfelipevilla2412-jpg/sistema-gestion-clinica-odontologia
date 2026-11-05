// ─────────────────────────────────────────────────────────────────────────────
// Yeray (2025) - RegistrarPacienteViewModel
//
// PARA QUÉ SIRVE:
//   La acción Strec02RegistrarPaciente devolvía View() sin ningún ViewModel.
//   La vista tenía todas sus listas hardcodeadas:
//     - Tipos de documento: CC, TI, CE (fijos en el HTML)
//     - Profesionales:      "Dr. Carlos Méndez", etc. (inventados)
//     - Servicios:          "Consulta General - $50.000" (inventados)
//     - Consultorios:       "Consultorio - Piso 1" (inventados)
//
//   Los catálogos del formulario son estáticos porque no tienen tabla propia
//   en el esquema.
// ─────────────────────────────────────────────────────────────────────────────

namespace SmileTrack_MVC.Models.ViewModels;

public class RegistrarPacienteViewModel
{
    // ── Listas estáticas (no tienen tabla propia en el esquema) ───────────────

    /// <summary>Tipos de documento válidos en Colombia.</summary>
    public static readonly List<(string Valor, string Etiqueta)> TiposDocumento =
    [
        ("CC",  "CC — Cédula de ciudadanía"),
        ("TI",  "TI — Tarjeta de identidad"),
        ("CE",  "CE — Cédula de extranjería"),
        ("PAS", "PAS — Pasaporte"),
        ("NIT", "NIT — Número de identificación tributaria")
    ];

    /// <summary>Géneros válidos (alineados con el CHECK M/F/O de la tabla Paciente).</summary>
    public static readonly List<(string Valor, string Etiqueta)> Generos =
    [
        ("M", "Masculino"),
        ("F", "Femenino"),
        ("O", "Otro / Prefiero no decir")
    ];

    /// <summary>Grupos sanguíneos estándar ABO + Rh.</summary>
    public static readonly List<string> GruposSanguineos =
    [
        "O+", "O-", "A+", "A-", "B+", "B-", "AB+", "AB-"
    ];

    /// <summary>
    /// Ciudades principales de Colombia para el select.
    /// Se cargan estáticamente porque no hay tabla de ciudades en el esquema.
    /// </summary>
    public static readonly List<string> Ciudades =
    [
        "Bogotá D.C.", "Medellín", "Cali", "Barranquilla", "Cartagena",
        "Cúcuta", "Bucaramanga", "Pereira", "Santa Marta", "Ibagué",
        "Manizales", "Neiva", "Villavicencio", "Armenia", "Valledupar",
        "Montería", "Pasto", "Sincelejo", "Popayán", "Tunja"
    ];

}
