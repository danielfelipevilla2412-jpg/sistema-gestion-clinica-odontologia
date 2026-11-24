using System.Globalization;
using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Configuration;
using SmileTrack_MVC.Models.Api;

namespace SmileTrack_MVC.Services;

public sealed class ChatbotService : IChatbotService
{
    private sealed record RoleProfile(string Key, string Label);
    private sealed record SearchResult(ChatbotKnowledgeEntry Entry, double Score);
    private enum ChatbotIntent
    {
        Unknown,
        Instruction,
        ContextHelp,
        Status,
        ErrorResolution,
        TicketCreation,
        TicketTracking,
        Greeting
    }
    private sealed record ConversationMessage(string Query, string EntryId);
    private sealed record ConversationState(
        Queue<ConversationMessage> Messages,
        string? CurrentTopicEntryId,
        DateTimeOffset LastTouched);

    private static readonly ConcurrentDictionary<string, ConversationState> ConversationStates =
        new(StringComparer.Ordinal);
    private const int MaxConversationMessages = 6;
    private readonly ChatbotOptions _options;

    public ChatbotService(IConfiguration? configuration = null)
    {
        _options = new ChatbotOptions();
        configuration?.GetSection("Chatbot").Bind(_options);
    }

    private static readonly IReadOnlyDictionary<string, RoleProfile> Profiles =
        new Dictionary<string, RoleProfile>(StringComparer.OrdinalIgnoreCase)
        {
            ["administrador"] = new("administrador", "Administrador"),
            ["recepcionista"] = new("recepcionista", "Recepcionista"),
            ["profesional"] = new("profesional", "Profesional"),
            ["odontologo"] = new("profesional", "Profesional"),
            ["doctor"] = new("profesional", "Profesional"),
            ["auxiliar"] = new("auxiliar", "Auxiliar"),
            ["paciente"] = new("paciente", "Paciente"),
            ["usuario"] = new("usuario", "Usuario")
        };

    private static readonly string[][] SynonymGroups =
    [
        ["cita", "agendar", "programar", "reservar", "turno"],
        ["cambiar", "cambio", "mover", "reprogramar", "modificar"],
        ["vista", "pantalla", "seccion", "modulo"],
        ["guia", "tutorial", "instructivo", "manual"],
        ["ayuda", "soporte", "asistencia"],
        ["ticket", "solicitud", "incidente", "reporte"],
        ["profesional", "odontologo", "doctor", "dentista"],
        ["paciente", "usuario", "persona"],
        ["consultorio", "sala", "box"],
        ["clave", "contrasena", "password"],
        ["historial", "historia", "expediente"]
    ];

    private static readonly HashSet<string> StopWords =
    [
        "a", "al", "de", "del", "el", "en", "la", "las", "lo", "los",
        "mi", "mis", "me", "un", "una", "y", "que", "como", "donde",
        "por", "para", "con", "puedo", "quiero", "hacer", "ver", "esta",
        "este", "hay", "es", "se", "the", "how", "what", "where", "my"
    ];

