using System.Text;
using System.Threading.RateLimiting;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using VantaiViet.CoreApi.Data;
using VantaiViet.CoreApi.Entities;
using VantaiViet.CoreApi.Services;
using VantaiViet.CoreApi.Services.Interfaces;

namespace VantaiViet.CoreApi.Hosting;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiFoundation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentActor, CurrentActor>();
        services.AddScoped<IShipmentService, ShipmentService>();
        services.AddScoped<IBookingService, BookingService>();
        services.AddScoped<IShipmentBrokerService, ShipmentBrokerService>();
        services.AddScoped<ITripService, TripService>();
        services.AddSignalR();
        services.AddScoped<ITrackingService, TrackingService>();
        services.AddScoped<ITripRouteService, TripRouteService>();
        services.AddHttpClient<IRouteProvider, OsrmRouteProvider>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(8);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("VantaiViet/1.0");
        });
        services.AddDbContext<CoreDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("CoreDatabase");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Configure ConnectionStrings:CoreDatabase before using database services.");
            }

            options.UseNpgsql(connectionString, postgres =>
                postgres.MigrationsHistoryTable("__EFMigrationsHistory", "public"));
        });
        services.AddDataProtection();
        services.AddIdentityCore<User>(options =>
        {
            options.SignIn.RequireConfirmedPhoneNumber = true;
            options.Stores.MaxLengthForKeys = 128;
        })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<CoreDbContext>()
            .AddDefaultTokenProviders();
        services.AddScoped<ISystemInfoService, SystemInfoService>();
        services.AddScoped<IRegistrationService, RegistrationService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPasswordRecoveryService, PasswordRecoveryService>();
        services.AddScoped<IOtpService, OtpService>();
        services.AddScoped<INotificationQueueService, NotificationQueueService>();
        if(configuration.GetValue("Notifications:WorkerEnabled",false))
        {
            services.AddHostedService<NotificationWorker>();
        }

        services.AddScoped<IKycReviewService, KycReviewService>();
        services.AddScoped<IAdminUserService, AdminUserService>();
        services.AddScoped<IDriverService, DriverService>();
        services.AddScoped<IDriverReviewService, DriverReviewService>();
        var jwtKey = configuration["Authentication:JwtSigningKey"];
        if (string.IsNullOrWhiteSpace(jwtKey))
        {
            throw new InvalidOperationException("Configure Authentication:JwtSigningKey.");
        }
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var claims = context.Principal;
                        if (!Guid.TryParse(claims?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,out var userId)
                            || !Guid.TryParse(claims?.FindFirst("sid")?.Value,out var sessionId)
                            || claims?.FindFirst("security_stamp")?.Value is not string stamp
                            || !await context.HttpContext.RequestServices.GetRequiredService<IAuthService>()
                                .IsSessionActiveAsync(userId,sessionId,stamp,context.HttpContext.RequestAborted))
                        { context.Fail("Session is no longer active."); }
                    },
                    OnMessageReceived = context =>
                    {
                        if (context.Request.Path.StartsWithSegments("/hubs/tracking"))
                        {
                            context.Token = context.Request.Query["access_token"];
                        }
                        return Task.CompletedTask;
                    }
                };
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = configuration["Authentication:Issuer"],
                    ValidateAudience = true,
                    ValidAudience = configuration["Authentication:Audience"],
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                    ValidateLifetime = true
                };
            });
        services.AddAuthorization();
        services.AddRateLimiter(options =>
        {
            options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit=30,Window=TimeSpan.FromMinutes(1),QueueLimit=0 }));
            options.OnRejected = async (context, ct) =>
            {
                context.HttpContext.Response.StatusCode = 429;
                await context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Status=429,Title="Too many authentication requests.",
                    Extensions = { ["errorCode"]="auth.rate_limited",["traceId"]=context.HttpContext.TraceIdentifier }
                },cancellationToken:ct);
            };
        });
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Dán accessToken trả về từ API đăng nhập."
            });
            options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
            {
                [new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    {
                        Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                }] = Array.Empty<string>()
            });
        });
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
