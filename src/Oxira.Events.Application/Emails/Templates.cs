using System.Globalization;
using System.Text;
using Oxira.Events.Application.Common;
using Oxira.Events.Domain;
using static Oxira.Events.Domain.Text;

namespace Oxira.Events.Application.Emails;

/// <summary>Oxira Events email layout and the localized messages (ported from the n8n workflow).</summary>
public static class Templates
{
    private const string Wa = "https://wa.me/966565670776";

    public static string Shell(string dir, string inner) =>
        $"<div dir=\"{dir}\" style=\"background:#F5F8FC;padding:24px 12px;font-family:Tahoma,Arial,sans-serif;font-size:15px;line-height:1.7;text-align:{(dir == "rtl" ? "right" : "left")}\">" +
        "<div style=\"max-width:640px;margin:0 auto;background:#fff;border:1px solid #DCE3EA\">" +
        "<div style=\"background:#0A253E;padding:18px 24px\"><span style=\"color:#fff;font-size:22px;font-weight:700;font-family:Arial,sans-serif\" dir=\"ltr\">Oxira</span> " +
        "<span style=\"background:#F5A800;color:#0A253E;font-weight:700;font-size:12px;padding:3px 8px;font-family:Arial,sans-serif\">Events</span></div>" +
        $"<div style=\"padding:24px\">{inner}</div><div style=\"height:6px;background:#F5A800\"></div></div>" +
        "<p style=\"text-align:center;color:#8193A8;font-size:12px;margin:14px 0 0\" dir=\"ltr\"><a href=\"https://events.oxira.sa\" style=\"color:#8193A8\">events.oxira.sa</a> · info@oxira.sa</p></div>";

    public static string Table(IEnumerable<(string Key, string? Value)> rows) =>
        "<table role=\"presentation\" style=\"border-collapse:collapse;width:100%\">" +
        string.Concat(rows.Where(r => !string.IsNullOrWhiteSpace(r.Value)).Select(r =>
            $"<tr><td style=\"padding:9px 12px;border-bottom:1px solid #DCE3EA;color:#3E5570;width:36%;vertical-align:top\">{Html(r.Key)}</td>" +
            $"<td style=\"padding:9px 12px;border-bottom:1px solid #DCE3EA;color:#0A253E;font-weight:600\">{Html(r.Value)}</td></tr>")) + "</table>";

    private static string P(string text, bool bold = false, string color = "#3E5570") =>
        $"<p style=\"margin:0 0 14px;color:{color};{(bold ? "font-weight:700" : "")}\">{Html(text)}</p>";

