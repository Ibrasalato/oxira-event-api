# Oxira Events API

الخادم الخلفي لموقع [events.oxira.sa](https://events.oxira.sa)، ويحل محل workflow «Oxira Events» في n8n.

- **التقنية:** ASP.NET Core 10 و EF Core 10 على SQL Server.
- **البنية:** Clean Architecture، مع MediatR (CQRS) و Controllers.
- **الحسابات والأمان:** JWT لحساب أوكسيرا الموحّد.
- **التوثيق والتحديث اللحظي:** Swagger و SignalR.
- **الاستضافة:** IIS.

## البنية

| المشروع | المحتوى |
|---|---|
| `Oxira.Events.Domain` | الكيانات والقواعد: الطلبات، الحجوزات، التنبيهات، قواعد المواعيد، توقيت الرياض |
| `Oxira.Events.Application` | أوامر واستعلامات MediatR لكل ميزة في `Features/*`، والمنافذ (`IAppDbContext`، `IEmailSender`، `IAnthropic`، `IImageEditor`، `IQuota`، `IRealtime`)، وقوالب الإيميل بخمس لغات |
| `Oxira.Events.Infrastructure` | EF Core / SQL Server، SMTP (Zoho) في الخلفية، Claude، OpenAI Images، عدّادات الحصص اليومية (`MERGE … HOLDLOCK`)، تقويم المعارض |
| `Oxira.Events.Api` | Controllers، JWT، Swagger، SignalR (`/hubs/live`)، حدود الطلبات، المهام المجدولة |
| `tests/Oxira.Events.Tests` | اختبارات تكامل على SQL Server حقيقي (في CI) |

## الواجهات

| الطريقة | المسار | الوظيفة |
|---|---|---|
| POST | `/api/chat` | المساعد الذكي (Claude + أداة `create_lead`، ذاكرة 14 رسالة لكل جلسة) |
| GET | `/api/chat/{sessionId}` | سجل المحادثة |
| POST | `/api/leads` | طلب عرض سعر مع ملفات المخطِّط (JPG / GLB / OBJ بصيغة base64) |
| POST | `/api/bookings` | حجز استشارة 30 دقيقة مع دعوة تقويم `.ics` للفريق والعميل |
| GET | `/api/bookings/taken` | المواعيد المحجوزة |
| POST | `/api/alerts` | الاشتراك في تنبيهات المعارض |
| GET | `/api/alerts/unsubscribe?t=` | إلغاء الاشتراك (صفحة HTML) |
| POST | `/api/renders` | تصميم الجناح بالذكاء الاصطناعي (حصة مجانية يومية) |
| GET | `/api/renders/quota/{sessionId}` | المتبقي اليوم |
| GET/PATCH | `/api/admin/*` | لوحة الفريق: الطلبات وملفاتها، الحجوزات، المشتركون، ملخص (يحتاج دور staff أو admin) |

- **شكل الردود:** الردود الناجحة بنفس الشكل الذي يقرؤه الموقع (`success`، `id`، `reply`، `image`، `left`). الأخطاء ترجع `{ success:false, reason, message }` مع حالة HTTP المناسبة، ومن قيم `reason`: missing، email، slot، taken، limit، invalid، too_big.
- **الطلبات بصيغة `text/plain`:** الموقع يرسل JSON بنوع `text/plain` ليتجنب طلب CORS المسبق، والـ API يقبله.

## القواعد

- **حجز المواعيد:** الأحد إلى الخميس، من 10:00 إلى 16:30 بتوقيت الرياض، كل نصف ساعة. الحجز قبل الموعد بساعة على الأقل، وخلال 45 يومًا كحد أقصى. كل موعد يُحجز مرة واحدة فقط، ويضمن ذلك فهرس فريد على الموعد يستثني الحجوزات الملغاة.
- **تصاميم الذكاء الاصطناعي:**
  - الحدود اليومية: 3 لكل جلسة، و6 لكل IP، و150 للموقع كله.
  - المحاولة الفاشلة لا تُحسب على الزائر.
- **تنبيهات المعارض:**
  - مهمة تعمل كل ساعة بين 9 صباحًا و9 مساءً بتوقيت الرياض.
  - ترسل للمشترك تنبيهًا قبل المعرض بشهرين، ثم آخر قبله بثلاثة أسابيع، في المجالات التي اختارها فقط.
  - كل تنبيه يُسجَّل في قاعدة البيانات، فلا يصل للمشترك مرتين.
- **الإيميلات:** تُرسل في الخلفية مع 3 محاولات، فالطلب لا ينتظر SMTP.
- **مصيدة البوتات:** إذا امتلأ الحقل المخفي `company_website`، يرد الـ API بنجاح ولا يحفظ شيئًا.

## الحساب الموحّد (JWT)

- **من يُصدر الرموز:** `oxira-web-api`. هذا الـ API يتحقق منها فقط، بنفس `Jwt:Issuer` و`Jwt:Audience` و`Jwt:Key` المستخدمة في كل APIs أوكسيرا.
- **الصلاحيات:** لوحة الفريق تحتاج الدور `staff` أو `admin`.
- **للتجربة في بيئة التطوير:** `POST /api/dev/token` يعطي رمز staff (يرجع 404 في الإنتاج).

## SignalR

- **الرابط:** `/hubs/live`، ويحتاج رمز staff، يُرسل في `?access_token=`.
- **الأحداث:** `lead` عند وصول طلب جديد، و`booking` عند وصول حجز جديد.

## Swagger

- **الروابط:** الواجهة على `/swagger`، والوثيقة على `/openapi/v1.json`.
- **الإيقاف:** يُطفأ بالمتغير `Swagger__Enabled=false`.

## التشغيل محليًا

```bash
dotnet run --project src/Oxira.Events.Api   # https://localhost:7101/swagger
dotnet test                                  # يحتاج SQL Server (OXIRA_TEST_DB)
```

## النشر على IIS

1. **السيرفر:** ثبّت .NET 10 Hosting Bundle، وفعّل **WebSocket Protocol** من Windows Features.
2. **الملفات:** نزّل artifact باسم `oxira-events-api-iis` من آخر تشغيل لـ CI، أو نفّذ `dotnet publish -c Release`.
3. **الموقع في IIS:** أنشئ Site أو Application، مثلًا `api.oxira.sa/events`، واجعل Application Pool على **No Managed Code**.
4. **متغيرات البيئة:** ضعها في `web.config` تحت `<environmentVariables>` أو في إعدادات السيرفر، ولا تضع أي سر في المستودع.
   - `ConnectionStrings__Events`
   - `Jwt__Key`: نفس المفتاح في كل APIs أوكسيرا، 32 حرفًا على الأقل.
   - `Smtp__Password`: كلمة مرور تطبيق من Zoho.
   - `Anthropic__ApiKey`
   - `OpenAi__ApiKey`
   - `Site__ApiUrl`: العنوان العام لهذا الـ API، ويُستخدم في روابط إلغاء الاشتراك.
5. **قاعدة البيانات:** تُنشأ وتُحدَّث تلقائيًا عند التشغيل. لو تفضّل أن يطبّقها الـ DBA يدويًا، اضبط `Database__MigrateOnStartup=false` واستخدم `db/oxira-events.sql`.
6. **Cloudflare:** إن كان الـ API خلفه، اترك `Proxy:TrustCloudflare=true` ليقرأ عنوان الزائر من `CF-Connecting-IP`.

## Migrations

**Actions → Add EF Core migration → Run workflow** مع اسم الـ migration: يولّدها ويضيف السكربت `db/oxira-events.sql`، ثم يحفظهما في المستودع.
