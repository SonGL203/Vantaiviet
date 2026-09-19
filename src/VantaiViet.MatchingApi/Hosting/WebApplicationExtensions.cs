using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace VantaiViet.MatchingApi.Hosting;

internal static class WebApplicationExtensions
{
    public static WebApplication UseApiFoundation(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages();

        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
            app.UseHttpsRedirection();
        }

        app.UseCors();
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks("/health/ready");
        app.MapControllers();
        return app;
    }
}

