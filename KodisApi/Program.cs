global using Microsoft.EntityFrameworkCore;
global using KodisApi.Data;
global using KodisApi.Dtos;
global using KodisApi.Extensions;
global using KodisApi.Infrastructure;
global using KodisApi.Services;
using KodisApi.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi.Models;
using Sqids;
using System.IdentityModel.Tokens.Jwt;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Configuration
// ---------------------------------------------------------------------------
// Every section is validated at startup so a missing secret fails the boot
// rather than surfacing as a confusing 500 on the first request.
builder.Services.AddOptions<JwtSettings>()
    .Bind(builder.Configuration.GetSection(JwtSettings.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<GoogleSettings>()
    .Bind(builder.Configuration.GetSection(GoogleSettings.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<SqidsSettings>()
    .Bind(builder.Configuration.GetSection(SqidsSettings.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<CorsSettings>()
    .Bind(builder.Configuration.GetSection(CorsSettings.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<NotebookSettings>()
    .Bind(builder.Configuration.GetSection(NotebookSettings.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
    ?? throw new InvalidOperationException($"The '{JwtSettings.SectionName}' configuration section is missing.");

var corsSettings = builder.Configuration.GetSection(CorsSettings.SectionName).Get<CorsSettings>()
    ?? new CorsSettings();

var connectionString = builder.Configuration.GetConnectionString("ApplicationDbContext")
    ?? throw new InvalidOperationException("The 'ApplicationDbContext' connection string is missing.");

// ---------------------------------------------------------------------------
// Framework services
// ---------------------------------------------------------------------------
// Under systemd this signals readiness (Type=notify) and maps log levels onto
// journald priorities. It is a no-op everywhere else.
builder.Host.UseSystemd();

builder.Services.AddSingleton(TimeProvider.System);

// Without a key ring the framework falls back to ephemeral in-memory keys and
// warns on every start. Nothing here depends on data protection today, but a
// stable location keeps the logs clean and avoids a surprise later.
var keyRingPath = builder.Configuration["DataProtection:KeyRingPath"];
if (!string.IsNullOrWhiteSpace(keyRingPath))
{
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(keyRingPath))
        .SetApplicationName("KodisApi");
}

// Raw JWT claim names are easier to reason about than the WS-Federation
// aliases the handler substitutes by default.
JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = JwtTokenValidation.BuildParameters(jwtSettings, TimeProvider.System);
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                // Access and refresh tokens share a signing key, so without
                // this a 14-day refresh token would work as a bearer credential.
                if (context.Principal is null ||
                    !JwtService.HasTokenType(context.Principal, JwtService.AccessTokenType))
                {
                    context.Fail("Only access tokens are accepted as bearer credentials.");
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(corsSettings.AllowedOrigins)
    .AllowAnyMethod()
    .AllowAnyHeader()
    .WithExposedHeaders("Retry-After")));

// SQLite: one file, no server process. The app is deployed as a single
// instance, which is the constraint that makes this safe - SQLite allows only
// one writer at a time and must never live on a shared/network volume.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddSingleton(serviceProvider =>
{
    var sqids = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SqidsSettings>>().Value;
    return new SqidsEncoder<int>(new SqidsOptions
    {
        Alphabet = sqids.Alphabet,
        MinLength = sqids.MinLength
    });
});

builder.Services.AddHttpClient(GoogleAuthService.HttpClientName, client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<NotebookService>();
builder.Services.AddScoped<GoogleAuthService>();
builder.Services.AddSingleton<NotebookPasswordHasher>();
builder.Services.AddScoped<DataCleanupService>();
builder.Services.AddHostedService<ExpiredDataCleanupService>();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// ---------------------------------------------------------------------------
// Rate limiting
// ---------------------------------------------------------------------------
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = (context, _) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString();
        }

        return ValueTask.CompletedTask;
    };

    options.AddPolicy(RateLimitPolicies.Authentication, PartitionByCaller(limit: 20, windowInMinutes: 5));
    options.AddPolicy(RateLimitPolicies.NotebookRead, PartitionByCaller(limit: 120, windowInMinutes: 1));
    options.AddPolicy(RateLimitPolicies.NotebookWrite, PartitionByCaller(limit: 60, windowInMinutes: 1));

    // Backstop for anything without an explicit policy.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(CallerKey(context), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 300,
            Window = TimeSpan.FromMinutes(1)
        }));
});

// ---------------------------------------------------------------------------
// MVC + Swagger
// ---------------------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Kodis API", Version = "v1" });

    var scheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the access token returned by the sign-in endpoints.",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    };

    options.AddSecurityDefinition("Bearer", scheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement { [scheme] = Array.Empty<string>() });
});

// Notes are text; a few megabytes is far more than any legitimate request needs.
builder.Services.Configure<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>(
    options => options.Limits.MaxRequestBodySize = 4 * 1024 * 1024);

// Behind a reverse proxy the original scheme and client IP only survive in
// these headers - the rate limiter and HTTPS redirect both depend on them.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

// ---------------------------------------------------------------------------
// Pipeline
// ---------------------------------------------------------------------------
app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // There is no route at the root - this is an API. Point a browser that
    // lands there at the docs instead of a bare 404. Production keeps the 404.
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
}
else
{
    app.UseHsts();
}

// Only redirect when a HTTPS endpoint actually exists; otherwise the middleware
// silently does nothing and just logs a warning on every request.
if (app.Configuration.GetValue("Hosting:UseHttpsRedirection", false))
{
    app.UseHttpsRedirection();
}

app.UseRateLimiter();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .AllowAnonymous()
    .ExcludeFromDescription();

if (app.Configuration.GetValue("Database:MigrateOnStartup", app.Environment.IsDevelopment()))
{
    await app.MigrateDatabaseAsync();
}

app.Run();

static Func<HttpContext, RateLimitPartition<string>> PartitionByCaller(int limit, int windowInMinutes) =>
    context => RateLimitPartition.GetFixedWindowLimiter(
        CallerKey(context),
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limit,
            Window = TimeSpan.FromMinutes(windowInMinutes)
        });

// Signed-in callers get their own bucket; everyone else is bucketed by IP.
static string CallerKey(HttpContext context) =>
    context.User.GetUserIdOrNull()
    ?? context.Connection.RemoteIpAddress?.ToString()
    ?? "unknown";

/// <summary>Exposed so the integration tests can boot the same pipeline.</summary>
public partial class Program;
