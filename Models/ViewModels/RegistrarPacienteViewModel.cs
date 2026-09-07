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
//   Con este ViewModel esas listas vienen de la BD real. Los campos estáticos
//   (géneros, grupos sanguíneos, ciudades principales) se definen aquí como
//   constantes porque no tienen tabla propia en el esquema.
//
// ESTRATEGIA:
//   Los tipos de documento y los valores estáticos son listas fijas (no hay
//   tabla en BD). Profesionales, servicios y consultorios activos sí se leen
//   desde la BD para el selector de la primera cita (Paso 2 del formulario).
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
        ("PA",  "PA — Pasaporte"),
        ("RC",  "RC — Registro civil"),
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

    // ── Listas dinámicas desde BD (Paso 2: agendar primera cita) ─────────────

    /// <summary>Profesionales activos para el selector de primera cita.</summary>
    public List<ProfesionalOpcionDto> Profesionales { get; set; } = [];

    /// <summary>Servicios activos con precio para el selector.</summary>
    public List<ServicioOpcionDto> Servicios { get; set; } = [];

    /// <summary>Consultorios activos para el selector.</summary>
    public List<ConsultorioOpcionDto> Consultorios { get; set; } = [];
}

// ── DTOs liviano para los selectores del Paso 2 ──────────────────────────────

/// <summary>Opción de profesional para el select del formulario de registro.</summary>
public class ProfesionalOpcionDto
{
    public int    Id         { get; set; }
    /// <summary>Texto que se muestra en el select: "Dr(a). Nombre Apellido — Especialidad"</summary>
    public string Etiqueta   { get; set; } = string.Empty;
}

/// <summary>Opción de servicio para el select del formulario de registro.</summary>
public class ServicioOpcionDto
{
    public int     Id       { get; set; }
    /// <summary>Texto: "Nombre del servicio — $precio"</summary>
    public string  Etiqueta { get; set; } = string.Empty;
}

/// <summary>Opción de consultorio para el select del formulario de registro.</summary>
public class ConsultorioOpcionDto
{
    public int    Id       { get; set; }
    public string Etiqueta { get; set; } = string.Empty;
}
