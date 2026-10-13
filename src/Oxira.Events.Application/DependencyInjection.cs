using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Oxira.Events.Application.Common;

namespace Oxira.Events.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection s, IConfiguration cfg)
    {
        s.Configure<SiteOptions>(cfg.GetSection("Site"));
        s.Configure<AiOptions>(cfg.GetSection("Ai"));
        s.Configure<RenderOptions>(cfg.GetSection("Renders"));
        s.Configure<AlertOptions>(cfg.GetSection("Alerts"));
        s.AddMediatR(c =>
        {
            c.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
            c.AddOpenBehavior(typeof(LoggingBehavior<,>));
        });
        return s;
    }
}