    public ChatbotResponse Respond(
        string? role,
        string? userName,
        string? currentPath,
        string message,
        string? conversationKey = null)
    {
        var profile = ResolveProfile(role);
        var safePath = ChatbotKnowledgeCatalog.NormalizePath(currentPath);
        var context = ChatbotKnowledgeCatalog.FindForRole(safePath, profile.Key);
        var query = Normalize(message);

        if (!string.IsNullOrWhiteSpace(conversationKey) &&
            IsFollowUpQuestion(query))
        {
            var previousTopicId = GetConversationState(conversationKey)?.CurrentTopicEntryId;
            var previousTopic = ChatbotKnowledgeCatalog.ForRole(profile.Key)
                .FirstOrDefault(entry => entry.Id == previousTopicId);
            if (previousTopic is not null)
            {
                if (ContainsAny(query, "cancelo", "cancelar", "anular"))
                    query = "cancelar cita";
                else if (ContainsAny(query, "cambio", "reprogram", "reagend"))
                    query = "reprogramar cita";
                else
                    query = Normalize($"{previousTopic.Title} {previousTopic.Keywords.LastOrDefault() ?? previousTopic.Module}");
            }
        }

        if (IsResetRequest(query))
        {
            if (!string.IsNullOrWhiteSpace(conversationKey))
                ResetConversation(conversationKey);
            return Welcome(profile, context, userName);
        }

        if (string.IsNullOrWhiteSpace(query) || IsGreeting(query))
            return Welcome(profile, context, userName);

        if (IsTicketTrackingQuestion(query))
            return TicketTrackingResponse(profile, userName);

        if (IsTicketCreationQuestion(query) &&
            !ContainsAny(query, "cita", "profesional", "paciente", "agenda", "guardar"))
            return TicketCreationResponse(profile, userName);

        if (ContainsAny(query, "como cancelo una cita", "como anulo una cita",
            "como cancelar una cita", "cancelar cita", "la cancelo"))
        {
            var cancellation = ChatbotKnowledgeCatalog.ForRole(profile.Key)
                .FirstOrDefault(entry => entry.Id == "appointment-reception-cancel");
            if (cancellation is not null)
            {
                if (!string.IsNullOrWhiteSpace(conversationKey))
                    RememberConversation(conversationKey, message, cancellation.Id);
                return KnowledgeResponse(
                    profile, userName, cancellation, safePath, false,
                    FormatSteps(cancellation));
            }
        }

        if (IsCurrentViewQuestion(query))
        {
            if (context is null)
                return UnknownViewResponse(profile, userName);

            return KnowledgeResponse(
                profile,
                userName,
                context,
                currentPath: safePath,
                needsClarification: false,
                intro: $"Esta vista se llama «{context.Title}» y pertenece a {context.Module}. {context.Summary}");
        }

        var results = Search(query, profile.Key, context?.Path);
        if (results.Count == 0)
            return NoVerifiedAnswer(profile, userName, context);

        var top = results[0];
        var second = results.Count > 1 ? results[1] : null;
        if (second is not null && IsAmbiguous(top, second))
            return ClarificationResponse(profile, userName, results.Take(3).ToArray());

        var answer = top.Entry.Steps.Count > 0
            ? FormatSteps(top.Entry)
            : $"{top.Entry.Title}: {top.Entry.Summary}";

        if (top.Entry.Steps.Count == 0 &&
            ContainsAny(query, "pasos", "como hago", "como puedo", "como se hace", "procedimiento"))
        {
            answer += " No tengo pasos verificados para esta operación; puedo abrir la vista correspondiente o ayudarte a reportar el problema.";
        }

        if (!string.IsNullOrWhiteSpace(conversationKey))
            RememberConversation(conversationKey, message, top.Entry.Id);

        return KnowledgeResponse(profile, userName, top.Entry, safePath, false, answer,
            results.Skip(1).Take(2).Select(result => result.Entry).ToArray());
    }

    public void ResetConversation(string conversationKey)
    {
        if (!string.IsNullOrWhiteSpace(conversationKey))
            ConversationStates.TryRemove(conversationKey, out _);
    }

    private ConversationState? GetConversationState(string conversationKey)
    {
        if (!ConversationStates.TryGetValue(conversationKey, out var state))
            return null;

        if (DateTimeOffset.UtcNow - state.LastTouched >
            TimeSpan.FromMinutes(_options.ConversationRetentionMinutes))
        {
            ConversationStates.TryRemove(conversationKey, out _);
            return null;
        }
        return state;
    }

    private void PurgeConversationStates()
    {
        var cutoff = DateTimeOffset.UtcNow -
                     TimeSpan.FromMinutes(_options.ConversationRetentionMinutes);
        foreach (var item in ConversationStates)
        {
            if (item.Value.LastTouched < cutoff)
                ConversationStates.TryRemove(item.Key, out _);
        }

        if (ConversationStates.Count <= _options.MaxConversationStates)
            return;

        foreach (var item in ConversationStates
                     .OrderBy(entry => entry.Value.LastTouched)
                     .Take(ConversationStates.Count - _options.MaxConversationStates))
        {
            ConversationStates.TryRemove(item.Key, out _);
        }
    }

