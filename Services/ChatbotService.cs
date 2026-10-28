using System.Globalization;
using System.Text;
using SmileTrack_MVC.Models.Api;

namespace SmileTrack_MVC.Services;

public sealed class ChatbotService : IChatbotService
{
    private sealed record RoleProfile(
        string Key,
        string Label,
        string Welcome,
        string[] Suggestions,
        ChatbotAction[] Actions);

    private static readonly IReadOnlyDictionary<string, RoleProfile> Profiles =
        new Dictionary<string, RoleProfile>(StringComparer.OrdinalIgnoreCase)
        {
            ["administrador"] = new(
                "administrador", "Administrador",
                "Puedo orientarte sobre usuarios, roles, reportes, facturación y la operación general de SmileTrack.",
                ["👥 ¿Cómo gestiono usuarios?", "🛡️ ¿Cómo reviso los roles?", "📊 ¿Dónde consulto reportes?", "🧾 ¿Cómo reviso facturación?"],
                [
                    new() { Label = "Gestión de usuarios", Url = "/acceso-y-seguridad/st-adm-02-gestion-usuarios" },
                    new() { Label = "Gestión de roles", Url = "/acceso-y-seguridad/st-adm-03-gestion-roles" },
                    new() { Label = "Reportes", Url = "/reportes" },
                    new() { Label = "Facturación", Url = "/facturacion-y-pagos/st-adm-12-facturacion" }
                ]),
            ["recepcionista"] = new(
                "recepcionista", "Recepcionista",
                "Te ayudo con citas, pacientes, recepción, recordatorios, facturación y solicitudes de soporte.",
                ["📅 ¿Cómo creo una cita?", "👤 ¿Cómo registro un paciente?", "🔔 ¿Cómo envío recordatorios?", "🎫 ¿Cómo solicito soporte?"],
                [
                    new() { Label = "Gestionar citas", Url = "/gestion-de-citas/st-rec-03-gestion-citas" },
                    new() { Label = "Registrar paciente", Url = "/gestion-de-pacientes/st-rec-02-registrar-paciente-cita" },
                    new() { Label = "Recordatorios", Url = "/gestion-de-citas/st-rec-05-recordatorios" },
                    new() { Label = "Soporte y tickets", Url = "/centro-de-ayuda/soporte" }
                ]),
            ["auxiliar"] = new(
                "auxiliar", "Auxiliar",
                "Puedo orientarte sobre la agenda de apoyo, preparación de consulta, asistencia durante el procedimiento y estado del consultorio.",
                ["📋 ¿Cómo uso el panel auxiliar?", "🧪 ¿Cómo preparo una consulta?", "🦷 ¿Cómo registro la asistencia?", "🏥 ¿Dónde veo el consultorio?"],
                [
                    new() { Label = "Panel auxiliar", Url = "/gestion-de-citas/st-aux-01-panel-operativo/panel-operativo" },
                    new() { Label = "Preparación de consulta", Url = "/gestion-de-pacientes/st-aux-03-preparacion-consulta" },
                    new() { Label = "Asistencia en procedimiento", Url = "/gestion-de-citas/st-aux-06-asistencia-procedi" },
                    new() { Label = "Estado del consultorio", Url = "/gestion-de-citas/st-aux-09-estado-consultorio" }
                ]),
            ["profesional"] = new(
                "profesional", "Profesional",
                "Te ayudo con tu agenda, pacientes, historia clínica, odontograma, tratamientos y reportes clínicos.",
                ["📅 ¿Cómo consulto mi agenda?", "📋 ¿Dónde registro la historia clínica?", "🦷 ¿Cómo uso el odontograma?", "📈 ¿Cómo reviso un tratamiento?"],
                [
                    new() { Label = "Mi agenda", Url = "/gestion-de-citas/st-odo-02-agenda" },
                    new() { Label = "Historia clínica", Url = "/historia-clinica/st-odo-03-historial" },
                    new() { Label = "Odontograma", Url = "/historia-clinica/st-odo-04-odontograma" },
                    new() { Label = "Tratamientos", Url = "/historia-clinica/st-odo-07-seguimiento-tratamiento" }
                ]),
            ["paciente"] = new(
                "paciente", "Paciente",
                "Puedo orientarte sobre tus citas, notificaciones, historial, solicitudes y datos de perfil.",
                ["📅 ¿Cómo consulto mis citas?", "🔔 ¿Dónde veo notificaciones?", "📋 ¿Dónde consulto mi historial?", "📝 ¿Cómo creo una solicitud?"],
                [
                    new() { Label = "Mis citas", Url = "/gestion-de-citas/st-pac-01-mis-citas" },
                    new() { Label = "Notificaciones", Url = "/gestion-de-citas/st-pac-03-notificaciones" },
                    new() { Label = "Mi historial", Url = "/historia-clinica/st-pac-02-historial" },
                    new() { Label = "Nueva solicitud", Url = "/gestion-de-pqr/st-pac-04-nueva-pqr" }
                ]),
            ["usuario"] = new(
                "usuario", "Usuario",
                "Puedo orientarte sobre las funciones disponibles en tu cuenta. Cuéntame qué intentas hacer.",
                ["¿Cómo consulto mis opciones disponibles?", "¿Cómo solicito soporte?"],
                [])
        };

