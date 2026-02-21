using Api.Infrastructure;
using Application;
using Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Load local overrides (not committed to git)
// Load local overrides (only in development)
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
}

// PROD-AUDIT: Structured Logging
// PERF-FIX: File sink wrapped in Async to prevent synchronous I/O from blocking
// request threads under load. Also added fileSizeLimitBytes to prevent disk exhaustion.
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.Async(a => a.File("logs/log-.txt",
        rollingInterval: RollingInterval.Day,
        fileSizeLimitBytes: 50_000_000, // 50 MB per file
        retainedFileCountLimit: 7))
    .CreateLogger();
builder.Host.UseSerilog();

// Fail fast if JWT secret is missing or too short
var jwtSecret = builder.Configuration["JwtSettings:Secret"];
if (string.IsNullOrEmpty(jwtSecret) || jwtSecret.Length < 16)
{
    if (builder.Environment.IsProduction())
    {
        throw new InvalidOperationException(
            "CRITICAL: JwtSettings:Secret is missing or too short (min 16 chars). " +
            "Set via environment variable: JwtSettings__Secret");
    }

    // Development only: use a dummy secret so local startup doesn't crash
    Log.Warning("[CONFIG] JwtSettings:Secret is missing. Using dummy secret (Development only).");
    builder.Configuration["JwtSettings:Secret"] = "DEV_ONLY_DUMMY_SECRET_NOT_FOR_PRODUCTION_USE_1234";
}

// Services
builder.ConfigureKestrel();
builder.Services.AddResponseCompression(options => options.EnableForHttps = true);
builder.Services.AddResponseCaching();
builder.Services.AddOutputCachePolicies();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });
builder.Services.AddHealthChecks()
    .AddDbContextCheck<Infrastructure.Data.AppDbContext>(tags: new[] { "ready" });
// IDistributedCache + IConnectionMultiplexer registered in Infrastructure/DependencyInjection.cs
builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(e => e.Value != null && e.Value.Errors.Count > 0)
            .ToDictionary(
                e => e.Key,
                e => e.Value!.Errors.Select(x => x.ErrorMessage).ToArray());
        return new BadRequestObjectResult(
            new Application.Contracts.Common.ValidationErrorResponse(
                "البيانات المرسلة غير صالحة. يرجى مراجعة الحقول.", errors));
    };
});
builder.AddSignalRServices();
builder.Services.AddApiCors(builder.Configuration, builder.Environment);
builder.Services.AddApiRateLimiting();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<Application.Interfaces.ICurrentUserAccessor, Infrastructure.Authentication.CurrentUserAccessor>();
builder.Services.AddSwaggerDocumentation();
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddAuthorizationPolicies();
builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.CheckConsentNeeded = context => false;
    options.MinimumSameSitePolicy = SameSiteMode.Strict;
    options.HttpOnly = Microsoft.AspNetCore.CookiePolicy.HttpOnlyPolicy.Always;
    options.Secure = CookieSecurePolicy.Always;
});

var app = builder.Build();
app.UseApiPipeline();
app.MapApiEndpoints();
app.InitializeDatabase();
app.Run();

public partial class Program { }
