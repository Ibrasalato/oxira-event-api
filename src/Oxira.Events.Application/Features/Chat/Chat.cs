using System.Text.Json;
using System.Text.Json.Nodes;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Oxira.Events.Application.Common;
using Oxira.Events.Application.Emails;
using Oxira.Events.Application.Features.Leads;
using Oxira.Events.Domain;
using static Oxira.Events.Domain.Text;

namespace Oxira.Events.Application.Features.Chat;

public record SendChatCommand(ChatInput Input) : ICommand<ChatReply>;
public record GetChatQuery(string SessionId) : IQuery<List<ChatTurnDto>>;
public record ChatTurnDto(string Role, string Content, DateTimeOffset At);

/// <summary>
/// The website assistant: Claude with one tool (create_lead). Memory is the last visible turns of the session.
/// When it saves a lead the team is emailed by the server (not by the model), so the email is always complete.
/// </summary>
public class ChatHandlers(IAppDbContext db, TimeProvider clock, IAnthropic ai, ISender mediator, IQuota quota, IEmailSender email,
    IOptions<AiOptions> opt, IOptions<SiteOptions> site, ILogger<ChatHandlers> log)
    : IRequestHandler<SendChatCommand, ChatReply>, IRequestHandler<GetChatQuery, List<ChatTurnDto>>
{
    public const string Sorry = "المعذرة، صار خلل بسيط عندنا. جرّب مرة ثانية بعد لحظات، أو راسلنا على واتساب +966 56 567 0776.";

    private static readonly JsonArray Tools =
    [
        new JsonObject
        {
            ["name"] = "create_lead",
            ["description"] = "Save a new request from an Oxira Events website visitor. Call ONCE per request, only after the visitor confirmed the summary. Returns the request id to give the visitor; the team is notified automatically.",
            ["input_schema"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["contact_name"] = Str("Visitor full name as they gave it"),
                    ["phone"] = Str("Visitor mobile number with country code, digits only"),
                    ["email"] = Str("Visitor email, or empty string"),
                    ["company"] = Str("Company or organisation name, or empty string"),
                    ["service"] = Str("Requested service(s): exhibition stand, conference stage, media production, lighting and sound, giveaways, printing, interior design"),
                    ["event_name"] = Str("Exhibition / conference / event name, or empty string"),
                    ["event_date"] = Str("Event date or period as the visitor said it, or empty string"),
                    ["city"] = Str("City / country, or empty string"),
                    ["stand_size"] = Str("Stand or space size in square metres, or empty string"),
                    ["needs"] = Str("Arabic summary of what the visitor needs"),
                    ["preferred_contact"] = Str("How and when the visitor prefers to be contacted, or empty string"),
                },
                ["required"] = new JsonArray("contact_name", "phone", "needs"),
            },
        },
    ];

    private static JsonObject Str(string d) => new() { ["type"] = "string", ["description"] = d };

    public async Task<ChatReply> Handle(SendChatCommand cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var sid = SessionKey(i.SessionId);
        var text = Clip(i.Message, 2000);
        if (sid.Length < 6 || text == "") throw new DomainException("missing message", "missing");
        var lang = Langs.Of(i.Lang);
        var page = Clip(i.Page, 300);

        if (await quota.TryConsumeAsync($"chat:s:{sid}", opt.Value.MessagesPerSession, ct) == null ||
            await quota.TryConsumeAsync("chat:all", opt.Value.MessagesPerDay, ct) == null)
            return new ChatReply(false, "وصلت للحد اليومي للمحادثة. راسلنا على واتساب +966 56 567 0776 ونخدمك مباشرة.");

        var history = await db.ChatMessages.AsNoTracking().Where(m => m.SessionId == sid)
            .OrderByDescending(m => m.Id).Take(opt.Value.Memory).ToListAsync(ct);
        history.Reverse();
        var messages = new JsonArray();
        foreach (var m in history.SkipWhile(m => m.Role != "user")) messages.Add(new JsonObject { ["role"] = m.Role, ["content"] = m.Content });
        messages.Add(new JsonObject { ["role"] = "user", ["content"] = text });

        var now = clock.GetUtcNow();
        db.ChatMessages.Add(new ChatMessage { SessionId = sid, Role = "user", Content = text, Lang = lang, Page = page, CreatedAt = now });

        string reply;
        bool ok = true;
        try { reply = await RunAgentAsync(Prompts.System(Riyadh.Now(clock), lang), messages, sid, lang, page, i.Source, ct); }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            log.LogError(e, "Chat agent failed for {Session}", sid);
            reply = Sorry;
            ok = false;
        }
        reply = Clip(reply, 4000);
        if (reply == "") { reply = Sorry; ok = false; }
        if (ok) db.ChatMessages.Add(new ChatMessage { SessionId = sid, Role = "assistant", Content = reply, Lang = lang, Page = page, CreatedAt = clock.GetUtcNow() });
        await db.SaveChangesAsync(ct);
        return new ChatReply(ok, reply);
    }

    private async Task<string> RunAgentAsync(string system, JsonArray messages, string sid, string lang, string page, JsonElement? source, CancellationToken ct)
    {
        var leadSaved = false;
        for (var step = 0; step < opt.Value.MaxIterations; step++)
        {
            var res = await ai.CreateMessageAsync(new AiRequest(system, messages, Tools, opt.Value.MaxTokens, opt.Value.Model), ct);
            var uses = res.Content.OfType<JsonObject>().Where(b => (string?)b["type"] == "tool_use").ToList();
            var textOut = string.Join("\n", res.Content.OfType<JsonObject>().Where(b => (string?)b["type"] == "text").Select(b => (string?)b["text"]));
            if (res.StopReason != "tool_use" || uses.Count == 0) return textOut.Trim();

            messages.Add(new JsonObject { ["role"] = "assistant", ["content"] = res.Content.DeepClone() });
            var results = new JsonArray();
            foreach (var u in uses)
            {
                string output;
                if ((string?)u["name"] == "create_lead" && !leadSaved)
                {
                    var id = await SaveLeadAsync(u["input"] as JsonObject ?? [], lang, page, source, ct);
                    leadSaved = true;
                    output = JsonSerializer.Serialize(new { success = true, id });
                }
                else output = JsonSerializer.Serialize(new { success = false, error = leadSaved ? "already saved in this reply" : "unknown tool" });
                results.Add(new JsonObject { ["type"] = "tool_result", ["tool_use_id"] = (string?)u["id"], ["content"] = output });
            }
            messages.Add(new JsonObject { ["role"] = "user", ["content"] = results });
        }
        return "";
    }

    private async Task<int> SaveLeadAsync(JsonObject input, string lang, string page, JsonElement? source, CancellationToken ct)
    {
        string F(string k, int max) => Clip(input[k]?.GetValueKind() == JsonValueKind.String ? (string?)input[k] : input[k]?.ToJsonString(), max);
        var lead = new Lead
        {
            ContactName = F("contact_name", 120), Phone = F("phone", 40), Email = F("email", 160), Company = F("company", 160),
            Service = F("service", 160), EventName = F("event_name", 160), EventDate = F("event_date", 80), City = F("city", 120),
            StandSize = F("stand_size", 40), Needs = F("needs", 5000), PreferredContact = F("preferred_contact", 200),
            Channel = LeadChannels.Chat, Lang = lang, Page = page, Source = Map.Source(source),
        };
        var id = await mediator.Send(new CreateChatLeadCommand(lead), ct);
        email.Enqueue(Templates.TeamQuote(lead, site.Value.TeamEmail, []));
        return id;
    }

    public async Task<List<ChatTurnDto>> Handle(GetChatQuery q, CancellationToken ct)
    {
        var sid = SessionKey(q.SessionId);
        return await db.ChatMessages.AsNoTracking().Where(m => m.SessionId == sid).OrderBy(m => m.Id)
            .Select(m => new ChatTurnDto(m.Role, m.Content, m.CreatedAt)).ToListAsync(ct);
    }
}