    public ChatbotResponse Respond(string? role, string? userName, string? currentPath, string message)
    {
        var profile = ResolveProfile(role);
        var normalizedMessage = Normalize(message);
        var context = ModuleName(currentPath);
        var response = ResolveIntent(profile, normalizedMessage, context);

        return new ChatbotResponse
        {
            Role = profile.Key,
            RoleLabel = profile.Label,
            Message = string.IsNullOrWhiteSpace(userName)
                ? response
                : $"{userName.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0]}, {response}",
            Suggestions = profile.Suggestions,
            Actions = profile.Actions
        };
    }

    private static RoleProfile ResolveProfile(string? role)
    {
        var normalizedRole = Normalize(role);
        return Profiles.TryGetValue(normalizedRole, out var profile)
            ? profile
            : Profiles["usuario"];
    }

    private static string ResolveIntent(RoleProfile profile, string message, string module)
    {
        if (string.IsNullOrWhiteSpace(message))
            return profile.Welcome;

        if (ContainsAny(message, "hola", "buenos dias", "buenas tardes", "buenas noches", "inicio", "saludo"))
            return profile.Welcome;

        if (ContainsAny(message, "cita", "agenda", "horario", "program", "reprogram", "disponibilidad", "turno"))
            return AppointmentGuidance(profile, module);

        if (ContainsAny(message, "registrar paciente", "crear paciente", "nuevo paciente", "datos del paciente", "paciente nuevo"))
            return profile.Key is "administrador" or "recepcionista"
                ? $"En {module}, abre el registro de pacientes, completa los datos solicitados y confirma que el documento y los datos de contacto estén correctos antes de guardar."
                : "El registro de pacientes está disponible para Recepción y Administración. Desde tu rol puedes consultar únicamente la información habilitada para ti.";

        if (ContainsAny(message, "historia", "odontograma", "tratamiento", "evolucion", "evolución", "diagnostico", "diagnóstico", "nota clinica", "nota clínica"))
            return ClinicalGuidance(profile, module);

        if (ContainsAny(message, "factura", "facturacion", "facturación", "pago", "cobro", "comprobante"))
            return profile.Key is "administrador" or "recepcionista"
                ? $"En {module}, abre la sección de facturación habilitada para tu rol, verifica paciente, concepto y valor, y confirma el pago solo después de validar los datos. Conserva el comprobante."
                : "La facturación está reservada a los roles autorizados de Administración y Recepción. Si necesitas consultar un cobro propio, solicita orientación a Recepción.";

        if (ContainsAny(message, "usuario", "rol", "permiso", "seguridad", "contrasena", "contraseña", "acceso", "iniciar sesion", "iniciar sesión"))
            return profile.Key == "administrador"
                ? $"En {module}, la gestión de usuarios y roles está en Acceso y Seguridad. Revisa los permisos antes de guardar cambios; nunca compartas credenciales."
                : "La gestión de usuarios y permisos está reservada al Administrador. Puedes cambiar tu propia contraseña desde tu perfil.";

        if (ContainsAny(message, "ayuda", "soporte", "error", "problema", "ticket", "no funciona", "no puedo", "no me deja", "no aparece", "fallo"))
            return $"Para resolverlo en {module}, indica qué intentabas hacer, qué ocurrió y el texto del error. Si persiste, registra un ticket en Centro de Ayuda. No incluyas contraseñas ni datos clínicos innecesarios.";

        if (ContainsAny(message, "reporte", "analitica", "analítica", "estadistica", "estadística"))
            return profile.Key is "administrador" or "profesional" or "recepcionista"
                ? $"En {module}, abre Reportes desde el menú lateral. Los indicadores y datos disponibles dependen de tu rol; selecciona el periodo y los filtros antes de consultar."
                : "Los reportes no están habilitados para tu rol. Consulta el panel operativo o solicita a Administración la información autorizada que necesitas.";

        if (ContainsAny(message, "perfil", "mis datos", "correo", "telefono", "teléfono", "actualizar datos"))
            return $"En {module}, abre tu perfil para revisar los datos personales que puedes modificar. Si un dato no es editable, solicita el cambio a Administración.";

        return $"Puedo orientarte sobre {module} y las tareas habilitadas para tu rol de {profile.Label}. Cuéntame qué intentas hacer o qué problema encontraste, por ejemplo: “¿cómo reprogramo una cita?” o “no puedo guardar el registro”.";
    }

