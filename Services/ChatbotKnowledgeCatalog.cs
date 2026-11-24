namespace SmileTrack_MVC.Services;

internal sealed record ChatbotKnowledgeEntry(
    string Id,
    string Title,
    string Module,
    string Path,
    string Summary,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Keywords,
    IReadOnlyList<string> Questions,
    IReadOnlyList<string> Steps);

internal static class ChatbotKnowledgeCatalog
{
    private static readonly string[] AllRoles =
        ["Administrador", "Recepcionista", "Profesional", "Auxiliar", "Paciente"];

    private static readonly IReadOnlyList<ChatbotKnowledgeEntry> Entries =
    [
        Page("home-admin", "Inicio", "Inicio", "/", ["Administrador", "Recepcionista", "Auxiliar", "Paciente"], "Panel de inicio.", "inicio", "portada", "home"),
        Page("home-professional", "Mi Dashboard", "Inicio", "/gestion-de-profesionales/st-odo-01-dashboard", ["Profesional"], "Panel del profesional.", "dashboard", "resumen", "inicio"),
        Page("reports-admin", "Panel de Analítica", "Reportes y Analítica", "/reportes", ["Administrador"], "Panel de analítica.", "reportes", "indicadores", "estadísticas", "analítica"),
        Page("users-admin", "Gestión de Usuarios", "Acceso y Seguridad", "/acceso-y-seguridad/st-adm-02-gestion-usuarios", ["Administrador"], "Vista de gestión de usuarios.", "usuarios", "cuentas", "crear usuario"),
        Page("roles-admin", "Gestión de Roles", "Acceso y Seguridad", "/acceso-y-seguridad/st-adm-03-gestion-roles", ["Administrador"], "Vista de gestión de roles.", "roles", "permisos", "autorización"),
        Page("audit-admin", "Bitácora del Sistema", "Acceso y Seguridad", "/acceso-y-seguridad/st-adm-15-bitacora", ["Administrador"], "Vista de bitácora del sistema.", "bitácora", "auditoría", "registro de actividad"),
        Page("password", "Cambiar Contraseña", "Acceso y Seguridad", "/acceso-y-seguridad/cambiar-contrasena", AllRoles, "Vista para cambiar la contraseña de la propia cuenta.", "contraseña", "clave", "password", "acceso"),
        Page("patients-admin", "Gestión de Pacientes", "Gestión de Pacientes", "/gestion-de-pacientes/st-adm-05-gestion-pacientes", ["Administrador"], "Vista de gestión de pacientes.", "pacientes", "buscar paciente", "actualizar paciente"),
        Page("patients-reception", "Gestión de Pacientes", "Gestión de Pacientes", "/gestion-de-pacientes/st-rec-pacientes", ["Recepcionista"], "Vista de gestión de pacientes para Recepción.", "pacientes", "buscar paciente", "actualizar paciente"),
        Page("patient-registration", "Registrar Paciente + Cita", "Gestión de Pacientes", "/gestion-de-pacientes/st-rec-02-registrar-paciente", ["Recepcionista"], "Vista para registrar un paciente y continuar con la gestión de su cita.", "registrar paciente", "nuevo paciente", "paciente nuevo"),
        Page("patients-professional", "Mis Pacientes", "Historia Clínica", "/historia-clinica/st-odo-06-pacientes", ["Profesional"], "Vista de pacientes del profesional.", "pacientes", "mis pacientes"),
        Page("patients-assistant", "Historial del Paciente", "Gestión de Pacientes", "/gestion-de-pacientes/st-aux-05-historial-parcial", ["Auxiliar"], "Vista de historial parcial del paciente.", "pacientes", "historial parcial"),
        Page("clinical-admin", "Historial Clínico", "Historia Clínica", "/historia-clinica/st-adm-historial", ["Administrador"], "Vista de historial clínico habilitada para Administración.", "historia clínica", "historial"),
        Page("clinical-reception", "Historial Clínico", "Historia Clínica", "/historia-clinica/st-rec-historial", ["Recepcionista"], "Vista de historial clínico habilitada para Recepción.", "historia clínica", "historial"),
        Page("clinical-professional", "Historia Clínica", "Historia Clínica", "/historia-clinica/st-odo-03-historial", ["Profesional"], "Vista de historia clínica del profesional.", "historia clínica", "expediente"),
        Page("clinical-patient", "Mi historial", "Historia Clínica", "/historia-clinica/st-pac-02-historial", ["Paciente"], "Vista del historial clínico disponible para el paciente.", "mi historial", "historia clínica"),
        Page("odontogram", "Odontograma", "Historia Clínica", "/historia-clinica/st-odo-04-odontograma", ["Profesional"], "Vista de odontograma.", "odontograma", "dientes", "pieza dental"),
        Page("treatment-followup", "Tratamientos", "Historia Clínica", "/historia-clinica/st-odo-07-seguimiento-tratamiento", ["Profesional"], "Vista de seguimiento de tratamientos.", "tratamientos", "seguimiento", "sesiones"),
        Page("professionals-admin", "Gestión de Profesionales", "Gestión de Profesionales", "/gestion-de-profesionales/st-adm-07-gestion-profesionales", ["Administrador"], "Vista de gestión de profesionales.", "profesionales", "odontólogos", "especialidades", "horarios"),
        Page("professional-reports-admin", "Reportes Clínicos", "Gestión de Profesionales", "/gestion-de-profesionales/st-adm-14-reportes-clinicos", ["Administrador"], "Vista de reportes clínicos.", "reportes clínicos", "informes"),
        Page("professional-reports", "Reportes Clínicos", "Gestión de Profesionales", "/gestion-de-profesionales/st-adm-14-reportes-clinicos", ["Profesional"], "Vista de reportes clínicos habilitada para el profesional.", "reportes clínicos", "informes"),
        Page("professional-dashboard", "Mi Dashboard", "Gestión de Profesionales", "/gestion-de-profesionales/st-odo-01-dashboard", ["Profesional"], "Panel de actividad del profesional.", "dashboard", "resumen", "mis estadísticas"),
        Page("professional-services", "Mis servicios", "Servicios y Recursos", "/servicios-y-recursos/st-odo-05-servicios", ["Profesional"], "Vista de servicios del profesional.", "servicios", "mis servicios"),
        Page("professional-profile", "Mi Perfil", "Perfil", "/gestion-de-profesionales/st-odo-09-perfil-profesional", ["Profesional"], "Perfil profesional.", "perfil", "mis datos"),
        Page("appointments-admin-dashboard", "Dashboard General", "Gestión de Citas", "/gestion-de-citas/st-adm-01-dashboard", ["Administrador"], "Panel general de gestión de citas.", "citas", "dashboard", "resumen de citas"),
        Page("appointments-admin-calendar", "Agenda General", "Gestión de Citas", "/gestion-de-citas/st-adm-08-agenda", ["Administrador"], "Agenda general de citas.", "agenda", "calendario", "horarios"),
        Page("appointments-admin", "Gestión Integral de Citas", "Gestión de Citas", "/gestion-de-citas/st-adm-09-citas", ["Administrador"], "Vista de gestión integral de citas.", "citas", "gestionar citas"),
        Page("appointments-reception-dashboard", "Panel de Recepción", "Gestión de Citas", "/gestion-de-citas/st-rec-01-dashboard", ["Recepcionista"], "Panel de Recepción.", "dashboard", "recepción"),
        Page("appointments-reception", "Gestión de Citas", "Gestión de Citas", "/gestion-de-citas/st-rec-03-gestion-citas", ["Recepcionista"], "Vista de gestión de citas para Recepción.", "citas", "nueva cita", "reprogramar", "cancelar"),
        Page("appointments-reminders", "Recordatorios", "Gestión de Citas", "/gestion-de-citas/st-rec-05-recordatorios", ["Recepcionista"], "Vista de recordatorios.", "recordatorios", "notificaciones", "correo"),
        Page("appointments-professional", "Mi Agenda", "Gestión de Citas", "/gestion-de-citas/st-odo-02-agenda", ["Profesional"], "Revisa Mi agenda para consultar citas y horarios. La disponibilidad depende de la configuración de la clínica.", "agenda", "calendario", "horarios"),
        Page("appointments-assistant-dashboard", "Panel Auxiliar", "Gestión de Citas", "/gestion-de-citas/st-aux-01-panel-operativo/panel-operativo", ["Auxiliar"], "Consulta en el panel operativo las atenciones asignadas y sus horarios. La programación o reprogramación debe realizarla Recepción.", "panel operativo", "dashboard", "atenciones asignadas"),
        Page("appointments-assistant-agenda", "Agenda de Apoyo", "Gestión de Citas", "/gestion-de-citas/st-aux-02-agenda-apoyo", ["Auxiliar"], "Agenda de apoyo del auxiliar.", "agenda", "apoyo", "horarios"),
        Page("appointments-assistant-preparation", "Preparación de Consulta", "Gestión de Citas", "/gestion-de-pacientes/st-aux-03-preparacion-consulta", ["Auxiliar"], "Vista de preparación de consulta.", "preparar consulta", "preparación"),
        Page("appointments-completed", "Citas Finalizadas", "Gestión de Citas", "/gestion-de-citas/st-aux-10-citas-finalizadas", ["Auxiliar", "Recepcionista"], "Vista de citas finalizadas.", "citas finalizadas", "atendidas"),
        Page("appointments-procedure", "Asistencia en Procedimiento", "Gestión de Citas", "/gestion-de-citas/st-aux-06-asistencia-procedi", ["Auxiliar"], "Vista de asistencia durante el procedimiento.", "procedimiento", "asistencia"),
        Page("office-status", "Estado del Consultorio", "Gestión de Citas", "/gestion-de-citas/st-aux-09-estado-consultorio", ["Auxiliar"], "Vista de estado del consultorio.", "consultorio", "sala", "estado"),
        Page("appointments-patient", "Mis citas", "Gestión de Citas", "/gestion-de-citas/st-pac-01-mis-citas", ["Paciente"], "Consulta tus fechas, horarios y estados. Para solicitar un cambio, usa las opciones disponibles en la cita o comunícate con Recepción.", "mis citas", "agendar", "cancelar", "estado de cita", "cambiar cita", "reprogramar"),
        Page("patient-notifications", "Notificaciones", "Gestión de Citas", "/gestion-de-citas/st-pac-03-notificaciones", ["Paciente"], "Vista de notificaciones del paciente.", "notificaciones", "avisos"),
        Page("billing-admin", "Gestión de Facturación", "Facturación y Pagos", "/facturacion-y-pagos/st-adm-12-facturacion", ["Administrador"], "Vista de gestión de facturación.", "factura", "facturación", "pagos", "cobro"),
        Page("billing-reception", "Generar Factura y Pago", "Facturación y Pagos", "/facturacion-y-pagos/st-rec-04-generar-factura", ["Recepcionista"], "Vista para generar facturas y pagos.", "factura", "facturación", "pagos"),
        Page("services-admin", "Catálogo de Servicios", "Servicios y Recursos", "/servicios-y-recursos/st-adm-10-servicios", ["Administrador"], "Catálogo de servicios.", "servicios", "catálogo"),
        Page("settings-admin", "Configuración General", "Servicios y Recursos", "/servicios-y-recursos/st-adm-16-configuracion-general", ["Administrador"], "Configuración general del sistema.", "configuración", "ajustes"),
        Page("reports-reception", "Reportes Operativos", "Reportes", "/reportes/vista-recepcion", ["Recepcionista"], "Vista de reportes operativos.", "reportes", "estadísticas", "operación"),
        Page("reports-professional", "Reportes y Analítica", "Reportes", "/reportes/vista-prof", ["Profesional"], "Vista de reportes del profesional.", "reportes", "estadísticas", "analítica"),
        Page("pqr-admin", "Gestión de PQR", "Gestión de PQR", "/gestion-de-pqr/st-adm-17-gestion-pqr", ["Administrador"], "Vista administrativa de PQR.", "pqr", "petición", "queja", "reclamo"),
        Page("pqr-new", "Nueva Solicitud", "Gestión de PQR", "/gestion-de-pqr/st-pac-04-nueva-pqr", ["Administrador", "Recepcionista", "Profesional", "Auxiliar", "Paciente"], "Vista para crear una solicitud PQR.", "pqr", "solicitud", "petición", "queja", "reclamo"),
        Page("profile-reception", "Perfil del Recepcionista", "Perfil", "/perfiles/st-rec-06-perfil-recepcionista", ["Recepcionista"], "Perfil de Recepción.", "perfil", "mis datos"),
        Page("profile-assistant", "Mi Perfil", "Perfil", "/perfiles/st-aux-11-perfil-auxiliar", ["Auxiliar"], "Perfil del auxiliar.", "perfil", "mis datos"),
        Page("profile-patient", "Mi perfil", "Perfil", "/perfiles/st-pac-perfil-paciente", ["Paciente"], "Perfil del paciente.", "perfil", "mis datos"),
        Page("help-index", "Guías y Tutoriales", "Centro de Ayuda", "/centro-de-ayuda/guias-tutoriales", AllRoles, "Catálogo actual de guías y accesos de ayuda.", "guías", "tutorial", "artículo", "ayuda"),
        Page("help-appointment", "Cómo programar una cita", "Centro de Ayuda", "/centro-de-ayuda/como-programar-cita", AllRoles, "Guía paso a paso para programar una cita.", "cita", "programar", "agendar", "nueva cita"),
        Page("help-support", "Soporte y Tickets", "Centro de Ayuda", "/centro-de-ayuda/soporte", AllRoles, "Formulario MVC de soporte con campos de asunto, tipo, módulo, severidad, descripción y adjunto opcional. El servicio admite archivos de hasta 5 MB.", "soporte", "ticket", "problema", "incidente", "adjunto", "captura"),
        new(
            "appointment-guide-steps",
            "Pasos para programar una cita",
            "Gestión de Citas",
            "/centro-de-ayuda/como-programar-cita",
            "La guía existente indica que se debe abrir la agenda, iniciar Nueva Cita, seleccionar al paciente, asignar profesional, servicio, consultorio y horario, revisar los datos y guardar.",
            ["Administrador", "Recepcionista"],
            ["cita", "programar", "agendar", "reservar", "crear", "nueva cita", "pasos"],
            ["¿Cómo programo una cita?", "¿Cómo agendo una cita?", "¿Cómo creo una cita nueva?"],
            [
                "Abre la agenda correspondiente a tu rol.",
                "Selecciona Nueva Cita.",
                "Busca o selecciona al paciente.",
                "Asigna profesional, servicio, consultorio y horario.",
                "Revisa los datos y guarda."
            ]),
        new(
            "appointment-patient-change",
            "Cambiar o solicitar reprogramación de una cita",
            "Gestión de Citas",
            "/gestion-de-citas/st-pac-01-mis-citas",
            "Consulta tus fechas, horarios y estados. Para solicitar un cambio, usa las opciones disponibles en la cita o comunícate con Recepción.",
            ["Paciente"],
            ["cambiar cita", "cambio de cita", "reprogramar cita", "modificar cita", "mover cita", "cambiar fecha"],
            ["¿Cómo cambio la cita?", "¿Cómo puedo reprogramar una cita?", "¿Puedo cambiar la fecha de mi cita?"],
            [
                "Abre Mis citas y selecciona la cita correspondiente.",
                "Usa las opciones disponibles en la cita para solicitar un cambio.",
                "Si no puedes solicitarlo desde ahí, comunícate con Recepción."
            ]),
        new(
            "appointment-reception-cancel",
            "Cancelar una cita",
            "Gestión de Citas",
            "/gestion-de-citas/st-rec-03-gestion-citas",
            "Recepción puede gestionar el estado de una cita desde la vista de Gestión de Citas. El sistema conserva la operación dentro del flujo autorizado y no permite tratar una cancelación como una cita activa.",
            ["Administrador", "Recepcionista"],
            ["cancelar cita", "anular cita", "eliminar cita", "cita cancelada", "estado cancelada"],
            ["¿Cómo cancelo una cita?", "¿Cómo anulo una cita?", "¿Qué pasa al cancelar una cita?"],
            [
                "Abre Gestión de Citas.",
                "Busca y selecciona la cita que corresponde.",
                "Usa la acción de cambio de estado disponible.",
                "Verifica que el resultado mostrado sea cancelado."
            ]),
        new(
            "professional-registration",
            "Registrar o actualizar un profesional",
            "Gestión de Profesionales",
            "/gestion-de-profesionales/st-adm-07-gestion-profesionales",
            "Administración puede registrar y actualizar profesionales desde la vista correspondiente.",
            ["Administrador"],
            ["registrar profesional", "nuevo profesional", "profesional nuevo", "crear odontologo", "profecional"],
            ["¿Cómo registro un profesional?", "¿Cómo doy de alta un odontólogo?", "¿Cómo registro un profecional?"],
            [
                "Abre Gestión de Profesionales.",
                "Inicia el registro o edición disponible.",
                "Completa los datos requeridos y verifica los datos obligatorios.",
                "Guarda y confirma que el profesional aparezca en el listado."
            ]),
        new(
            "professional-absence",
            "Ausencias y disponibilidad profesional",
            "Gestión de Profesionales",
            "/gestion-de-profesionales/ausencias",
            "Administración puede consultar o gestionar las ausencias profesionales desde la funcionalidad correspondiente.",
            ["Administrador", "Recepcionista"],
            ["ausencia", "vacaciones", "disponibilidad", "no disponible", "horario profesional"],
            ["¿Cómo registro una ausencia?", "¿Cómo funcionan las vacaciones?", "¿Dónde veo la disponibilidad?"],
            [
                "Abre la sección de ausencias o disponibilidad.",
                "Selecciona el profesional y el periodo.",
                "Revisa los datos antes de guardar.",
                "Ten presente que el horario puede impedir asignar o reagendar citas."
            ]),
        new(
            "support-faq-attachments",
            "Adjuntar evidencia a un ticket",
            "Centro de Ayuda",
            "/centro-de-ayuda/soporte",
            "El formulario completo acepta imágenes, PDF, Word y texto. El límite validado por el servicio es 5 MB. El chat ofrece un enlace al formulario completo.",
            AllRoles,
            ["ticket", "soporte", "adjuntar", "archivo", "captura", "evidencia"],
            ["¿Qué archivos puedo adjuntar?", "¿Cómo adjunto una captura?", "¿Cuál es el tamaño máximo del archivo?"],
            ["Abre Soporte y Tickets.", "Completa los datos del reporte.", "Adjunta evidencia de hasta 5 MB y envía el formulario."]),
        new(
            "support-ticket-status",
            "Estados de un ticket",
            "Centro de Ayuda",
            "/centro-de-ayuda/soporte",
            "Los estados definidos por el servicio son abierto, en_proceso, resuelto y cerrado. El estado actual y la respuesta solo se consultan mediante las funciones autorizadas del sistema.",
            AllRoles,
            ["ticket", "estado", "seguimiento", "solicitud", "referencia"],
            ["¿Qué significa el estado de mi ticket?", "¿Cómo consulto una solicitud?", "¿Cómo hago seguimiento a soporte?"],
            ["Abierto: ticket registrado.", "En proceso: el ticket está siendo atendido.", "Resuelto o cerrado: el ticket figura como finalizado."]),
        new(
            "help-context",
            "Ayuda de la vista actual",
            "Centro de Ayuda",
            "/centro-de-ayuda/guias-tutoriales",
            "Puedo identificar vistas registradas en el catálogo, explicar su propósito y abrirlas si están disponibles para tu rol. Si el catálogo no contiene instrucciones verificadas para una vista, te lo indicaré.",
            AllRoles,
            ["vista", "pantalla", "módulo", "sección", "qué hace", "cómo uso", "ayuda contextual"],
            ["¿Qué hace esta vista?", "¿Cómo uso esta pantalla?", "¿Qué opciones tengo aquí?"],
            [])
    ];