    private void RememberConversation(string conversationKey, string query, string entryId)
    {
        var state = ConversationStates.AddOrUpdate(
            conversationKey,
            _ => new ConversationState(
                new Queue<ConversationMessage>([new ConversationMessage(query, entryId)]),
                entryId,
                DateTimeOffset.UtcNow),
            (_, existing) =>
            {
                existing.Messages.Enqueue(new ConversationMessage(query, entryId));
                while (existing.Messages.Count > MaxConversationMessages)
                    existing.Messages.Dequeue();
                return new ConversationState(existing.Messages, entryId, DateTimeOffset.UtcNow);
            });
        PurgeConversationStates();
    }

    private static ChatbotResponse Welcome(
        RoleProfile profile,
        ChatbotKnowledgeEntry? context,
        string? userName)
    {
        var message = context is null
            ? $"Soy el asistente de SmileTrack para el rol {profile.Label}. Puedo buscar guías verificadas, explicar vistas disponibles y ayudarte con soporte."
            : $"Estás en «{context.Title}». {context.Summary} Puedo explicarte esta vista, buscar una guía o ayudarte con soporte.";

        var suggestions = new List<string>
        {
            "¿Qué hace esta vista?",
            "Buscar guías",
            "Crear ticket de soporte",
            "Consultar mis tickets"
        };

        var actions = new List<ChatbotAction>();
        if (context is not null)
            actions.Add(CreateLink(context.Title, context.Path));

        foreach (var helpPath in new[]
                 {
                     "/centro-de-ayuda/guias-tutoriales",
                     "/centro-de-ayuda/como-programar-cita",
                     "/centro-de-ayuda/soporte"
                 })
        {
            var helpEntry = ChatbotKnowledgeCatalog.FindForRole(helpPath, profile.Key);
            if (helpEntry is not null)
                actions.Add(CreateLink(helpEntry.Title, helpEntry.Path));
        }

        return CreateResponse(profile, userName, message, suggestions, actions, []);
    }

    private static ChatbotResponse TicketTrackingResponse(RoleProfile profile, string? userName) =>
        CreateResponse(
            profile,
            userName,
            "Puedo consultar los tickets asociados a tu cuenta. Selecciona «Consultar mis tickets» para ver su estado; solo se mostrarán solicitudes creadas por ti.",
            ["Consultar mis tickets", "Crear ticket de soporte"],
            [new ChatbotAction { Label = "Consultar mis tickets", Type = "ticket-list" }],
            []);

    private static ChatbotResponse TicketCreationResponse(RoleProfile profile, string? userName) =>
        CreateResponse(
            profile,
            userName,
            "Puedo ayudarte a registrar un ticket. Describe el problema sin incluir contraseñas ni datos clínicos. Para adjuntar evidencia, abre el formulario completo de soporte.",
            ["Crear ticket de soporte", "Adjuntar evidencia"],
            [
                new ChatbotAction { Label = "Crear ticket aquí", Type = "ticket-form" },
                CreateLink("Formulario completo y adjuntos", "/centro-de-ayuda/soporte")
            ],
            []);

    private static ChatbotResponse NoVerifiedAnswer(
        RoleProfile profile,
        string? userName,
        ChatbotKnowledgeEntry? context)
    {
        var message = context is null
            ? "No encuentro una guía verificada para esa pregunta. Dime el nombre de la pantalla o la tarea que intentas realizar, o crea un ticket de soporte."
            : $"No encuentro una guía verificada para esa pregunta en «{context.Title}». Puedo explicar el propósito de esta vista o ayudarte a crear un ticket.";

        var actions = new List<ChatbotAction>();
        if (context is not null)
            actions.Add(CreateLink(context.Title, context.Path));
        actions.Add(new ChatbotAction { Label = "Crear ticket de soporte", Type = "ticket-form" });

        return CreateResponse(
            profile,
            userName,
            message,
            ["¿Qué hace esta vista?", "Buscar guías", "Crear ticket de soporte"],
            actions,
            []);
    }

