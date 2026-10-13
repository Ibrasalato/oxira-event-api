using System.Globalization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Oxira.Events.Application.Common;
using Oxira.Events.Application.Emails;
using Oxira.Events.Domain;
using static Oxira.Events.Domain.Text;

namespace Oxira.Events.Application.Features.Bookings;

public record CreateBookingCommand(BookingInput Input) : ICommand<SubmitResult>;
/// <summary>slots already taken between two dates (Riyadh), "yyyy-MM-ddTHH:mm"</summary>
public record GetTakenSlotsQuery(DateOnly? From, DateOnly? To) : IQuery<SlotsResult>;
public record ListBookingsQuery(bool Upcoming = true, string? Status = null) : IQuery<List<BookingDto>>;
public record UpdateBookingCommand(int Id, string Status) : ICommand<BookingDto>;

public class BookingHandlers(IAppDbContext db, TimeProvider clock, IEmailSender email, IRealtime realtime, IOptions<SiteOptions> site)
    : IRequestHandler<CreateBookingCommand, SubmitResult>, IRequestHandler<GetTakenSlotsQuery, SlotsResult>,
      IRequestHandler<ListBookingsQuery, List<BookingDto>>, IRequestHandler<UpdateBookingCommand, BookingDto>
{
    public async Task<SubmitResult> Handle(CreateBookingCommand cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        if (!string.IsNullOrEmpty(i.CompanyWebsite)) return new SubmitResult(true, 0);
        var name = Clip(i.Name, 120);
        var phone = Clip(i.Phone, 40);
        if (name == "" || !IsPhone(phone)) throw new DomainException("أدخل الاسم ورقم الجوال", "missing");
        var mail = Clip(i.Email, 160);
        if (mail != "" && !IsEmail(mail)) throw new DomainException("البريد الإلكتروني غير صحيح", "email");
        if (!DateOnly.TryParseExact(i.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ||
            !TimeOnly.TryParseExact(i.Time, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
            throw new DomainException("اختر اليوم والوقت", "slot");
        var problem = BookingRules.Check(date, time, Riyadh.Now(clock));
        if (problem != null) throw new DomainException(problem, "slot");
        var start = new DateTimeOffset(date.ToDateTime(time), Riyadh.Offset);
        // the filtered unique index (StartsAt where not cancelled) also guards against two people taking the same slot at once
        if (await db.Bookings.AnyAsync(b => b.StartsAt == start && b.Status != BookingStatuses.Cancelled, ct))
            throw new DomainException("هذا الموعد محجوز، اختر وقتًا آخر", "taken", 409);

        var b = new Booking
        {
            ContactName = name, Phone = phone, Email = mail, Company = Clip(i.Company, 160), Topic = Clip(i.Topic, 2000),
            Channel = BookingChannels.All.Contains(i.Channel) ? i.Channel! : BookingChannels.Call, StartsAt = start,
            Lang = Langs.Of(i.Lang), Page = Clip(i.Page, 300), Source = Map.Source(i.Source), CreatedAt = clock.GetUtcNow(),
        };
        db.Bookings.Add(b);
        await db.SaveChangesAsync(ct);

        var invite = Templates.Invite(b, clock.GetUtcNow(), site.Value.WhatsApp);
        email.Enqueue(Templates.TeamBooking(b, site.Value.TeamEmail, invite));
        if (IsEmail(b.Email)) email.Enqueue(Templates.ClientBooking(b, invite));
        await realtime.BookingCreatedAsync(Map.Booking(b));
        return new SubmitResult(true, b.Id);
    }

    public async Task<SlotsResult> Handle(GetTakenSlotsQuery q, CancellationToken ct)
    {
        var today = Riyadh.Today(clock);
        var from = q.From ?? today;
        var to = q.To ?? today.AddDays(BookingRules.MaxDaysAhead);
        if (to < from || to.DayNumber - from.DayNumber > 62) throw new DomainException("نطاق غير صالح");
        var a = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), Riyadh.Offset);
        var z = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), Riyadh.Offset);
        var taken = await db.Bookings.AsNoTracking().Where(b => b.StartsAt >= a && b.StartsAt < z && b.Status != BookingStatuses.Cancelled)
            .Select(b => b.StartsAt).ToListAsync(ct);
        return new SlotsResult(from.ToString("yyyy-MM-dd"), to.ToString("yyyy-MM-dd"),
            taken.Select(t => t.ToOffset(Riyadh.Offset).ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture)).Order().ToList());
    }

    public async Task<List<BookingDto>> Handle(ListBookingsQuery q, CancellationToken ct)
    {
        var query = db.Bookings.AsNoTracking();
        if (q.Upcoming) { var since = clock.GetUtcNow().AddHours(-2); query = query.Where(b => b.StartsAt >= since); }
        if (!string.IsNullOrEmpty(q.Status)) query = query.Where(b => b.Status == q.Status);
        var list = await (q.Upcoming ? query.OrderBy(b => b.StartsAt) : query.OrderByDescending(b => b.StartsAt)).Take(300).ToListAsync(ct);
        return list.Select(Map.Booking).ToList();
    }

    public async Task<BookingDto> Handle(UpdateBookingCommand cmd, CancellationToken ct)
    {
        if (!BookingStatuses.All.Contains(cmd.Status)) throw new DomainException("حالة غير معروفة");
        var b = await db.Bookings.FirstOrDefaultAsync(x => x.Id == cmd.Id, ct) ?? throw DomainException.NotFound("الحجز غير موجود");
        b.Status = cmd.Status;
        await db.SaveChangesAsync(ct);
        return Map.Booking(b);
    }
}
