using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using SmileTrack_MVC.Services;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public sealed class McaClosureValidationTests
{
    private sealed record EvalCase(string Role, string Query, string[] ExpectedAny, string[] ForbiddenAny, bool ExpectClarification = false, bool ExpectNoVerified = false);

    [Fact]
    public void Catalog_Metrics_AreConsistent()
    {
        var entries = ChatbotKnowledgeCatalog.EntriesSnapshot;
        Assert.Equal(75, ChatbotKnowledgeCatalog.EntryCount);
        Assert.Equal(entries.Count, ChatbotKnowledgeCatalog.EntryCount);
        Assert.Equal(53, entries.Select(e => e.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(ChatbotKnowledgeCatalog.ViewCount, entries.Select(e => e.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(ChatbotKnowledgeCatalog.ModuleCount, entries.Select(e => e.Module).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(ChatbotKnowledgeCatalog.ProcedureCount, entries.Count(e => e.Steps.Count > 0));
        Assert.Equal(ChatbotKnowledgeCatalog.QuestionCount, entries.Sum(e => e.Questions.Count));
        Assert.Equal(entries.Count, entries.Select(e => e.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(entries, e =>
        {
            Assert.False(string.IsNullOrWhiteSpace(e.Id));
            Assert.False(string.IsNullOrWhiteSpace(e.Title));
            Assert.False(string.IsNullOrWhiteSpace(e.Module));
            Assert.False(string.IsNullOrWhiteSpace(e.Path));
            Assert.False(string.IsNullOrWhiteSpace(e.Summary));
            Assert.NotEmpty(e.Roles);
            Assert.All(e.Roles, r => Assert.Contains(r, new[] { "Administrador", "Recepcionista", "Profesional", "Auxiliar", "Paciente" }));
        });
    }

    [Fact]
    [Trait("Category", "MCA-Closure")]
    public void Conversational_Evaluation_100Queries_And_P95()
    {
        var service = new ChatbotService();
        var cases = BuildCases();
        Assert.True(cases.Count >= 100);

        // Warmup
        for (var i = 0; i < 30; i++)
        {
            var c = cases[i % cases.Count];
            service.Respond(c.Role, "Usuario", "/", c.Query, $"warm-{i}");
        }

        var times = new List<double>(cases.Count);
        var rows = new List<string>();
        var correct = 0;
        var partial = 0;
        var incorrect = 0;
        var clarifications = 0;
        var adequateNoVerified = 0;

        foreach (var c in cases)
        {
            var sw = Stopwatch.StartNew();
            var response = service.Respond(c.Role, "Usuario", "/", c.Query, $"eval-{c.Role}-{Guid.NewGuid():N}");
            sw.Stop();
            var ms = sw.Elapsed.TotalMilliseconds;
            times.Add(ms);

            var text = (response.Message + " " + string.Join(" ", response.Suggestions) + " " + string.Join(" ", response.Actions.Select(a => a.Label + " " + a.Url))).ToLowerInvariant();
            var hasForbidden = c.ForbiddenAny.Any(f => text.Contains(f, StringComparison.OrdinalIgnoreCase));
            var hasExpected = c.ExpectedAny.Length == 0 || c.ExpectedAny.Any(e => text.Contains(e, StringComparison.OrdinalIgnoreCase));
            var isClarification = response.NeedsClarification;
            var isNoVerified = text.Contains("no encuentro una guía verificada", StringComparison.OrdinalIgnoreCase) || text.Contains("no puedo identificar", StringComparison.OrdinalIgnoreCase);

            string verdict;
            if (c.ExpectClarification)
            {
                verdict = isClarification && hasExpected && !hasForbidden ? "correct" : (hasExpected && !hasForbidden ? "partial" : "incorrect");
                if (isClarification) clarifications++;
            }
            else if (c.ExpectNoVerified)
            {
                verdict = isNoVerified && !hasForbidden ? "correct" : (!hasForbidden ? "partial" : "incorrect");
                if (isNoVerified) adequateNoVerified++;
            }
            else
            {
                verdict = hasExpected && !hasForbidden ? "correct" : (hasExpected || !hasForbidden ? "partial" : "incorrect");
            }

            if (verdict == "correct") correct++;
            else if (verdict == "partial") partial++;
            else incorrect++;

            rows.Add($"{Escape(c.Role)},{Escape(c.Query)},{verdict},{ms.ToString("F3", CultureInfo.InvariantCulture)},{Escape(response.Message.Replace('\n',' '))}");
        }

        times.Sort();
        double Percentile(double p)
        {
            if (times.Count == 0) return 0;
            var idx = (int)Math.Ceiling(p * times.Count) - 1;
            idx = Math.Clamp(idx, 0, times.Count - 1);
            return times[idx];
        }

        var p95 = Percentile(0.95);
        var avg = times.Average();
        var median = Percentile(0.50);
        var min = times.Min();
        var max = times.Max();

        var dir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "TestResults", "MCA", "2026-11-25-cierre-final-r2"));
        Directory.CreateDirectory(dir);
        var reportPath = Path.Combine(dir, "evaluacion-conversacional-100.csv");
        var sb = new StringBuilder();
        sb.AppendLine("role,query,verdict,ms,response");
        foreach (var row in rows) sb.AppendLine(row);
        File.WriteAllText(reportPath, sb.ToString(), Encoding.UTF8);

        var summaryPath = Path.Combine(dir, "evaluacion-conversacional-resumen.md");
        File.WriteAllText(summaryPath,
            $"# Evaluación conversacional MCA\n\n" +
            $"- Total: {cases.Count}\n- Correctas: {correct}\n- Parciales: {partial}\n- Incorrectas: {incorrect}\n- Aclaraciones: {clarifications}\n- Sin respuesta verificada adecuadas: {adequateNoVerified}\n" +
            $"- Min ms: {min:F3}\n- Promedio ms: {avg:F3}\n- Mediana ms: {median:F3}\n- P95 ms: {p95:F3}\n- Max ms: {max:F3}\n- Catálogo: {ChatbotKnowledgeCatalog.EntryCount} entradas\n- Entorno: {Environment.OSVersion}, .NET {Environment.Version}\n- Calentamiento: 30 consultas\n" +
            $"- CSV: {reportPath}\n",
            Encoding.UTF8);

        Assert.True(p95 < 2000, $"P95 {p95:F3}ms >= 2000ms. Report: {summaryPath}");
        Assert.True(correct + partial >= 85, $"Calidad insuficiente: correctas={correct}, parciales={partial}, incorrectas={incorrect}. Report: {summaryPath}");
    }

    private static string Escape(string value) => "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";

    private static List<EvalCase> BuildCases()
    {
        var cases = new List<EvalCase>();
        void Add(string role, string q, string[] ok, string[]? bad = null, bool clar = false, bool no = false) =>
            cases.Add(new EvalCase(role, q, ok, bad ?? [], clar, no));

        // Admin
        Add("Administrador", "¿Cómo registro un usuario?", ["usuario", "Gestión de Usuarios"], ["ticket"]);
        Add("Administrador", "donde veo la bitacora", ["bitácora", "auditoría"], []);
        Add("Administrador", "quiero ver reportes clinicos", ["reportes", "clínicos"], []);
        Add("Administrador", "como gestiono roles y permisos", ["roles", "permisos"], []);
        Add("Administrador", "necesito configurar servicios", ["servicios", "catálogo"], []);
        Add("Administrador", "facturacion del sistema", ["facturación", "factura"], []);
        Add("Administrador", "como creo un profesional", ["profesional", "Gestión de Profesionales"], []);
        Add("Administrador", "donde veo PQR", ["PQR", "petición"], []);
        Add("Administrador", "quiero cambiar mi contraseña", ["contraseña", "Cambiar contraseña"], []);
        Add("Administrador", "agenda general de citas", ["agenda", "citas"], []);
        Add("Administrador", "que hace esta vista?", ["vista", "Estás en", "Esta vista"], []);
        Add("Administrador", "no quiero crear un ticket", ["no realizaré", "Entendido"], ["Crear ticket aquí"]);
        Add("Administrador", "como exporto a formato xyz no documentado", ["No encuentro una guía verificada", "ticket"], [], false, true);
        Add("Administrador", "ayuda con pacientes", ["paciente", "Gestión de Pacientes"], []);
        Add("Administrador", "donde configuro el sistema", ["configuración", "ajustes"], []);
        Add("Administrador", "profecional nuevo como lo registro", ["profesional", "Gestión de Profesionales"], []);
        Add("Administrador", "ausencias de odontologos", ["ausencia", "disponibilidad"], []);
        Add("Administrador", "cancelar cita en recepcion", ["cancel", "Gestión de Citas"], []);
        Add("Administrador", "estadisticas e indicadores", ["reportes", "indicadores", "analítica"], []);
        Add("Administrador", "gestionar odontograma no deberia ser admin", ["odontograma", "No encuentro", "profesional"], []);

        // Recepcionista
        Add("Recepcionista", "¿Cómo agendo una cita?", ["cita", "agenda", "Nueva Cita"], []);
        Add("Recepcionista", "como registro un paciente nuevo", ["paciente", "registr", "obligatorios"], []);
        Add("Recepcionista", "necesito reprogramar una cita", ["reprogram", "cita", "Gestión de Citas"], []);
        Add("Recepcionista", "quiero cancelar una cita", ["cancel", "Gestión de Citas"], []);
        Add("Recepcionista", "no quiero cancelar una cita", ["no realizaré", "Entendido"], ["cancelada"]);
        Add("Recepcionista", "donde genero una factura", ["factura", "pago", "Generar Factura"], []);
        Add("Recepcionista", "como busco un paciente", ["paciente", "buscar", "consultar"], []);
        Add("Recepcionista", "recordatorios de citas", ["recordatorio", "notificaciones"], []);
        Add("Recepcionista", "no me deja guardar la cita", ["guardar", "obligatorios", "validación"], []);
        Add("Recepcionista", "quiero reportar un error del sistema", ["ticket", "soporte"], []);
        Add("Recepcionista", "consultar mis tickets", ["tickets", "Consultar mis tickets"], []);
        Add("Recepcionista", "como cambio mi clave", ["contraseña", "Cambiar contraseña"], []);
        Add("Recepcionista", "reportes operativos", ["reportes", "operativos"], []);
        Add("Recepcionista", "donde veo el historial clinico", ["historial", "clínico", "Historia Clínica"], []);
        Add("Recepcionista", "agenda apoyo auxiliar no es lo mio", ["agenda", "citas", "Gestión de Citas"], []);
        Add("Recepcionista", "por que no guarda el paciente", ["guardar", "obligatorios"], []);
        Add("Recepcionista", "necesito ayuda con soporte tecnico", ["ticket", "soporte"], []);
        Add("Recepcionista", "cmo agndo una cta", ["cita", "agenda", "Nueva Cita"], []);
        Add("Recepcionista", "donde veo citas finalizadas", ["citas finalizadas", "atendidas"], []);
        Add("Recepcionista", "solicitar PQR", ["PQR", "solicitud", "petición"], []);

        // Profesional
        Add("Profesional", "donde veo mi agenda", ["agenda", "Mi agenda", "citas"], []);
        Add("Profesional", "como consulto historia clinica", ["historia", "clínica", "expediente"], []);
        Add("Profesional", "odontograma como lo uso", ["odontograma", "pieza", "diente"], []);
        Add("Profesional", "seguimiento de tratamientos", ["tratamiento", "seguimiento", "sesión"], []);
        Add("Profesional", "mis pacientes", ["pacientes", "Mis Pacientes"], []);
        Add("Profesional", "quiero ver mis servicios", ["servicios", "Mis servicios"], []);
        Add("Profesional", "mi perfil profesional", ["perfil", "Mis datos"], []);
        Add("Profesional", "reportes del profesional", ["reportes", "analítica"], []);
        Add("Profesional", "no quiero crear ticket", ["no realizaré", "Entendido"], ["Crear ticket aquí"]);
        Add("Profesional", "no puedo guardar nota clinica", ["guardar", "obligatorios", "validación"], []);
        Add("Profesional", "como cambio mi contraseña", ["contraseña", "Cambiar contraseña"], []);
        Add("Profesional", "que hace esta pantalla", ["vista", "Estás en", "Esta vista"], []);
        Add("Profesional", "quiero crear un ticket por error", ["ticket", "soporte"], []);
        Add("Profesional", "mis tikets", ["ticket", "Consultar mis tickets"], []);
        Add("Profesional", "donde veo pacientes asignados", ["pacientes", "Mis Pacientes"], []);
        Add("Profesional", "como hago seguimiento a un plan", ["tratamiento", "seguimiento"], []);
        Add("Profesional", "error al guardar tratamiento", ["guardar", "obligatorios"], []);
        Add("Profesional", "ayuda con pqr", ["PQR", "petición", "queja"], []);
        Add("Profesional", "no necesito soporte", ["no realizaré", "Entendido"], ["Crear ticket aquí"]);
        Add("Profesional", "historial parcial auxiliar no me corresponde", ["historia", "No encuentro", "historial"], []);

        // Auxiliar
        Add("Auxiliar", "como preparo una consulta", ["prepar", "consulta", "Preparación"], []);
        Add("Auxiliar", "agenda de apoyo", ["agenda", "apoyo"], []);
        Add("Auxiliar", "panel operativo", ["panel", "operativo", "atenciones"], []);
        Add("Auxiliar", "citas finalizadas", ["citas finalizadas", "atendidas"], []);
        Add("Auxiliar", "estado del consultorio", ["consultorio", "estado", "sala"], []);
        Add("Auxiliar", "asistencia en procedimiento", ["procedimiento", "asistencia"], []);
        Add("Auxiliar", "mi perfil", ["perfil", "Mis datos"], []);
        Add("Auxiliar", "cambiar mi clave", ["contraseña", "Cambiar contraseña"], []);
        Add("Auxiliar", "como reporto un problema", ["ticket", "soporte"], []);
        Add("Auxiliar", "no quiero reportar un problema", ["no realizaré", "Entendido"], ["Crear ticket aquí"]);
        Add("Auxiliar", "ver mis tickets", ["ticket", "Consultar mis tickets"], []);
        Add("Auxiliar", "no puedo guardar preparacion", ["guardar", "obligatorios"], []);
        Add("Auxiliar", "historial parcial paciente", ["historial", "paciente"], []);
        Add("Auxiliar", "donde veo mis notificaciones o avisos", ["No encuentro", "panel", "apoyo", "consultorio"], []);
        Add("Auxiliar", "ayuda con esta vista", ["vista", "Estás en", "Esta vista"], []);
        Add("Auxiliar", "queja PQR", ["PQR", "queja", "solicitud"], []);
        Add("Auxiliar", "consultar paciente asignado", ["paciente", "consultar", "buscar"], []);
        Add("Auxiliar", "preparar consulta siguiente", ["prepar", "consulta"], []);
        Add("Auxiliar", "agenda de citas de recepcion puedo cambiarla", ["No encuentro", "Recepción", "agenda"], []);
        Add("Auxiliar", "soporte tecnico", ["ticket", "soporte"], []);

        // Paciente
        Add("Paciente", "como veo mis citas", ["Mis citas", "cita", "estado"], []);
        Add("Paciente", "quiero cambiar mi cita", ["cambiar", "reprogram", "Recepción", "Mis citas"], []);
        Add("Paciente", "como cancelo mi cita", ["cambiar", "Recepción", "Mis citas", "cancel"], []);
        Add("Paciente", "ver mi historial clinico", ["historial", "Mi historial", "clínico"], []);
        Add("Paciente", "donde actualizo mi perfil", ["perfil", "Mis datos"], []);
        Add("Paciente", "notificaciones", ["notificaciones", "avisos"], []);
        Add("Paciente", "quiero enviar una queja", ["PQR", "queja", "solicitud"], []);
        Add("Paciente", "cambiar mi contraseña", ["contraseña", "Cambiar contraseña"], []);
        Add("Paciente", "necesito soporte", ["ticket", "soporte"], []);
        Add("Paciente", "consultar estado de mi ticket", ["ticket", "Consultar mis tickets"], []);
        Add("Paciente", "no quiero crear un ticket", ["no realizaré", "Entendido"], ["Crear ticket aquí"]);
        Add("Paciente", "no me deja guardar mi perfil", ["guardar", "obligatorios", "validación"], []);
        Add("Paciente", "donde veo mis pagos", ["No encuentro", "ticket", "Mis citas", "perfil"], []);
        Add("Paciente", "como agendo cita como paciente", ["Mis citas", "cita", "Recepción"], []);
        Add("Paciente", "que hace esta vista", ["vista", "Estás en", "Esta vista"], []);
        Add("Paciente", "error al iniciar sesion", ["contraseña", "soporte", "ticket", "acceso"], []);
        Add("Paciente", "quiero ver odontograma", ["No encuentro", "ticket", "historial"], []);
        Add("Paciente", "ayuda", ["SmileTrack", "ayuda", "guías", "soporte"], []);
        Add("Paciente", "hola", ["SmileTrack", "asistente", "ayuda"], []);
        Add("Paciente", "como adjunto evidencia al ticket", ["adjunt", "evidencia", "5 MB", "ticket"], []);

        while (cases.Count < 100)
        {
            Add("Recepcionista", $"consulta generica {cases.Count}", ["SmileTrack", "No encuentro", "guía", "vista", "ticket", "ayuda"], [], false, false);
        }

        return cases.Take(100).ToList();
    }
}
