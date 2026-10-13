using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Oxira.Events.Application.Common;
using Oxira.Events.Infrastructure.Data;
using Oxira.Events.Infrastructure.Services;

namespace Oxira.Events.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection s, IConfiguration cfg)
    {
        var cs = cfg.GetConnectionString("Events") ?? throw new InvalidOperationException("ConnectionStrings:Events is missing");
        s.AddDbContext<EventsDbContext>(o => o.UseSqlServer(cs, sql => sql.EnableRetryOnFailure(3)));
        s.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<EventsDbContext>());
        s.AddScoped<DbInitializer>();
        s.AddScoped<IQuota, SqlQuota>();
        s.AddSingleton(TimeProvider.System);

        s.Configure<SmtpOptions>(cfg.GetSection("Smtp"));
        s.AddSingleton<EmailQueue>();
        s.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<EmailQueue>());
        s.AddHostedService<EmailWorker>();

        s.Configure<AnthropicOptions>(cfg.GetSection("Anthropic"));
        s.Configure<OpenAiOptions>(cfg.GetSection("OpenAi"));
        s.Configure<ExhibitionFeedOptions>(cfg.GetSection("Exhibitions"));
        s.AddHttpClient<IAnthropic, AnthropicClient>(h => h.Timeout = TimeSpan.FromSeconds(90));
        s.AddHttpClient<IImageEditor, OpenAiImageEditor>(h => h.Timeout = TimeSpan.FromSeconds(200));
        s.AddHttpClient<IExhibitionFeed, ExhibitionFeed>(h => h.Timeout = TimeSpan.FromSeconds(20));
        return s;
    }
}
