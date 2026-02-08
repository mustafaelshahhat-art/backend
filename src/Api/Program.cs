using Api.Middleware;
using Application;
using Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddSignalR();
builder.Services.AddSingleton<Microsoft.AspNetCore.SignalR.IUserIdProvider, Api.Infrastructure.CustomUserIdProvider>();
builder.Services.AddScoped<Application.Interfaces.IRealTimeNotifier, Api.Services.RealTimeNotifier>();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy =>
        {
            var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? new[] { "http://localhost:4200" };
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials(); // Required for SignalR
        });
});

// Add Layer Dependencies
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Helpers
builder.Services.AddHttpContextAccessor();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Ramadan Tournament API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] { }
        }
    });
});

// Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
            ValidAudience = builder.Configuration["JwtSettings:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JwtSettings:Secret"]!))
        };
        
        // SignalR Token Config
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) &&
                    (path.StartsWithSegments("/hubs/notifications") || path.StartsWithSegments("/hubs/chat")))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

// Authorization policies if needed, or stick to Roles.
builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment()) { 
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

// app.UseHttpsRedirection(); 
app.UseStaticFiles();

app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseMiddleware<UserStatusCheckMiddleware>();
app.UseMiddleware<MaintenanceModeMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.MapHub<Api.Hubs.NotificationHub>("/hubs/notifications");
app.MapHub<Api.Hubs.MatchChatHub>("/hubs/chat");

// Ensure Migration?

// Ensure Migration?
// Scope for migration.
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<Infrastructure.Data.AppDbContext>();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    
    try 
    {
        dbContext.Database.Migrate();

        // Seed Admin if not exists (check ignoring soft-delete filters)
        var adminUser = dbContext.Users.IgnoreQueryFilters().FirstOrDefault(u => u.Email == "admin@test.com");
        
        // Retrieve Admin Password safely
        var adminPassword = configuration["AdminSettings:Password"];
        
        // Only enforce password presence if we actully need to create/update the admin
        // But for "Fail startup if missing" rule, we should check it if we intend to seed.
        
        if (adminUser == null)
        {
            if (string.IsNullOrEmpty(adminPassword))
            {
                throw new InvalidOperationException("AdminSettings:Password is not configured. Cannot seed Admin user.");
            }

            var hasher = scope.ServiceProvider.GetRequiredService<Application.Interfaces.IPasswordHasher>();
            adminUser = new Domain.Entities.User
            {
                Email = "admin@test.com",
                Name = "Admin",
                PasswordHash = hasher.HashPassword(adminPassword),
                Role = UserRole.Admin,
                Status = UserStatus.Active,
                IsEmailVerified = true,
                DisplayId = "ADM-001",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            dbContext.Users.Add(adminUser);
            dbContext.SaveChanges();
        }
        else 
        {
            // Ensure existing admin is active, not deleted.
            // ONLY update password if configured, otherwise keep existing.
            // This prevents locking out admin if config is missing in an existing env, 
            // BUT the audit goal is "Remove Hardcoded". So we can't fall back to "password".
            
            var hasher = scope.ServiceProvider.GetRequiredService<Application.Interfaces.IPasswordHasher>();
            adminUser.Role = UserRole.Admin;
            adminUser.Status = UserStatus.Active;
            adminUser.IsEmailVerified = true;
            
            if (!string.IsNullOrEmpty(adminPassword))
            {
                adminUser.PasswordHash = hasher.HashPassword(adminPassword);
            }
            
            // Explicitly reset IsDeleted shadow property
            dbContext.Entry(adminUser).Property("IsDeleted").CurrentValue = false;
            
            dbContext.SaveChanges();
        }
        // Initialize Activity Log Migration (Run once or on demand)
        // var migrationService = scope.ServiceProvider.GetRequiredService<Application.Services.ActivityLogMigrationService>();
        // await migrationService.MigrateLegacyLogsAsync();
    }
    catch (Exception ex)
    {
        // Log migration error
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating or seeding the database.");
        
        // Rethrow if it's a configuration error to prevent silent failure in Production
        if (ex is InvalidOperationException) throw;
    }
}

app.Run();
// Trigger rebuild
