using System.Globalization;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Configuration;
using SmileTrack_MVC.Models.Api;

[assembly: InternalsVisibleTo("SmileTrack_MVC.Tests")]

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
    internal sealed record ConversationMessage(string Query, string EntryId);
    internal sealed record ConversationState(
        Queue<ConversationMessage> Messages,
        string? CurrentTopicEntryId,
        DateTimeOffset LastTouched);

    private static readonly ConcurrentDictionary<string, ConversationState> ConversationStates =
        new(StringComparer.Ordinal);
    private const int MaxConversationMessages = 6;
    private readonly ChatbotOptions _options;
    private readonly TimeProvider _timeProvider;

    public ChatbotService(IConfiguration? configuration = null, TimeProvider? timeProvider = null)
    {
        _options = new ChatbotOptions();
        configuration?.GetSection("Chatbot").Bind(_options);
        // El reloj es inyectable para poder verificar los comportamientos temporales
        // (retención, expiración) sin depender del reloj del sistema.
        _timeProvider = timeProvider ?? TimeProvider.System;
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
        ["cita", "agendar", "programar", "reservar", "turno", "agenda", "cta", "agendo", "agndo", "agendacion"],
        ["cambiar", "cambio", "mover", "reprogramar", "reagendar", "rejendar", "modificar"],
        ["vista", "pantalla", "seccion", "modulo"],
        ["guia", "tutorial", "instructivo", "manual"],
        ["ayuda", "soporte", "asistencia"],
        ["ticket", "solicitud", "incidente", "reporte"],
        ["profesional", "odontologo", "doctor", "dentista"],
        ["paciente", "persona"],
        ["consultorio", "sala", "box"],
        ["clave", "contrasena", "password"],
        ["historial", "historia", "expediente"],
        ["configurar", "configuro", "configuracion", "configura", "ajuste", "ajustes", "ajustar"],
        ["pago", "pagos", "factura", "facturas", "cobro", "cobros", "facturacion"]
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
        {
            if (!string.IsNullOrWhiteSpace(conversationKey))
                RememberConversation(conversationKey, message, "welcome");
            return Welcome(profile, context, userName);
        }

        if (IsActionNegation(query))
            return NegationResponse(profile, userName, context);

        if (IsTicketTrackingQuestion(query))
            return TicketTrackingResponse(profile, userName);

        if (IsTicketCreationQuestion(query) &&
            !ContainsAny(query, "no quiero", "no deseo", "no necesito", "cita", "profesional", "paciente", "agenda", "guardar"))
            return TicketCreationResponse(profile, userName);

        if (!ContainsAny(query, "no quiero cancelar", "no deseo cancelar", "no necesito cancelar") &&
            ContainsAny(query, "como cancelo una cita", "como anulo una cita",
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

        if (ContainsAny(query, "no puedo guardar", "no me deja guardar", "porque no guarda",
            "error al guardar", "fallo al guardar", "no se guarda"))
        {
            var saveError = ChatbotKnowledgeCatalog.ForRole(profile.Key)
                .FirstOrDefault(entry => entry.Id == "save-error-help");
            if (saveError is not null)
            {
                if (!string.IsNullOrWhiteSpace(conversationKey))
                    RememberConversation(conversationKey, message, saveError.Id);
                return CreateResponse(
                    profile,
                    userName,
                    FormatSteps(saveError),
                    ["Revisar campos obligatorios", "Crear ticket de soporte"],
                    [new ChatbotAction { Label = "Crear ticket de soporte", Type = "ticket-form" }],
                    [ToSource(saveError)]);
            }
        }

        if (ContainsAny(query, "exporto un documento", "exportar un documento", "formato no documentado",
            "exportar documento", "exportar a formato") ||
            ((ContainsAny(query, "exportar", "exporto", "exportacion") && ContainsAny(query, "formato", "documento"))
             && ContainsAny(query, "no documentado", "no esta documentado", "no disponible", "cual formato", "que formato", "xyz")))
        {
            return NoVerifiedAnswer(profile, userName, context);
        }

        if (IsCurrentViewQuestion(query))
        {
            if (context is null)
                return UnknownViewResponse(profile, userName);

            return KnowledgeResponse(
                profile,
                userName,
                context,
                safePath,
                false,
                $"Esta vista se llama «{context.Title}» y pertenece a {context.Module}. {context.Summary}");
        }

        var results = Search(query, profile.Key, context?.Path);
        var otherRoleMatch = FindBestOtherRoleMatch(query, profile.Key, context?.Path);
        if (otherRoleMatch is not null)
        {
            var topInRole = results.FirstOrDefault();
            var inRoleIsWeak = topInRole is null ||
                               (topInRole.Score < otherRoleMatch.Score * 0.55 && topInRole.Score < 1.2);
            var inRoleTopicallyUnrelated = topInRole is not null &&
                                           !IsTopicallyRelated(topInRole.Entry, query) &&
                                           topInRole.Score < otherRoleMatch.Score * 0.85;
            if (inRoleIsWeak || inRoleTopicallyUnrelated)
            {
                if (!string.IsNullOrWhiteSpace(conversationKey))
                    RememberConversation(conversationKey, message, otherRoleMatch.Entry.Id);
                return OtherRoleResponse(profile, userName, otherRoleMatch.Entry);
            }
        }

        if (results.Count == 0)
        {
            if (!string.IsNullOrWhiteSpace(conversationKey))
                RememberConversation(conversationKey, message, "no-verified-answer");
            return NoVerifiedAnswer(profile, userName, context);
        }

        var authoritative = results.FirstOrDefault(result => MatchesCataloguedQuestion(result.Entry, query));
        var top = authoritative ?? results[0];
        var second = results.FirstOrDefault(result => result.Entry.Id != top.Entry.Id);
        if (authoritative is null && second is not null && IsAmbiguous(top, second))
        {
            if (!string.IsNullOrWhiteSpace(conversationKey))
                RememberConversation(conversationKey, message, top.Entry.Id);
            return ClarificationResponse(profile, userName, results.Take(3).ToArray());
        }

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
            results.Where(result => result.Entry.Id != top.Entry.Id).Take(2).Select(result => result.Entry).ToArray());
    }

    public void ResetConversation(string conversationKey)
    {
        if (!string.IsNullOrWhiteSpace(conversationKey))
            ConversationStates.TryRemove(conversationKey, out _);
    }

    internal ConversationState? GetConversationState(string conversationKey)
    {
        if (!ConversationStates.TryGetValue(conversationKey, out var state))
            return null;

        if (_timeProvider.GetUtcNow() - state.LastTouched >
            TimeSpan.FromMinutes(_options.ConversationRetentionMinutes))
        {
            ConversationStates.TryRemove(conversationKey, out _);
            return null;
        }
        return state;
    }

    internal void PurgeConversationStates()
    {
        var cutoff = _timeProvider.GetUtcNow() -
                     TimeSpan.FromMinutes(_options.ConversationRetentionMinutes);
        foreach (var item in ConversationStates)
        {
            if (item.Value is null || item.Value.LastTouched < cutoff)
                ConversationStates.TryRemove(item.Key, out _);
        }

        var overflow = ConversationStates.Count - _options.MaxConversationStates;
        if (overflow <= 0)
            return;

        foreach (var item in ConversationStates
                     .Where(entry => entry.Value is not null)
                     .OrderBy(entry => entry.Value!.LastTouched)
                     .Take(overflow))
        {
            ConversationStates.TryRemove(item.Key, out _);
        }
    }

    private void RememberConversation(string conversationKey, string query, string entryId)
    {
        var now = _timeProvider.GetUtcNow();
        // El estado se reemplaza por una copia inmutable en cada actualización para
        // evitar mutar Queue<ConversationMessage> en caliente bajo AddOrUpdate concurrente.
        ConversationStates.AddOrUpdate(
            conversationKey,
            _ => new ConversationState(
                new Queue<ConversationMessage>([new ConversationMessage(query, entryId)]),
                entryId,
                now),
            (_, existing) =>
            {
                var messages = new Queue<ConversationMessage>(existing.Messages);
                messages.Enqueue(new ConversationMessage(query, entryId));
                while (messages.Count > MaxConversationMessages)
                    messages.Dequeue();
                return new ConversationState(messages, entryId, now);
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
            "Puedo ayudarte a registrar un ticket desde este asistente. Describe el problema sin incluir contraseñas ni datos clínicos y puedes adjuntar evidencia en el formulario.",
            ["Crear ticket de soporte", "Adjuntar evidencia"],
            [
                new ChatbotAction { Label = "Crear ticket aquí", Type = "ticket-form" }
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
            [new ChatbotAction { Label = "Crear ticket de soporte", Type = "ticket-form" }],
            []);

    private static ChatbotResponse NegationResponse(
        RoleProfile profile,
        string? userName,
        ChatbotKnowledgeEntry? context)
    {
        var actions = new List<ChatbotAction>();
        if (context is not null)
            actions.Add(CreateLink(context.Title, context.Path));

        return CreateResponse(
            profile,
            userName,
            "Entendido, no realizaré esa acción. Si necesitas algo distinto, puedo buscar una guía verificada, explicar la vista actual o consultar tus tickets existentes.",
            ["Buscar guías", "Consultar mis tickets"],
            actions,
            []);
    }

    private static ChatbotResponse ClarificationResponse(
        RoleProfile profile,
        string? userName,
        IReadOnlyList<SearchResult> results)
    {
        var suggestions = results.Select(result => result.Entry.Title).Distinct().ToArray();
        var actions = results
            .Select(result => new
            {
                result.Entry,
                Path = ResolveEntryPath(result.Entry, profile)
            })
            .Where(item => ChatbotKnowledgeCatalog.NormalizePath(item.Path) is not null)
            .Select(item => CreateLink(item.Entry.Title, item.Path))
            .DistinctBy(action => action.Url, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return CreateResponse(
            profile,
            userName,
            $"Encontré varias opciones parecidas. ¿Cuál necesitas? {string.Join(" / ", suggestions)}.",
            suggestions,
            actions,
            GetSources(profile, results.Select(result => result.Entry)),
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
        var entryPath = ResolveEntryPath(entry, profile);
        if (ChatbotKnowledgeCatalog.NormalizePath(entryPath) is not null &&
            !string.Equals(
                ChatbotKnowledgeCatalog.NormalizePath(currentPath),
                ChatbotKnowledgeCatalog.NormalizePath(entryPath),
                StringComparison.OrdinalIgnoreCase))
        {
            actions.Add(CreateLink(
                entry.Id == "appointment-guide-steps" ? "Abrir agenda" : entry.Title,
                entryPath));
        }

        if (entry.Id == "support-faq-attachments")
            actions.Add(new ChatbotAction { Label = "Crear ticket aquí", Type = "ticket-form" });
        else if (entry.Id == "support-ticket-status")
            actions.Add(new ChatbotAction { Label = "Consultar mis tickets", Type = "ticket-list" });

        foreach (var additional in additionalEntries ?? [])
        {
            var additionalPath = ResolveEntryPath(additional, profile);
            if (ChatbotKnowledgeCatalog.NormalizePath(additionalPath) is not null &&
                !string.Equals(additionalPath, entryPath, StringComparison.OrdinalIgnoreCase))
                actions.Add(CreateLink(additional.Title, additionalPath));
        }

        return CreateResponse(
            profile,
            userName,
            intro,
            additionalEntries?.Select(item => item.Title).ToArray() ?? [],
            actions,
            GetSources(profile, new[] { entry }.Concat(additionalEntries ?? [])),
            needsClarification);
    }

    private static string ResolveEntryPath(ChatbotKnowledgeEntry entry, RoleProfile profile) =>
        entry.Id == "appointment-guide-steps"
            ? profile.Key switch
            {
                "administrador" => "/gestion-de-citas/st-adm-08-agenda",
                "recepcionista" => "/gestion-de-citas/st-rec-03-gestion-citas",
                _ => string.Empty
            }
            : entry.Path;

    private static ChatbotKnowledgeSource[] GetSources(
        RoleProfile profile,
        IEnumerable<ChatbotKnowledgeEntry> entries) =>
        entries
            .Select(entry => new ChatbotKnowledgeSource
            {
                Id = entry.Id,
                Title = entry.Title,
                Url = ResolveEntryPath(entry, profile)
            })
            .Where(source => ChatbotKnowledgeCatalog.NormalizePath(source.Url) is not null)
            .DistinctBy(source => source.Id)
            .ToArray();

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
        var titleMatches = CountMatches(Tokenize(entry.Title), queryTokens);
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

    private static bool IsActionNegation(string message) =>
        ContainsAny(message, "no quiero", "no deseo", "no necesito", "no me interesa", "no por ahora") &&
        ContainsAny(message, "ticket", "cita", "cancelar", "anular", "crear", "programar",
            "agendar", "reservar", "eliminar", "borrar", "modificar", "reportar", "problema",
            "soporte", "abrir", "enviar", "solicitar");

    private static bool MatchesCataloguedQuestion(ChatbotKnowledgeEntry entry, string query)
    {
        var normalizedQuery = Normalize(query);
        return entry.Questions.Any(question =>
            string.Equals(Normalize(question), normalizedQuery, StringComparison.Ordinal));
    }

    private static bool IsTicketTrackingQuestion(string message) =>
        ContainsAny(message, "mis tickets", "mi ticket", "estado del ticket",
            "estado de mi ticket", "consultar ticket", "consultar mis tickets", "ver mis tickets",
            "seguimiento de ticket", "seguimiento del ticket", "seguimiento de mi ticket",
            "seguimiento de solicitud", "seguimiento de soporte", "seguimiento del caso") ||
        (ContainsAny(message, "seguimiento", "estado") && ContainsAny(message, "ticket", "solicitud", "soporte", "caso"));

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
        if (left.Length < 6 || right.Length < 6)
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

    private static bool IsTopicallyRelated(ChatbotKnowledgeEntry entry, string query)
    {
        var generic = new HashSet<string>(StringComparer.Ordinal)
        {
            "como", "donde", "puedo", "quiero", "necesito", "hacer", "ver", "tengo",
            "debo", "para", "con", "esta", "este", "del", "los", "las", "una", "unos",
            "unas", "por", "que", "hay", "ser", "deberia", "corresponde", "admin",
            "administrador", "recepcionista", "profesional", "auxiliar", "paciente",
            "gestionar", "gestion", "usar", "consultar", "abrir"
        };

        var queryTokens = Tokenize(query)
            .Where(token => token.Length >= 4 && !generic.Contains(token))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (queryTokens.Length == 0)
            return false;

        var entryTokens = Tokenize(string.Join(" ",
                new[] { entry.Title, entry.Summary, entry.Module }
                    .Concat(entry.Keywords)
                    .Concat(entry.Questions)))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return queryTokens.Any(queryToken =>
            entryTokens.Any(entryToken =>
                entryToken == queryToken ||
                (queryToken.Length >= 4 && entryToken.Length >= 4 &&
                 (entryToken.StartsWith(queryToken, StringComparison.Ordinal) ||
                  queryToken.StartsWith(entryToken, StringComparison.Ordinal))) ||
                IsReasonableFuzzyMatch(entryToken, queryToken)));
    }

    private static ChatbotResponse OtherRoleResponse(
        RoleProfile profile,
        string? userName,
        ChatbotKnowledgeEntry entry)
    {
        var allowedRoles = string.Join(", ", entry.Roles);
        var message =
            $"No encuentro una guía verificada para tu rol {profile.Label}. " +
            $"La funcionalidad consultada corresponde a un rol diferente: {allowedRoles}. " +
            "Puedo orientarte con tareas de tu rol o ayudarte a crear un ticket.";

        var actions = new List<ChatbotAction>
        {
            new() { Label = "Crear ticket de soporte", Type = "ticket-form" }
        };

        return CreateResponse(
            profile,
            userName,
            message,
            ["Buscar guías", "Crear ticket de soporte"],
            actions,
            []);
    }

    private SearchResult? FindBestOtherRoleMatch(string query, string role, string? currentPath)
    {
        var originalTokens = Tokenize(query).ToHashSet(StringComparer.Ordinal);
        var queryTokens = ExpandTokens(originalTokens);
        if (queryTokens.Count == 0)
            return null;

        return ChatbotKnowledgeCatalog.EntriesSnapshot
            .Where(entry => !entry.Roles.Contains(role, StringComparer.OrdinalIgnoreCase))
            .Select(entry => new SearchResult(entry, Score(entry, query, queryTokens, originalTokens.Count, currentPath)))
            .Where(result => result.Score >= _options.HighRelevanceThreshold)
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.Entry.Title, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }
}
