using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Oxira.Events.Application.Common;
using Oxira.Events.Application.Emails;
using Oxira.Events.Domain;
using static Oxira.Events.Domain.Text;

namespace Oxira.Events.Application.Features.Leads;

// ───── messages ─────
public record SubmitQuoteCommand(QuoteInput Input) : ICommand<SubmitResult>;
/// <summary>used by the chat agent's create_lead tool</summary>
public record CreateChatLeadCommand(Lead Lead) : ICommand<int>;
public record ListLeadsQuery(string? Status, string? Q, int Page = 1, int Size = 30) : IQuery<PageOf<LeadSummaryDto>>;
public record GetLeadQuery(int Id) : IQuery<LeadDto>;
public record UpdateLeadCommand(int Id, string? Status, string? StaffNote) : ICommand<LeadDto>;
public record GetLeadFileQuery(int LeadId, int FileId) : IQuery<FileContent>;

// ───── handlers ─────
public class LeadHandlers(IAppDbContext db, TimeProvider clock, IEmailSender email, IRealtime realtime, IOptions<SiteOptions> site)
    : IRequestHandler<SubmitQuoteCommand, SubmitResult>, IRequestHandler<CreateChatLeadCommand, int>,
      IRequestHandler<ListLeadsQuery, PageOf<LeadSummaryDto>>, IRequestHandler<GetLeadQuery, LeadDto>,
      IRequestHandler<UpdateLeadCommand, LeadDto>, IRequestHandler<GetLeadFileQuery, FileContent>
{
    /// <summary>total base64 the site sends at most (it drops the OBJ, then the GLB, above 7,000,000 chars)</summary>
    private const int MaxBase64 = 7_500_000;
    private static readonly Dictionary<string, string> AllowedMime = new()
    {
        ["image/jpeg"] = ".jpg", ["image/png"] = ".png", ["model/gltf-binary"] = ".glb", ["text/plain"] = ".obj", ["application/pdf"] = ".pdf",
    };

    public async Task<SubmitResult> Handle(SubmitQuoteCommand cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        if (!string.IsNullOrEmpty(i.CompanyWebsite)) return new SubmitResult(true, 0); // honeypot: pretend success, store nothing
        var now = clock.GetUtcNow();
        var lead = new Lead
        {
            ContactName = Clip(i.Name, 120), Company = Clip(i.Company, 160), Phone = Clip(i.Phone, 40), Email = Clip(i.Email, 160),
            Service = Clip(i.Service, 160), EventName = Clip(i.EventName, 160), EventDate = Clip(i.EventDate, 80), StandSize = Clip(i.StandSize, 40),
            Needs = Clip(i.Details, 5000), Channel = LeadChannels.Form, Lang = Langs.Of(i.Lang), Page = Clip(i.Page, 300),
            Source = Map.Source(i.Source), CreatedAt = now, UpdatedAt = now,
        };
        if (lead.ContactName == "" || lead.Needs == "" || (lead.Phone == "" && lead.Email == ""))
            throw new DomainException("أدخل الاسم والتفاصيل ورقم الجوال أو البريد", "missing");
        if (lead.Email != "" && !IsEmail(lead.Email)) throw new DomainException("البريد الإلكتروني غير صحيح", "email");
        if (i.Design is { ValueKind: JsonValueKind.Object } d && d.GetRawText().Length <= 4000) lead.DesignJson = d.GetRawText();
        lead.Files = Files(i.Files);

        db.Leads.Add(lead);
        await db.SaveChangesAsync(ct);

        var attachments = lead.Files.Select(f => new EmailAttachment(f.Name, f.Mime, f.Content, f.Role == "preview" ? "preview" : null)).ToList();
        email.Enqueue(Templates.TeamQuote(lead, site.Value.TeamEmail, attachments));
        if (IsEmail(lead.Email)) email.Enqueue(Templates.ClientQuote(lead, attachments.FirstOrDefault(a => a.ContentId != null), site.Value.WhatsApp));
        await realtime.LeadCreatedAsync(Map.Summary(lead));
        return new SubmitResult(true, lead.Id);
    }

    private static List<LeadFile> Files(List<FileInput>? files)
    {
        if (files is not { Count: > 0 }) return [];
        if (files.Count > 5 || files.Sum(f => f.B64?.Length ?? 0) > MaxBase64) throw new DomainException("حجم الملفات كبير جدًا", "too_big", 413);
        var list = new List<LeadFile>();
        foreach (var f in files)
        {
            var mime = (f.Mime ?? "").ToLowerInvariant();
            if (!AllowedMime.TryGetValue(mime, out var ext) || string.IsNullOrEmpty(f.B64)) continue;
            byte[] bytes;
            try { bytes = Convert.FromBase64String(f.B64); } catch (FormatException) { throw new DomainException("ملف تالف", "file"); }
            var name = Clip(Path.GetFileName(f.Name ?? ""), 120);
            if (name == "" || !name.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) name = $"oxira-stand{ext}";
            list.Add(new LeadFile { Name = name, Mime = mime, Role = f.Role == "preview" ? "preview" : "model", Size = bytes.Length, Content = bytes });
        }
        return list;
    }

    public async Task<int> Handle(CreateChatLeadCommand cmd, CancellationToken ct)
    {
        var l = cmd.Lead;
        l.CreatedAt = l.UpdatedAt = clock.GetUtcNow();
        db.Leads.Add(l);
        await db.SaveChangesAsync(ct);
        await realtime.LeadCreatedAsync(Map.Summary(l));
        return l.Id;
    }

    public async Task<PageOf<LeadSummaryDto>> Handle(ListLeadsQuery q, CancellationToken ct)
    {
        var size = Math.Clamp(q.Size, 1, 100);
        var page = Math.Max(1, q.Page);
        var query = db.Leads.AsNoTracking();
        if (!string.IsNullOrEmpty(q.Status)) query = query.Where(l => l.Status == q.Status);
        if (!string.IsNullOrWhiteSpace(q.Q))
        {
            var s = q.Q.Trim();
            query = int.TryParse(s.TrimStart('#'), out var id)
                ? query.Where(l => l.Id == id || l.Phone.Contains(s))
                : query.Where(l => l.ContactName.Contains(s) || l.Company.Contains(s) || l.Email.Contains(s) || l.EventName.Contains(s));
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(l => l.Id).Skip((page - 1) * size).Take(size)
            .Select(l => new LeadSummaryDto(l.Id, l.ContactName, l.Phone, l.Email, l.Company, l.Service, l.EventName, l.StandSize, l.Channel, l.Status,
                l.Lang, l.Source.Source, l.Source.Medium, l.DesignJson != null, l.Files.Count, l.CreatedAt))
            .ToListAsync(ct);
        return new PageOf<LeadSummaryDto>(items, total, page, size);
    }

    /// <summary>the lead with its files' metadata (file contents stay in the database until downloaded)</summary>
    private async Task<Lead> LoadAsync(int id, CancellationToken ct)
    {
        var l = await db.Leads.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw DomainException.NotFound("الطلب غير موجود");
        l.Files = await db.LeadFiles.AsNoTracking().Where(f => f.LeadId == id).OrderBy(f => f.Id)
            .Select(f => new LeadFile { Id = f.Id, LeadId = f.LeadId, Name = f.Name, Mime = f.Mime, Role = f.Role, Size = f.Size }).ToListAsync(ct);
        return l;
    }

    public async Task<LeadDto> Handle(GetLeadQuery q, CancellationToken ct) => Map.Lead(await LoadAsync(q.Id, ct));

    public async Task<LeadDto> Handle(UpdateLeadCommand cmd, CancellationToken ct)
    {
        var l = await db.Leads.FirstOrDefaultAsync(x => x.Id == cmd.Id, ct) ?? throw DomainException.NotFound("الطلب غير موجود");
        if (cmd.Status != null)
        {
            if (!LeadStatuses.All.Contains(cmd.Status)) throw new DomainException("حالة غير معروفة");
            l.Status = cmd.Status;
        }
        if (cmd.StaffNote != null) l.StaffNote = Clip(cmd.StaffNote, 4000);
        l.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return Map.Lead(await LoadAsync(l.Id, ct));
    }

    public async Task<FileContent> Handle(GetLeadFileQuery q, CancellationToken ct)
    {
        var f = await db.LeadFiles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == q.FileId && x.LeadId == q.LeadId, ct)
            ?? throw DomainException.NotFound("الملف غير موجود");
        return new FileContent(f.Name, f.Mime, f.Content);
    }
}
