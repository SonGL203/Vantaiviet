using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VantaiViet.MatchingApi.Data;
using VantaiViet.MatchingApi.Services.Interfaces;
using VantaiViet.MatchingApi.Services;

namespace VantaiViet.MatchingApi.Hosting;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiFoundation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddDbContext<MatchingDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("MatchingDatabase");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Configure ConnectionStrings:MatchingDatabase before using database services.");
            }

            options.UseNpgsql(connectionString);
        });
        services.AddScoped<ISystemInfoService, SystemInfoService>();
        services.AddControllers().ConfigureApiBehaviorOptions(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var problem = new ValidationProblemDetails(context.ModelState)
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Request validation failed."
                };
                problem.Extensions["errorCode"] = "request.validation_failed";
                problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
                return new BadRequestObjectResult(problem)
                {
                    ContentTypes = { "application/problem+json" }
                };
            };
        });
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Extensions.TryAdd("traceId", context.HttpContext.TraceIdentifier);
                context.ProblemDetails.Extensions.TryAdd(
                    "errorCode",
                    $"http.{context.ProblemDetails.Status ?? context.HttpContext.Response.StatusCode}");
            };
        });
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddHealthChecks();
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
                if (origins.Length > 0)
                {
                    policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
                }
            });
        });
        return services;
    }
}