    private static ChatbotResponse UnknownViewResponse(RoleProfile profile, string? userName) =>
        CreateResponse(
            profile,
            userName,
            "No puedo identificar esa vista dentro del catálogo autorizado para tu rol. Indícame el nombre del módulo o selecciona una guía disponible.",
            ["Buscar guías", "Crear ticket de soporte"],
            new ChatbotAction?[]
            {
                CreateLinkIfAllowed(profile, "Guías y Tutoriales", "/centro-de-ayuda/guias-tutoriales"),
                new ChatbotAction { Label = "Crear ticket de soporte", Type = "ticket-form" }
            }.Where(action => action is not null).Select(action => action!).ToArray(),
            []);

    private static ChatbotResponse ClarificationResponse(
        RoleProfile profile,
        string? userName,
        IReadOnlyList<SearchResult> results)
    {
        var suggestions = results.Select(result => result.Entry.Title).Distinct().ToArray();
        var actions = results
            .Select(result => CreateLink(result.Entry.Title, result.Entry.Path))
            .DistinctBy(action => action.Url, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return CreateResponse(
            profile,
            userName,
            $"Encontré varias opciones parecidas. ¿Cuál necesitas? {string.Join(" / ", suggestions)}.",
            suggestions,
            actions,
            results.Select(result => ToSource(result.Entry)).DistinctBy(source => source.Id).ToArray(),
            needsClarification: true);
    }

    private static ChatbotResponse KnowledgeResponse(
        RoleProfile profile,
        string? userName,
        ChatbotKnowledgeEntry entry,
        string? currentPath,
        bool needsClarification,
        string intro,
        IReadOnlyList<ChatbotKnowledgeEntry>? additionalEntries = null)
    {
        var actions = new List<ChatbotAction>();
        if (!string.Equals(
                ChatbotKnowledgeCatalog.NormalizePath(currentPath),
                ChatbotKnowledgeCatalog.NormalizePath(entry.Path),
                StringComparison.OrdinalIgnoreCase))
        {
            actions.Add(CreateLink(entry.Title, entry.Path));
        }

        if (entry.Id == "appointment-guide-steps")
        {
            var agendaPath = profile.Key switch
            {
                "administrador" => "/gestion-de-citas/st-adm-08-agenda",
                "recepcionista" => "/gestion-de-citas/st-rec-03-gestion-citas",
                _ => null
            };
            if (agendaPath is not null)
                actions.Add(CreateLink("Abrir agenda", agendaPath));
        }

        foreach (var additional in additionalEntries ?? [])
        {
            if (!string.Equals(additional.Path, entry.Path, StringComparison.OrdinalIgnoreCase))
                actions.Add(CreateLink(additional.Title, additional.Path));
        }

        return CreateResponse(
            profile,
            userName,
            intro,
            additionalEntries?.Select(item => item.Title).ToArray() ?? [],
            actions,
            new[] { ToSource(entry) }.Concat(
                (additionalEntries ?? []).Select(ToSource)).DistinctBy(source => source.Id).ToArray(),
            needsClarification);
    }

    private static ChatbotResponse CreateResponse(
        RoleProfile profile,
        string? userName,
        string message,
        IReadOnlyList<string> suggestions,
        IReadOnlyList<ChatbotAction> actions,
        IReadOnlyList<ChatbotKnowledgeSource> sources,
        bool needsClarification = false)
    {
        var prefix = string.IsNullOrWhiteSpace(userName)
            ? string.Empty
            : $"{userName.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0]}, ";

        var safeActions = actions
            .Where(action =>
                action.Type is "ticket-form" or "ticket-list" ||
                ChatbotKnowledgeCatalog.FindForRole(action.Url, profile.Key) is not null)
            .DistinctBy(action => $"{action.Type}:{action.Url}", StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new ChatbotResponse
        {
            Role = profile.Key,
            RoleLabel = profile.Label,
            Message = prefix + message,
            NeedsClarification = needsClarification,
            Suggestions = suggestions,
            Actions = safeActions,
            Sources = sources
        };
    }

    private List<SearchResult> Search(string query, string role, string? currentPath)
    {
        var originalTokens = Tokenize(query).ToHashSet(StringComparer.Ordinal);
        var queryTokens = ExpandTokens(originalTokens);
        if (queryTokens.Count == 0)
            return [];

        return ChatbotKnowledgeCatalog.ForRole(role)
            .Select(entry => new SearchResult(entry, Score(entry, query, queryTokens, originalTokens.Count, currentPath)))
            .Where(result => result.Score >= _options.HighRelevanceThreshold)
            .GroupBy(result => result.Entry.Path, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(result => result.Score)
                .ThenByDescending(result => result.Entry.Steps.Count)
                .First())
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.Entry.Title, StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();
    }

    private static double Score(
        ChatbotKnowledgeEntry entry,
        string query,
        HashSet<string> queryTokens,
        int originalTokenCount,
        string? currentPath)
    {
        var keywordMatches = CountMatches(Tokenize(string.Join(' ', entry.Keywords)), queryTokens);
        var questionMatches = CountMatches(Tokenize(string.Join(' ', entry.Questions)), queryTokens);
        var titleMatches = CountMatches(Tokenize($"{entry.Title} {entry.Module}"), queryTokens);
        var summaryMatches = CountMatches(Tokenize(entry.Summary), queryTokens);
        var stepMatches = CountMatches(Tokenize(string.Join(' ', entry.Steps)), queryTokens);

        var score = (keywordMatches * 2.4) +
                    (questionMatches * 2.0) +
                    (titleMatches * 1.8) +
                    (summaryMatches * 0.8) +
                    (stepMatches * 0.5);

        var normalizedQuery = Normalize(query);
        if (normalizedQuery.Length >= 4 &&
            Normalize(entry.Title).Contains(normalizedQuery, StringComparison.Ordinal))
        {
            score += 2.0;
        }

        if (currentPath is not null &&
            string.Equals(entry.Path, currentPath, StringComparison.OrdinalIgnoreCase))
        {
            score += 0.25;
        }

        return score / Math.Max(1, originalTokenCount);
    }

    private bool IsAmbiguous(SearchResult first, SearchResult second) =>
        Math.Abs(first.Score - second.Score) <= Math.Max(
            _options.AmbiguityScoreMargin,
            first.Score * _options.AmbiguityRelativeMargin);

    private static string FormatSteps(ChatbotKnowledgeEntry entry) =>
        $"{entry.Title}\n" + string.Join(
            "\n",
            entry.Steps.Select((step, index) => $"{index + 1}. {step}"));

    private static bool IsGreeting(string message) =>
        ContainsAny(message, "hola", "buenos dias", "buenas tardes", "buenas noches", "saludo");

    private static bool IsResetRequest(string message) =>
        ContainsAny(message, "reiniciar conversacion", "nueva conversacion", "borrar historial",
            "limpiar conversacion", "empezar de nuevo");

    private static bool IsFollowUpQuestion(string message) =>
        ContainsAny(message, "y como", "y donde", "y que", "tambien", "entonces",
            "la cancelo", "la cancela", "la cambio", "la reprogramo");

    private static ChatbotIntent RecognizeIntent(string message)
    {
        if (IsGreeting(message)) return ChatbotIntent.Greeting;
        if (IsTicketTrackingQuestion(message)) return ChatbotIntent.TicketTracking;
        if (IsTicketCreationQuestion(message)) return ChatbotIntent.TicketCreation;
        if (IsCurrentViewQuestion(message)) return ChatbotIntent.ContextHelp;
        if (ContainsAny(message, "estado", "seguimiento", "resultado")) return ChatbotIntent.Status;
        if (ContainsAny(message, "error", "fallo", "no funciona", "no puedo", "problema")) return ChatbotIntent.ErrorResolution;
        if (ContainsAny(message, "como", "pasos", "procedimiento", "instruccion", "donde")) return ChatbotIntent.Instruction;
        return ChatbotIntent.Unknown;
    }

    private static bool IsCurrentViewQuestion(string message) =>
        ContainsAny(message, "que hace esta vista", "que hace esta pantalla", "como uso esta vista",
            "como uso esta pantalla", "que puedo hacer aqui", "ayuda contextual", "en esta vista");

    private static bool IsTicketTrackingQuestion(string message) =>
        ContainsAny(message, "mis tickets", "mi ticket", "seguimiento", "estado del ticket",
            "consultar ticket", "consultar solicitud", "ver ticket", "referencia del ticket");

    private static bool IsTicketCreationQuestion(string message) =>
        ContainsAny(message, "crear ticket", "nuevo ticket", "reportar problema", "reportar error",
            "reportar un problema", "reportar un error", "necesito reportar", "no funciona",
            "no puedo", "me sale error", "fallo", "incidente", "necesito soporte");

    private static HashSet<string> ExpandTokens(IEnumerable<string> tokens)
    {
        var expanded = new HashSet<string>(tokens, StringComparer.Ordinal);
        foreach (var group in SynonymGroups)
        {
            if (group.Any(expanded.Contains))
                expanded.UnionWith(group);
        }
        return expanded;
    }

    private static IEnumerable<string> Tokenize(string value) =>
        Normalize(value)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length > 1 && !StopWords.Contains(token));

    private static int CountMatches(IEnumerable<string> source, HashSet<string> queryTokens)
    {
        var sourceTokens = source
            .SelectMany(value => value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var matches = 0;
        foreach (var queryToken in queryTokens)
        {
            if (sourceTokens.Any(sourceToken =>
                    sourceToken == queryToken ||
                    IsReasonableFuzzyMatch(sourceToken, queryToken)))
            {
                matches++;
            }
        }
        return matches;
    }

    private static bool IsReasonableFuzzyMatch(string left, string right)
    {
        if (left.Length < 5 || right.Length < 5)
            return false;

        var distance = LevenshteinDistance(left, right);
        var threshold = Math.Max(left.Length, right.Length) >= 8 ? 2 : 1;
        return distance <= threshold;
    }

    private static int LevenshteinDistance(string left, string right)
    {
        var previous = Enumerable.Range(0, right.Length + 1).ToArray();
        for (var i = 1; i <= left.Length; i++)
        {
            var current = new int[right.Length + 1];
            current[0] = i;
            for (var j = 1; j <= right.Length; j++)
            {
                var cost = left[i - 1] == right[j - 1] ? 0 : 1;
                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + cost);
            }
            previous = current;
        }
        return previous[right.Length];
    }

    private static ChatbotKnowledgeSource ToSource(ChatbotKnowledgeEntry entry) => new()
    {
        Id = entry.Id,
        Title = entry.Title,
        Url = entry.Path
    };

    private static ChatbotAction CreateLink(string label, string path) => new()
    {
        Label = label,
        Url = path,
        Type = "link"
    };

    private static ChatbotAction? CreateLinkIfAllowed(RoleProfile profile, string label, string path) =>
        ChatbotKnowledgeCatalog.FindForRole(path, profile.Key) is null
            ? null
            : CreateLink(label, path);

    private static RoleProfile ResolveProfile(string? role)
    {
        var normalizedRole = Normalize(role);
        return Profiles.TryGetValue(normalizedRole, out var profile)
            ? profile
            : Profiles["usuario"];
    }

    private static bool ContainsAny(string value, params string[] terms) =>
        terms.Any(term => value.Contains(Normalize(term), StringComparison.Ordinal));

    private static string Normalize(string? value)
    {
        var normalized = (value ?? string.Empty)
            .Trim()
            .ToLowerInvariant()
            .Normalize(NormalizationForm.FormD);

        var withoutDiacritics = new string(normalized
            .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            .ToArray());

        var builder = new StringBuilder(withoutDiacritics.Length);
        var previousWasSpace = false;
        foreach (var character in withoutDiacritics)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasSpace = false;
            }
            else if (!previousWasSpace)
            {
                builder.Append(' ');
                previousWasSpace = true;
            }
        }

        return builder.ToString().Trim();
    }
}