    public static string RiyadhTime(DateTimeOffset t) => t.ToOffset(Riyadh.Offset).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    /// <summary>"Thursday 15 October 2026 · 10:30" in the page language, Gregorian calendar</summary>
    public static string LongDate(DateTimeOffset t, string lang)
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo(lang switch { "en" => "en-GB", "de" => "de-DE", "fr" => "fr-FR", "ru" => "ru-RU", _ => "ar-EG" }).Clone();
        culture.DateTimeFormat.Calendar = new GregorianCalendar();
        return t.ToOffset(Riyadh.Offset).ToString("dddd d MMMM yyyy · HH:mm", culture);
    }

    // ───── quote request ─────
    public static EmailMessage TeamQuote(Lead l, string team, IEnumerable<EmailAttachment> files)
    {
        var rows = new (string, string?)[]
        {
            ("رقم الطلب", "#" + l.Id), ("الاسم", l.ContactName), ("الجوال", l.Phone), ("البريد", l.Email), ("الجهة", l.Company), ("الخدمة", l.Service),
            ("المعرض / الفعالية", l.EventName), ("التاريخ", l.EventDate), ("المدينة", l.City), ("مساحة الجناح", l.StandSize), ("التفاصيل", l.Needs),
            ("طريقة التواصل", l.PreferredContact), ("القناة", l.Channel == LeadChannels.Chat ? "المحادثة الذكية" : "نموذج الموقع"),
            ("المصدر", string.Join(" / ", new[] { l.Source.Source, l.Source.Medium, l.Source.Campaign }.Where(x => !string.IsNullOrEmpty(x)))),
            ("لغة الصفحة", l.Lang), ("الصفحة", l.Page), ("الوقت", RiyadhTime(l.CreatedAt)),
        };
        var list = files.ToList();
        var preview = list.FirstOrDefault(f => f.ContentId != null);
        var inner = "<h2 style=\"margin:0 0 12px;color:#0A253E;font-size:19px\">طلب جديد من موقع Oxira Events</h2>" + Table(rows) +
            (l.DesignJson != null ? "<p style=\"margin:16px 0 6px;color:#0A253E;font-weight:700\">تصميم الجناح من المخطِّط ثلاثي الأبعاد</p>" + Table(Design(l.DesignJson)) : "") +
            (preview != null ? $"<img src=\"cid:{preview.ContentId}\" alt=\"\" style=\"width:100%;max-width:600px;display:block;margin-top:12px;border:1px solid #DCE3EA\">" : "") +
            (list.Count > 0 ? P("المرفقات: GLB يحتفظ بالألوان والشعار، وOBJ يفتح في أي نسخة من 3ds Max. الوحدات بالمتر.") : "");
        return new EmailMessage([team], $"طلب جديد من موقع Oxira Events رقم {l.Id}: {l.ContactName}", Shell("rtl", inner),
            IsEmail(l.Email) ? l.Email : null, list);
    }

    private static IEnumerable<(string, string?)> Design(string json)
    {
        using var d = System.Text.Json.JsonDocument.Parse(json);
        foreach (var p in d.RootElement.EnumerateObject())
            yield return (p.Name, p.Value.ValueKind == System.Text.Json.JsonValueKind.Array
                ? string.Join("، ", p.Value.EnumerateArray().Select(x => x.ToString())) : p.Value.ToString());
    }

    private record QuoteText(string Dir, string Subject, string Hi, string Intro, string Num, string Service, string Event, string Date, string Size,
        string Details, string Design, string DesignNote, string Next, string[] Steps, string Reach, string Sign);

    private static QuoteText Quote(string lang, int id, string name) => lang switch
    {
        "en" => new("ltr", $"We received your request #{id} | Oxira Events", $"Hello {name},", "Thank you for contacting Oxira Events. We received your request:",
            "Request number", "Service", "Exhibition or event", "Date", "Stand size", "Details", "Your 3D outline",
            "This is an initial concept from the planner. Our team prepares the final design.", "What happens next",
            ["Our team reviews your request and contacts you to confirm the details.", "We prepare the stand design and the quote.", "Once approved, we fabricate and install the stand before opening."],
            "For any question, reply to this email or message us on WhatsApp", "The Oxira Events team"),
        "de" => new("ltr", $"Ihre Anfrage Nr. {id} ist eingegangen | Oxira Events", $"Hallo {name},", "vielen Dank für Ihre Anfrage bei Oxira Events. Folgende Angaben haben wir erhalten:",
            "Anfragenummer", "Leistung", "Messe oder Veranstaltung", "Datum", "Standgröße", "Details", "Ihr 3D-Entwurf",
            "Dies ist ein erster Entwurf aus dem Planer. Das finale Design erstellt unser Team.", "Die nächsten Schritte",
            ["Unser Team prüft Ihre Anfrage und meldet sich, um die Details abzustimmen.", "Wir erstellen Standentwurf und Angebot.", "Nach Freigabe fertigen wir den Stand und bauen ihn vor Messebeginn auf."],
            "Bei Fragen antworten Sie einfach auf diese E-Mail oder schreiben Sie uns auf WhatsApp", "Ihr Oxira Events Team"),
        "fr" => new("ltr", $"Nous avons reçu votre demande n° {id} | Oxira Events", $"Bonjour {name},", "merci d’avoir contacté Oxira Events. Voici votre demande :",
            "Numéro de demande", "Service", "Salon ou événement", "Date", "Surface du stand", "Détails", "Votre esquisse 3D",
            "Il s’agit d’une esquisse initiale issue du planificateur. Notre équipe prépare la conception finale.", "Prochaines étapes",
            ["Notre équipe étudie votre demande et vous contacte pour confirmer les détails.", "Nous préparons la conception du stand et le devis.", "Après validation, nous fabriquons et montons le stand avant l’ouverture."],
            "Pour toute question, répondez à cet e-mail ou écrivez-nous sur WhatsApp", "L’équipe Oxira Events"),
        "ru" => new("ltr", $"Мы получили ваш запрос № {id} | Oxira Events", $"Здравствуйте, {name}!", "Спасибо, что обратились в Oxira Events. Мы получили ваш запрос:",
            "Номер запроса", "Услуга", "Выставка или мероприятие", "Дата", "Площадь стенда", "Подробности", "Ваш 3D-эскиз",
            "Это предварительный эскиз из планировщика. Финальный проект готовит наша команда.", "Что дальше",
            ["Наша команда изучит запрос и свяжется с вами для уточнения деталей.", "Мы подготовим проект стенда и смету.", "После согласования изготовим и смонтируем стенд до открытия."],
            "Если есть вопросы, ответьте на это письмо или напишите нам в WhatsApp", "Команда Oxira Events"),
        _ => new("rtl", $"استلمنا طلبك رقم {id} | Oxira Events", $"أهلاً {name}،", "شكراً لتواصلك مع Oxira Events. استلمنا طلبك وهذه تفاصيله:",
            "رقم الطلب", "الخدمة", "المعرض أو الفعالية", "التاريخ", "مساحة الجناح", "التفاصيل", "تصورك ثلاثي الأبعاد",
            "هذا تصور مبدئي من المخطِّط، وسيجهز فريقنا التصميم النهائي.", "الخطوات القادمة",
            ["يراجع فريقنا طلبك ويتواصل معك لتأكيد التفاصيل.", "نجهز تصميم الجناح وعرض السعر.", "بعد الاعتماد نبدأ التصنيع ونركّب الجناح قبل الافتتاح."],
            "لأي استفسار رد على هذا الإيميل أو راسلنا على واتساب", "فريق Oxira Events"),
    };

    public static EmailMessage ClientQuote(Lead l, EmailAttachment? preview, string whatsapp)
    {
        var t = Quote(l.Lang, l.Id, l.ContactName);
        var rows = new (string, string?)[] { (t.Num, "#" + l.Id), (t.Service, l.Service), (t.Event, l.EventName), (t.Date, l.EventDate),
            (t.Size, string.IsNullOrWhiteSpace(l.StandSize) ? null : l.StandSize + " m²"), (t.Details, l.Needs) };
        var design = preview != null
            ? $"<h3 style=\"margin:26px 0 10px;color:#0A253E;font-size:17px\">{Html(t.Design)}</h3><img src=\"cid:{preview.ContentId}\" alt=\"\" style=\"width:100%;max-width:600px;display:block;border:1px solid #DCE3EA\"><p style=\"margin:8px 0 0;color:#3E5570;font-size:13px\">{Html(t.DesignNote)}</p>"
            : "";
        var pad = t.Dir == "rtl" ? "right" : "left";
        var steps = $"<ol style=\"margin:8px 0 0;padding-{pad}:20px;color:#0A253E\">" + string.Concat(t.Steps.Select(s => $"<li style=\"margin:6px 0\">{Html(s)}</li>")) + "</ol>";
        var inner = P(t.Hi, true, "#0A253E") + P(t.Intro) + Table(rows) + design +
            $"<h3 style=\"margin:26px 0 4px;color:#0A253E;font-size:17px\">{Html(t.Next)}</h3>" + steps +
            $"<p style=\"margin:22px 0 0;color:#3E5570\">{Html(t.Reach)}: <a href=\"{Wa}\" style=\"color:#007DB4;font-weight:700\" dir=\"ltr\">{Html(whatsapp)}</a></p>" +
            $"<p style=\"margin:14px 0 0;color:#0A253E;font-weight:700\">{Html(t.Sign)}</p>";
        return new EmailMessage([l.Email], t.Subject, Shell(t.Dir, inner), null, preview != null ? [preview] : null);
    }

    // ───── consultation booking ─────
    private static readonly Dictionary<string, Dictionary<string, string>> Channels = new()
    {
        ["call"] = new() { ["ar"] = "مكالمة هاتفية", ["en"] = "Phone call", ["de"] = "Telefonat", ["fr"] = "Appel téléphonique", ["ru"] = "Телефонный звонок" },
        ["whatsapp"] = new() { ["ar"] = "مكالمة واتساب", ["en"] = "WhatsApp call", ["de"] = "WhatsApp-Anruf", ["fr"] = "Appel WhatsApp", ["ru"] = "Звонок в WhatsApp" },
        ["video"] = new() { ["ar"] = "اجتماع فيديو", ["en"] = "Video meeting", ["de"] = "Videomeeting", ["fr"] = "Visioconférence", ["ru"] = "Видеовстреча" },
        ["visit"] = new() { ["ar"] = "زيارة مكتبنا", ["en"] = "Visit our office", ["de"] = "Besuch in unserem Büro", ["fr"] = "Visite de nos bureaux", ["ru"] = "Визит в наш офис" },
    };

    private record BookingText(string Dir, string Subject, string Hi, string Intro, string When, string How, string Topic, string Num, string Ics, string Note, string Sign);

    private static BookingText BookingT(string lang, int id, string name) => lang switch
    {
        "en" => new("ltr", $"Your consultation request #{id} | Oxira Events", $"Hello {name},", "We have your consultation request and our team will confirm it with you. Details:", "Time", "How we connect", "Topic", "Booking number", "A calendar invite is attached so you can add it in one tap.", "Need another time? Just reply to this email.", "The Oxira Events team"),
        "de" => new("ltr", $"Ihre Beratungsanfrage Nr. {id} | Oxira Events", $"Hallo {name},", "wir haben Ihre Beratungsanfrage erhalten; unser Team bestätigt den Termin mit Ihnen. Details:", "Termin", "Kontaktweg", "Thema", "Buchungsnummer", "Eine Kalendereinladung ist angehängt.", "Anderer Termin gewünscht? Antworten Sie einfach auf diese E-Mail.", "Ihr Oxira Events Team"),
        "fr" => new("ltr", $"Votre demande de rendez-vous n° {id} | Oxira Events", $"Bonjour {name},", "nous avons bien reçu votre demande de rendez-vous ; notre équipe vous la confirmera. Détails :", "Horaire", "Moyen de contact", "Sujet", "Numéro de réservation", "Une invitation de calendrier est jointe.", "Besoin d’un autre horaire ? Répondez simplement à cet e-mail.", "L’équipe Oxira Events"),
        "ru" => new("ltr", $"Ваша запись на консультацию № {id} | Oxira Events", $"Здравствуйте, {name}!", "Мы получили запрос на консультацию, наша команда подтвердит его. Детали:", "Время", "Способ связи", "Тема", "Номер записи", "Во вложении — приглашение для календаря.", "Нужно другое время? Просто ответьте на это письмо.", "Команда Oxira Events"),
        _ => new("rtl", $"استلمنا طلب موعد الاستشارة رقم {id} | Oxira Events", $"أهلاً {name}،", "سجّلنا طلب موعد الاستشارة، وسيؤكده فريقنا معك. التفاصيل:", "الموعد", "طريقة التواصل", "الموضوع", "رقم الحجز", "أرفقنا دعوة تقويم تضيفها لتقويمك بضغطة واحدة.", "إذا احتجت تغيير الموعد رد على هذا الإيميل.", "فريق Oxira Events"),
    };

    public static string ChannelName(string channel, string lang) => Channels.GetValueOrDefault(channel, Channels["call"])[lang];

    /// <summary>.ics invite (UTC times, 15-minute reminder)</summary>
    public static EmailAttachment Invite(Booking b, DateTimeOffset now, string whatsapp)
    {
        static string U(DateTimeOffset d) => d.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        static string E(string? s) => (s ?? "").Replace("\\", "\\\\").Replace("\n", "\\n").Replace(",", "\\,").Replace(";", "\\;");
        var ch = ChannelName(b.Channel, "en");
        var lines = new[]
        {
            "BEGIN:VCALENDAR", "VERSION:2.0", "PRODID:-//Oxira Events//Consultation//EN", "CALSCALE:GREGORIAN", "METHOD:PUBLISH",
            "BEGIN:VEVENT", $"UID:booking-{b.Id}@events.oxira.sa", $"DTSTAMP:{U(now)}", $"DTSTART:{U(b.StartsAt)}",
            $"DTEND:{U(b.StartsAt.AddMinutes(BookingRules.SlotMinutes))}",
            $"SUMMARY:{E($"Oxira Events · {ch} · {b.ContactName}")}",
            $"DESCRIPTION:{E($"Oxira Events consultation #{b.Id}\n{ch}\n{b.Topic}\nWhatsApp: {whatsapp} · info@oxira.sa")}",
            $"LOCATION:{E(b.Channel == "visit" ? "Oxira, Riyadh" : ch)}",
            "ORGANIZER;CN=Oxira Events:mailto:info@oxira.sa",
            "BEGIN:VALARM", "TRIGGER:-PT15M", "ACTION:DISPLAY", "DESCRIPTION:Oxira Events", "END:VALARM",
            "END:VEVENT", "END:VCALENDAR", "",
        };
        return new EmailAttachment("oxira-events-consultation.ics", "text/calendar", Encoding.UTF8.GetBytes(string.Join("\r\n", lines)));
    }

    public static EmailMessage TeamBooking(Booking b, string team, EmailAttachment invite)
    {
        var when = LongDate(b.StartsAt, "ar") + " (الرياض)";
        var inner = "<h2 style=\"margin:0 0 12px;color:#0A253E;font-size:19px\">طلب موعد استشارة جديد</h2>" +
            Table([("رقم الحجز", "#" + b.Id), ("الموعد", when), ("طريقة التواصل", ChannelName(b.Channel, "ar")), ("الاسم", b.ContactName), ("الجوال", b.Phone),
                ("البريد", b.Email), ("الجهة", b.Company), ("الموضوع", b.Topic), ("المصدر", b.Source.Source), ("لغة الصفحة", b.Lang)]) +
            "<p style=\"margin:16px 0 0;color:#3E5570;font-size:14px\">الموعد مبدئي: تواصل مع العميل لتأكيده. دعوة التقويم مرفقة.</p>";
        return new EmailMessage([team], $"موعد استشارة جديد رقم {b.Id}: {b.ContactName} · {when}", Shell("rtl", inner), IsEmail(b.Email) ? b.Email : null, [invite]);
    }

    public static EmailMessage ClientBooking(Booking b, EmailAttachment invite)
    {
        var t = BookingT(b.Lang, b.Id, b.ContactName);
        var when = LongDate(b.StartsAt, b.Lang) + (b.Lang == "ar" ? " (بتوقيت الرياض)" : " (Riyadh time, GMT+3)");
        var inner = P(t.Hi, true, "#0A253E") + P(t.Intro) +
            Table([(t.Num, "#" + b.Id), (t.When, when), (t.How, ChannelName(b.Channel, b.Lang)), (t.Topic, b.Topic)]) +
            $"<p style=\"margin:18px 0 0;color:#3E5570\">{Html(t.Ics + " " + t.Note)}</p><p style=\"margin:14px 0 0;color:#0A253E;font-weight:700\">{Html(t.Sign)}</p>";
        return new EmailMessage([b.Email], t.Subject, Shell(t.Dir, inner), null, [invite]);
    }

    // ───── exhibition alerts ─────
    private record AlertText(string Dir, string Subject, string Hi, string Soon, string Later, string Venue, string Dates, string Cta, string Unsub, string Welcome, string WelcomeBody);

    private static AlertText AlertT(string lang) => lang switch
    {
        "en" => new("ltr", "Upcoming exhibitions in your sectors | Oxira Events", "Hello,", "Opening in about three weeks:", "Coming up in about two months:", "Venue", "Dates", "Plan your stand with us", "Unsubscribe", "You are subscribed to exhibition alerts | Oxira Events", "We will email you about two months and three weeks before each exhibition in the sectors you chose."),
        "de" => new("ltr", "Bevorstehende Messen in Ihren Branchen | Oxira Events", "Hallo,", "Beginnt in etwa drei Wochen:", "In etwa zwei Monaten:", "Ort", "Termin", "Planen Sie Ihren Stand mit uns", "Abmelden", "Ihre Messe-Benachrichtigungen sind aktiv | Oxira Events", "Wir schreiben Ihnen etwa zwei Monate und drei Wochen vor jeder Messe in Ihren Branchen."),
        "fr" => new("ltr", "Salons à venir dans vos secteurs | Oxira Events", "Bonjour,", "Ouvre dans environ trois semaines :", "Dans environ deux mois :", "Lieu", "Dates", "Préparez votre stand avec nous", "Se désabonner", "Vos alertes salons sont activées | Oxira Events", "Nous vous écrirons environ deux mois et trois semaines avant chaque salon de vos secteurs."),
        "ru" => new("ltr", "Ближайшие выставки в ваших отраслях | Oxira Events", "Здравствуйте!", "Откроется примерно через три недели:", "Примерно через два месяца:", "Площадка", "Даты", "Спланируйте стенд с нами", "Отписаться", "Уведомления о выставках включены | Oxira Events", "Мы напишем примерно за два месяца и за три недели до каждой выставки в выбранных отраслях."),
        _ => new("rtl", "معارض قادمة في مجالاتك | Oxira Events", "أهلاً،", "تفتتح بعد نحو ثلاثة أسابيع:", "قادمة بعد نحو شهرين:", "المكان", "التاريخ", "جهّز جناحك معنا", "إلغاء الاشتراك", "تم تفعيل تنبيهات المعارض | Oxira Events", "سنراسلك قبل كل معرض في المجالات التي اخترتها بنحو شهرين ثم بثلاثة أسابيع."),
    };

    public static EmailMessage AlertWelcome(AlertSubscriber s, string unsubscribeUrl)
    {
        var t = AlertT(s.Lang);
        var inner = P(t.Hi, true, "#0A253E") + P(t.WelcomeBody) + Unsub(t, unsubscribeUrl);
        return new EmailMessage([s.Email], t.Welcome, Shell(t.Dir, inner));
    }

    public static EmailMessage Alert(AlertSubscriber s, IReadOnlyList<(Exhibition Show, int Stage)> shows, string siteUrl, string unsubscribeUrl)
    {
        var t = AlertT(s.Lang);
        string Block(Exhibition e) =>
            $"<div style=\"border:1px solid #DCE3EA;padding:12px 14px;margin:0 0 10px\"><p style=\"margin:0 0 6px;color:#0A253E;font-weight:700\">{Html(s.Lang == "ar" && !string.IsNullOrEmpty(e.NameAr) ? e.NameAr : e.Name)}</p>" +
            Table([(t.Dates, $"{e.Start:yyyy-MM-dd} → {e.End:yyyy-MM-dd}"), (t.Venue, $"{e.Venue}، {e.City}")]) +
            $"<p style=\"margin:8px 0 0\"><a href=\"{siteUrl}/?plan={Uri.EscapeDataString(e.Name)}#planner\" style=\"color:#007DB4;font-weight:700\">{Html(t.Cta)}</a></p></div>";
        var soon = shows.Where(x => x.Stage == 21).Select(x => x.Show).ToList();
        var later = shows.Where(x => x.Stage != 21).Select(x => x.Show).ToList();
        var inner = P(t.Hi, true, "#0A253E") +
            (soon.Count > 0 ? P(t.Soon, true, "#0A253E") + string.Concat(soon.Select(Block)) : "") +
            (later.Count > 0 ? P(t.Later, true, "#0A253E") + string.Concat(later.Select(Block)) : "") + Unsub(t, unsubscribeUrl);
        return new EmailMessage([s.Email], t.Subject, Shell(t.Dir, inner));
    }

    private static string Unsub(AlertText t, string url) =>
        $"<p style=\"margin:20px 0 0;font-size:12px;color:#8193A8\"><a href=\"{url}\" style=\"color:#8193A8\">{Html(t.Unsub)}</a></p>";

    public static string UnsubscribedPage(bool ok) =>
        "<!doctype html><html lang=\"ar\" dir=\"rtl\"><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>Oxira Events</title>" +
        Shell("rtl", ok
            ? P("تم إلغاء اشتراكك في تنبيهات المعارض. You have been unsubscribed.", true, "#0A253E")
            : P("الرابط غير صالح أو مستخدم من قبل. This link is not valid.", true, "#0A253E")) + "</html>";
}
