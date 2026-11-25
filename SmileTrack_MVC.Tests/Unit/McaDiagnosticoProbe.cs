using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SmileTrack_MVC.Services;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

// Sonda temporal de diagnóstico: vuelca métricas del catálogo, medición de rendimiento
// y resultados de la evaluación de calidad conversacional a un archivo de evidencia.
public sealed class McaDiagnosticoProbe
{
    [Fact]
    public void VolcarDiagnostico()
    {
        var sb = new StringBuilder();

        sb.AppendLine("== METRICAS DE CATALOGO ==");
        sb.AppendLine($"EntryCount={ChatbotKnowledgeCatalog.EntryCount}");
        sb.AppendLine($"ModuleCount={ChatbotKnowledgeCatalog.ModuleCount}");
        sb.AppendLine($"ViewCount={ChatbotKnowledgeCatalog.ViewCount}");
        sb.AppendLine($"ProcedureCount={ChatbotKnowledgeCatalog.ProcedureCount}");
        sb.AppendLine($"QuestionCount={ChatbotKnowledgeCatalog.QuestionCount}");
        sb.AppendLine($"DiscoveredTestCount={ChatbotKnowledgeCatalog.EntriesSnapshot.Count}");

        var roleCounts = new[] { "Administrador", "Recepcionista", "Profesional", "Auxiliar", "Paciente" };
        foreach (var role in roleCounts)
            sb.AppendLine($"EntradasRol[{role}]={ChatbotKnowledgeCatalog.ForRole(role).Count}");

        sb.AppendLine();
        sb.AppendLine("== DESGLOSE POR MODULO ==");
        foreach (var group in ChatbotKnowledgeCatalog.EntriesSnapshot
                     .GroupBy(entry => entry.Module, StringComparer.OrdinalIgnoreCase)
                     .OrderByDescending(g => g.Count())
                     .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            var paginas = group.Count(entry => entry.Steps.Count == 0);
            var procedimientos = group.Count(entry => entry.Steps.Count > 0);
            sb.AppendLine($"{group.Key}={group.Count()} (paginas={paginas}; procedimientos={procedimientos})");
        }

        sb.AppendLine();
        sb.AppendLine("== CLASIFICACION DE ENTRADAS ==");
        sb.AppendLine($"PaginasSinPasos={ChatbotKnowledgeCatalog.EntriesSnapshot.Count(entry => entry.Steps.Count == 0)}");
        sb.AppendLine($"ConPasos={ChatbotKnowledgeCatalog.EntriesSnapshot.Count(entry => entry.Steps.Count > 0)}");
        sb.AppendLine($"SinPreguntasFrecuentes={ChatbotKnowledgeCatalog.EntriesSnapshot.Count(entry => entry.Questions.Count == 0)}");
        sb.AppendLine($"ResumenMuyCorto={ChatbotKnowledgeCatalog.EntriesSnapshot.Count(entry => entry.Summary.Trim().Length < 40)}");

        sb.AppendLine();
        sb.AppendLine("== ENTRADAS DE PAGINA (sin pasos) ==");
        foreach (var entry in ChatbotKnowledgeCatalog.EntriesSnapshot
                     .Where(entry => entry.Steps.Count == 0)
                     .OrderBy(entry => entry.Module, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(entry => entry.Id, StringComparer.Ordinal))
        {
            sb.AppendLine($"{entry.Id} | {entry.Module} | {entry.Title} | {entry.Path} | roles={string.Join(',', entry.Roles)} | preguntas={entry.Questions.Count} | resumen='{OneLine(entry.Summary)}'");
        }

        sb.AppendLine();
        sb.AppendLine("== RENDIMIENTO (motor interno) ==");
        var chatbot = new ChatbotService();
        var queries = new[]
        {
            "¿Cómo programo una cita?",              // directa
            "¿Dónde veo mis citas?",                 // directa
            "¿Cómo uso el odontograma?",             // directa
            "¿Cómo genero una factura?",             // directa
            "Como agendo una sita",                  // error ortográfico
            "¿Cómo reservo un turno?",               // sinónimo
            "No me deja guardar el paciente",        // ambigua/error
            "¿Cómo exporto a un formato raro?",      // desconocida
            "Necesito soporte con un error",         // soporte
            "¿Qué hace esta vista?"                  // contextual
        };

        // Calentamiento
        for (var i = 0; i < 50; i++)
            chatbot.Respond("Recepcionista", null, "/", queries[i % queries.Length], $"warm-{i}");

        var durations = new List<double>();
        var errores = 0;
        const int iterations = 1000;
        var sw = new System.Diagnostics.Stopwatch();
        for (var i = 0; i < iterations; i++)
        {
            try
            {
                sw.Restart();
                chatbot.Respond("Recepcionista", null, "/", queries[i % queries.Length], $"bench-{i % 25}");
                sw.Stop();
                durations.Add(sw.Elapsed.TotalMilliseconds);
            }
            catch
            {
                errores++;
            }
        }

        durations.Sort();
        double pct(double p) => durations[Math.Min(durations.Count - 1, (int)Math.Ceiling(durations.Count * p) - 1)];
        sb.AppendLine($"Iteraciones={iterations}; Errores={errores}");
        sb.AppendLine($"Min={durations.First():F3} ms");
        sb.AppendLine($"Promedio={durations.Average():F3} ms");
        sb.AppendLine($"Mediana={pct(0.50):F3} ms");
        sb.AppendLine($"P95={pct(0.95):F3} ms");
        sb.AppendLine($"P99={pct(0.99):F3} ms");
        sb.AppendLine($"Max={durations.Last():F3} ms");

        // Medición concurrente
        var concurrentDurations = new System.Collections.Concurrent.ConcurrentBag<double>();
        var swc = new System.Diagnostics.Stopwatch();
        System.Threading.Tasks.Parallel.For(0, 400, i =>
        {
            var local = new System.Diagnostics.Stopwatch();
            local.Restart();
            new ChatbotService().Respond("Recepcionista", null, "/", queries[i % queries.Length], $"conc-{i % 25}");
            local.Stop();
            concurrentDurations.Add(local.Elapsed.TotalMilliseconds);
        });
        var concSorted = concurrentDurations.OrderBy(v => v).ToArray();
        double cpc(double p) => concSorted[Math.Min(concSorted.Length - 1, (int)Math.Ceiling(concSorted.Length * p) - 1)];
        sb.AppendLine($"ConcurrenteIteraciones={concSorted.Length}");
        sb.AppendLine($"ConcurrentePromedio={concSorted.Average():F3} ms");
        sb.AppendLine($"ConcurrenteP95={cpc(0.95):F3} ms");
        sb.AppendLine($"ConcurrenteMax={concSorted.Last():F3} ms");

        sb.AppendLine();
        sb.AppendLine("== EVALUACION CONVERSACIONAL (volcado para clasificación) ==");
        var eval = new ChatbotService();
        var cases = EvaluationSet();
        var index = 0;
        foreach (var c in cases)
        {
            index++;
            var key = c.Category.StartsWith("consecutiva", StringComparison.Ordinal)
                ? "eval-consecutiva"
                : $"eval-{index}";
            var r = eval.Respond(c.Role, null, c.Path, c.Query, key);
            var actions = string.Join("; ", r.Actions.Select(a => $"{a.Type}:{a.Url}"));
            var sources = string.Join("; ", r.Sources.Select(s => s.Url));
            sb.AppendLine($"[{index}] rol={c.Role} categoria={c.Category}");
            sb.AppendLine($"    Q: {c.Query}");
            sb.AppendLine($"    clarif={r.NeedsClarification} msg={OneLine(r.Message)}");
            sb.AppendLine($"    actions=[{OneLine(actions)}]");
            sb.AppendLine($"    sources=[{OneLine(sources)}]");
        }

        var dir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "TestResults");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "MCA-DIAGNOSTICO.txt"), sb.ToString());

        Assert.True(ChatbotKnowledgeCatalog.EntryCount > 0);
        Assert.Equal(0, errores);
    }

    private static string OneLine(string value) =>
        value.Replace("\r", " ").Replace("\n", " | ");

    private sealed record EvalCase(string Role, string Path, string Query, string Category);

    private static IReadOnlyList<EvalCase> EvaluationSet() =>
    [
        // Sinónimos
        new("Recepcionista", "/", "¿Cómo reservo un turno para un paciente?", "sinonimo"),
        new("Recepcionista", "/", "Necesito agendar a alguien", "sinonimo"),
        new("Paciente", "/", "Quiero mover mi cita a otro día", "sinonimo"),
        new("Administrador", "/", "¿Cómo doy de alta a un odontólogo nuevo?", "sinonimo"),
        new("Profesional", "/", "¿Dónde marco las piezas dentales?", "sinonimo"),
        // Errores ortográficos
        new("Recepcionista", "/", "Como agendo una sita", "ortografia"),
        new("Administrador", "/", "Como registro un profecional", "ortografia"),
        new("Paciente", "/", "kiero cambiar mi cita", "ortografia"),
        new("Profesional", "/", "como uso el odontograma", "ortografia"),
        // Negaciones
        new("Paciente", "/", "No quiero crear un ticket", "negacion"),
        new("Recepcionista", "/", "No quiero cancelar una cita", "negacion"),
        // Ambigüedades
        new("Recepcionista", "/", "¿Cómo gestiono una cita?", "ambiguedad"),
        new("Administrador", "/", "¿Cómo gestiono pacientes?", "ambiguedad"),
        // Desconocidas
        new("Auxiliar", "/", "¿Cómo exporto un documento a un formato no documentado?", "desconocida"),
        new("Paciente", "/", "¿Puedo pagar con criptomonedas?", "desconocida"),
        new("Profesional", "/", "¿Cómo integro un asistente de inteligencia artificial?", "desconocida"),
        // Restringidas
        new("Paciente", "/", "¿Cómo gestiono usuarios?", "restringida"),
        new("Paciente", "/", "¿Dónde veo la facturación administrativa?", "restringida"),
        new("Auxiliar", "/", "¿Cómo creo un rol nuevo?", "restringida"),
        new("Profesional", "/", "¿Cómo elimino un usuario?", "restringida"),
        // Contextual / cambios de tema / consecutivas
        new("Paciente", "/gestion-de-citas/st-pac-01-mis-citas", "¿Qué hace esta vista?", "contextual"),
        new("Recepcionista", "/", "¿Cómo programo una cita?", "consecutiva-1"),
        new("Recepcionista", "/", "Y como la cancelo?", "consecutiva-2"),
        // Soporte
        new("Paciente", "/", "Necesito reportar un problema", "soporte"),
        new("Recepcionista", "/gestion-de-citas/st-rec-03-gestion-citas", "No me deja guardar la cita", "soporte"),
        new("Profesional", "/", "¿Cómo consulto mis tickets?", "soporte"),
        // Cinco roles - consulta representativa válida
        new("Administrador", "/", "¿Cómo gestiono usuarios?", "rol-admin"),
        new("Recepcionista", "/", "¿Cómo genero una factura?", "rol-recepcion"),
        new("Profesional", "/", "¿Cómo uso el odontograma?", "rol-profesional"),
        new("Auxiliar", "/", "¿Cómo preparo una consulta?", "rol-auxiliar"),
        new("Paciente", "/", "¿Cómo cambio mi contraseña?", "rol-paciente")
    ];
}