    public static IReadOnlyList<ChatbotKnowledgeEntry> ForRole(string role) =>
        Entries.Where(entry => entry.Roles.Contains(role, StringComparer.OrdinalIgnoreCase)).ToArray();

    public static ChatbotKnowledgeEntry? FindForRole(string? path, string role)
    {
        var normalizedPath = NormalizePath(path);
        if (normalizedPath is null)
            return null;

        return Entries
            .Where(entry =>
                string.Equals(NormalizePath(entry.Path), normalizedPath, StringComparison.OrdinalIgnoreCase) &&
                entry.Roles.Contains(role, StringComparer.OrdinalIgnoreCase))
            .OrderByDescending(entry => entry.Id.StartsWith("help-", StringComparison.Ordinal))
            .ThenBy(entry => entry.Steps.Count > 0)
            .FirstOrDefault();
    }

    public static string? NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            !path.StartsWith("/", StringComparison.Ordinal) ||
            path.StartsWith("//", StringComparison.Ordinal) ||
            path.Contains('\\') ||
            path.Contains('?') ||
            path.Contains('#'))
        {
            return null;
        }

        var normalized = path.TrimEnd('/');
        return normalized.Length == 0 ? "/" : normalized;
    }

    private static ChatbotKnowledgeEntry Page(
        string id,
        string title,
        string module,
        string path,
        IReadOnlyList<string> roles,
        string summary,
        params string[] keywords) =>
        new(id, title, module, path, summary, roles, keywords, [], []);
}
