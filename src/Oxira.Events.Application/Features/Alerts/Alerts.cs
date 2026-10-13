using System.Security.Cryptography;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Oxira.Events.Application.Common;
using Oxira.Events.Application.Emails;
using Oxira.Events.Domain;
using static Oxira.Events.Domain.Text;

namespace Oxira.Events.Application.Features.Alerts;

public record SubscribeAlertsCommand(AlertsInput Input) : ICommand<SuccessResult>;
public record UnsubscribeCommand(string Token) : ICommand<bool>;
/// <summary>the scheduled job: email each subscriber about shows reaching ~60 and ~21 days ahead</summary>
public record SendDueAlertsCommand : ICommand<int>;
public record ListSubscribersQuery(bool ActiveOnly = true) : IQuery<List<SubscriberDto>>;

public class AlertHandlers(IAppDbContext db, TimeProvider clock, IEmailSender email, IExhibitionFeed feed,
    IOptions<SiteOptions> site, IOptions<AlertOptions> opt, ILogger<AlertHandlers> log)
    : IRequestHandler<SubscribeAlertsCommand, SuccessResult>, IRequestHandler<UnsubscribeCommand, bool>,
      IRequestHandler<SendDueAlertsCommand, int>, IRequestHandler<ListSubscribersQuery, List<SubscriberDto>>
{
    private string UnsubUrl(AlertSubscriber s) => $"{site.Value.ApiUrl.TrimEnd('/')}/api/alerts/unsubscribe?t={s.Token}";

    public async Task<SuccessResult> Handle(SubscribeAlertsCommand cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        if (!string.IsNullOrEmpty(i.CompanyWebsite)) return new SuccessResult();
        var mail = Clip(i.Email, 160).ToLowerInvariant();
        var sectors = (i.Sectors ?? []).Where(Sectors.All.Contains).Distinct().ToList();
        if (!IsEmail(mail) || sectors.Count == 0) throw new DomainException("أدخل بريدًا صحيحًا واختر مجالًا واحدًا على الأقل", "invalid");

        var s = await db.AlertSubscribers.FirstOrDefaultAsync(x => x.Email == mail, ct);
        var isNew = s == null || s.UnsubscribedAt != null;
        if (s == null)
        {
            s = new AlertSubscriber { Email = mail, Token = RandomNumberGenerator.GetHexString(32, lowercase: true), CreatedAt = clock.GetUtcNow() };
            db.AlertSubscribers.Add(s);
        }
        s.Sectors = string.Join(',', sectors);
        s.Lang = Langs.Of(i.Lang);
        s.Source = Map.Source(i.Source);
        s.UnsubscribedAt = null;
        await db.SaveChangesAsync(ct);
        if (isNew) email.Enqueue(Templates.AlertWelcome(s, UnsubUrl(s)));
        return new SuccessResult();
    }

    public async Task<bool> Handle(UnsubscribeCommand cmd, CancellationToken ct)
    {
        if (cmd.Token.Length != 32) return false;
        var s = await db.AlertSubscribers.FirstOrDefaultAsync(x => x.Token == cmd.Token, ct);
        if (s == null) return false;
        s.UnsubscribedAt ??= clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> Handle(SendDueAlertsCommand cmd, CancellationToken ct)
    {
        var now = Riyadh.Now(clock);
        if (now.Hour < opt.Value.FromHour || now.Hour >= opt.Value.ToHour) return 0;
        var today = DateOnly.FromDateTime(now.DateTime);
        var shows = (await feed.GetAsync(ct)).Where(e => e.Start > today).ToList();
        if (shows.Count == 0) return 0;

        int StageOf(Exhibition e)
        {
            var days = e.Start.DayNumber - today.DayNumber;
            return days <= opt.Value.SecondDays ? 21 : days <= opt.Value.FirstDays ? 60 : 0;
        }
        var due = shows.Select(e => (Show: e, Stage: StageOf(e))).Where(x => x.Stage > 0).ToList();
        if (due.Count == 0) return 0;

        var keys = due.Select(x => x.Show.Key).ToList();
        var subscribers = await db.AlertSubscribers.Where(s => s.UnsubscribedAt == null).ToListAsync(ct);
        var sent = (await db.AlertsSent.Where(a => keys.Contains(a.EventKey)).Select(a => new { a.SubscriberId, a.EventKey, a.Stage }).ToListAsync(ct))
            .Select(a => (a.SubscriberId, a.EventKey, a.Stage)).ToHashSet();

        var emails = 0;
        foreach (var s in subscribers)
        {
            var mine = s.Sectors.Split(',');
            // a late subscriber who missed the first stage only gets the stage that is due now
            var pick = due.Where(x => mine.Contains(x.Show.Sector) && !sent.Contains((s.Id, x.Show.Key, x.Stage))
                && !(x.Stage == 60 && sent.Contains((s.Id, x.Show.Key, 21)))).ToList();
            if (pick.Count == 0) continue;
            foreach (var x in pick) db.AlertsSent.Add(new AlertSent { SubscriberId = s.Id, EventKey = x.Show.Key, Stage = x.Stage, SentAt = clock.GetUtcNow() });
            await db.SaveChangesAsync(ct); // recorded first: a crash can skip an alert, never send it twice
            email.Enqueue(Templates.Alert(s, pick, site.Value.SiteUrl.TrimEnd('/'), UnsubUrl(s)));
            emails++;
        }
        if (emails > 0) log.LogInformation("Exhibition alerts: {Count} emails", emails);
        return emails;
    }

    public async Task<List<SubscriberDto>> Handle(ListSubscribersQuery q, CancellationToken ct)
    {
        var query = db.AlertSubscribers.AsNoTracking();
        if (q.ActiveOnly) query = query.Where(s => s.UnsubscribedAt == null);
        return (await query.OrderByDescending(s => s.CreatedAt).Take(2000).ToListAsync(ct)).Select(Map.Subscriber).ToList();
    }
}
