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
                ])
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
            : Profiles["administrador"];
    }

    private static string ResolveIntent(RoleProfile profile, string message, string module)
    {
        if (string.IsNullOrWhiteSpace(message))
            return profile.Welcome;

        if (ContainsAny(message, "hola", "inicio", "saludo"))
            return profile.Welcome;

        if (ContainsAny(message, "cita", "agenda", "horario", "programar"))
            return $"En {module}, consulta el estado y los horarios desde la agenda disponible para tu rol. Las consultas rápidas visibles respetan tus permisos.";

        if (ContainsAny(message, "paciente", "historia", "odontograma", "tratamiento"))
            return "Para proteger la información clínica, abre el módulo autorizado desde el botón de acceso y verifica siempre el paciente antes de consultar o registrar datos.";

        if (ContainsAny(message, "factura", "pago", "cobro"))
            return "La facturación debe realizarse desde el módulo habilitado para tu rol. Revisa los datos antes de confirmar el pago y conserva el comprobante.";

        if (ContainsAny(message, "usuario", "rol", "permiso", "seguridad", "contraseña"))
            return profile.Key == "administrador"
                ? "Como Administrador puedes gestionar usuarios y roles desde Acceso y Seguridad. No compartas credenciales y revisa los permisos antes de guardar cambios."
                : "La gestión de usuarios y permisos está reservada al Administrador. Puedes cambiar tu propia contraseña desde tu perfil.";

        if (ContainsAny(message, "ayuda", "soporte", "error", "problema", "ticket"))
            return "Si una guía no resuelve tu caso, registra un ticket en Centro de Ayuda con el módulo afectado, pasos para reproducirlo y el mensaje de error. No adjuntes contraseñas ni datos clínicos innecesarios.";

        if (ContainsAny(message, "reporte", "analítica", "analitica"))
            return "Los reportes disponibles dependen de tu rol. Usa el acceso de reportes del panel lateral para consultar únicamente la información que tienes autorizada.";

        return $"No encontré una guía específica para esa consulta en {module}. Prueba una de las preguntas sugeridas o abre Centro de Ayuda para consultar guías y registrar soporte.";
    }

    private static string ModuleName(string? currentPath)
    {
        if (string.IsNullOrWhiteSpace(currentPath)) return "el sistema";
        if (currentPath.Contains("citas", StringComparison.OrdinalIgnoreCase)) return "Gestión de Citas";
        if (currentPath.Contains("historia", StringComparison.OrdinalIgnoreCase)) return "Historia Clínica";
        if (currentPath.Contains("pacientes", StringComparison.OrdinalIgnoreCase)) return "Gestión de Pacientes";
        if (currentPath.Contains("facturacion", StringComparison.OrdinalIgnoreCase)) return "Facturación y Pagos";
        if (currentPath.Contains("report", StringComparison.OrdinalIgnoreCase)) return "Reportes";
        return "el módulo actual";
    }

    private static bool ContainsAny(string value, params string[] terms) => terms.Any(value.Contains);

    private static string Normalize(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        return new string(normalized.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
    }
}