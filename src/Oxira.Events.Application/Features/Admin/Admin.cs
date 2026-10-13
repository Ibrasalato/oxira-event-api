using MediatR;
using Microsoft.EntityFrameworkCore;
using Oxira.Events.Application.Common;
using Oxira.Events.Domain;

namespace Oxira.Events.Application.Features.Admin;

public record GetOverviewQuery : IQuery<OverviewDto>;

public class AdminHandlers(IAppDbContext db, TimeProvider clock, IQuota quota) : IRequestHandler<GetOverviewQuery, OverviewDto>
{
    public async Task<OverviewDto> Handle(GetOverviewQuery q, CancellationToken ct)
    {
        var now = Riyadh.Now(clock);
        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, Riyadh.Offset);
        var since = clock.GetUtcNow();
        var sources = await db.Leads.AsNoTracking().Where(l => l.CreatedAt >= monthStart).Select(l => l.Source.Source).ToListAsync(ct);
        var bySource = sources.GroupBy(x => string.IsNullOrEmpty(x) ? "direct" : x).Select(g => new CountDto(g.Key, g.Count()))
            .OrderByDescending(x => x.Count).Take(10).ToList();
        return new OverviewDto(
            await db.Leads.CountAsync(l => l.Status == LeadStatuses.New, ct),
            await db.Leads.CountAsync(l => l.CreatedAt >= monthStart, ct),
            await db.Bookings.CountAsync(b => b.StartsAt >= since && b.Status != BookingStatuses.Cancelled, ct),
            await db.AlertSubscribers.CountAsync(s => s.UnsubscribedAt == null, ct),
            await quota.UsedAsync("render:all", ct),
            bySource);
    }
}
