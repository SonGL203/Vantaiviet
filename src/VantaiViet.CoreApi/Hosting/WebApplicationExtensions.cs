using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace VantaiViet.CoreApi.Hosting;

internal static class WebApplicationExtensions
{
    public static WebApplication UseApiFoundation(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseSwagger();
        app.UseSwaggerUI();
        app.UseStatusCodePages();

        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
            app.UseHttpsRedirection();
        }

        app.UseCors();
        if (app.Environment.IsDevelopment())
        {
            app.UseStaticFiles();
        }
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks("/health/ready");
        app.MapControllers();
        app.MapHub<VantaiViet.CoreApi.Hubs.TrackingHub>("/hubs/tracking", options =>
        {
            options.CloseOnAuthenticationExpiration = true;
        });
        return app;
    }
}