    private static string AppointmentGuidance(RoleProfile profile, string module) => profile.Key switch
    {
        "paciente" => $"En {module}, entra a Mis citas para consultar fechas, horarios y estados. Para solicitar un cambio, usa las opciones disponibles en la cita o comunícate con Recepción.",
        "profesional" => $"En {module}, revisa Mi agenda para consultar tus citas y horarios. Usa los filtros de fecha para localizar una atención; la disponibilidad depende de la configuración de la clínica.",
        "auxiliar" => $"En {module}, revisa el panel operativo para ver las atenciones asignadas y su horario. La programación o reprogramación debe realizarla Recepción.",
        "recepcionista" => $"En {module}, abre Gestión de Citas, busca al paciente y verifica la disponibilidad antes de crear o reprogramar. Confirma la fecha, hora y profesional antes de guardar.",
        "administrador" => $"En {module}, abre Gestión de Citas para consultar la agenda y el estado de las atenciones. Verifica disponibilidad y permisos antes de modificar una cita.",
        _ => $"En {module}, consulta la agenda que está habilitada para tu cuenta. Para crear o cambiar una cita, comunícate con Recepción."
    };

    private static string ClinicalGuidance(RoleProfile profile, string module) => profile.Key switch
    {
        "profesional" => $"En {module}, busca y verifica al paciente antes de abrir su historia clínica. Registra la evolución, el odontograma o el tratamiento en la sección correspondiente y guarda los cambios al finalizar la atención.",
        "paciente" => $"En {module}, consulta Mi historial para ver la información clínica que la clínica ha habilitado para ti. Para corregir un dato o resolver una duda clínica, comunícate con tu profesional tratante.",
        "auxiliar" => $"En {module}, consulta la información necesaria para preparar la atención dentro de tus permisos. El diagnóstico y las notas clínicas deben ser registrados por el profesional tratante.",
        _ => "La información clínica solo debe consultarse desde el módulo autorizado y para una finalidad de atención. Verifica la identidad del paciente; el registro clínico corresponde al profesional tratante."
    };

    private static string ModuleName(string? currentPath)
    {
        if (string.IsNullOrWhiteSpace(currentPath)) return "el sistema";
        if (currentPath.Contains("citas", StringComparison.OrdinalIgnoreCase)) return "Gestión de Citas";
        if (currentPath.Contains("historia", StringComparison.OrdinalIgnoreCase)) return "Historia Clínica";
        if (currentPath.Contains("pacientes", StringComparison.OrdinalIgnoreCase)) return "Gestión de Pacientes";
        if (currentPath.Contains("facturacion", StringComparison.OrdinalIgnoreCase)) return "Facturación y Pagos";
        if (currentPath.Contains("report", StringComparison.OrdinalIgnoreCase)) return "Reportes";
        if (currentPath.Contains("ayuda", StringComparison.OrdinalIgnoreCase) || currentPath.Contains("pqr", StringComparison.OrdinalIgnoreCase)) return "Centro de Ayuda";
        if (currentPath.Contains("servicios", StringComparison.OrdinalIgnoreCase)) return "Servicios y Recursos";
        if (currentPath.Contains("perfil", StringComparison.OrdinalIgnoreCase)) return "Perfil";
        return "el módulo actual";
    }

    private static bool ContainsAny(string value, params string[] terms) => terms.Any(value.Contains);

    private static string Normalize(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        return new string(normalized.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
    }
}