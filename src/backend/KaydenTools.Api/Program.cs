using System.Reflection;
using System.Text;
using FluentMigrator.Runner;
using KaydenTools.Api.Middleware;
using KaydenTools.Core.Configuration.Settings;
using KaydenTools.Core.Extensions;
using Kayden.Commons.Interfaces;
using KaydenTools.Core.Interfaces;
using KaydenTools.Migration.Extensions;
using KaydenTools.Repositories.Extensions;
using KaydenTools.Services.Auth;
using KaydenTools.Services.Extensions;
using KaydenTools.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

// Enable legacy timestamp behavior for Npgsql (PostgreSQL)
// This allows DateTime with Kind=Unspecified to be written to timestamptz columns
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Configuration
var databaseSettings = builder.Services.GetSettings<DatabaseSettings>(builder.Configuration);
var jwtSettings = builder.Services.GetSettings<JwtSettings>(builder.Configuration);
var lineSettings = builder.Services.GetSettings<LineLoginSettings>(builder.Configuration);
var googleSettings = builder.Services.GetSettings<GoogleLoginSettings>(builder.Configuration);
var urlShortenerSettings = builder.Services.GetSettings<UrlShortenerSettings>(builder.Configuration);

// Add services
builder.Services.AddCoreServices();
builder.Services.AddRepositories(databaseSettings.ConnectionString);
builder.Services.AddMigrations(databaseSettings.ConnectionString);
builder.Services.AddServices();

// Auth services
builder.Services.AddHttpClient();
builder.Services.AddSingleton(jwtSettings);
builder.Services.AddSingleton(lineSettings);
builder.Services.AddSingleton(googleSettings);
builder.Services.AddSingleton(urlShortenerSettings);
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IAuthService, AuthService>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// JWT Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidateAudience = true,
        ValidAudience = jwtSettings.Audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "KaydenTools API",
        Version = "v1",
        Description = "KaydenTools Backend API - SnapSplit, Authentication, and more."
    });

    // JWT Authentication
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token"
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
            Array.Empty<string>()
        }
    });

    // Include XML comments
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

// CORS
var allowedOrigins = builder.Configuration["Cors:AllowedOrigins"]?.Split(',')
    ?? new[] { "http://localhost:5173", "http://localhost:3000" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

// Run migrations
using (var scope = app.Services.CreateScope())
{
    var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
    runner.MigrateUp();
}

// Configure pipeline
app.UseGlobalExceptionHandler();

// Enable Swagger in all environments for now (can be restricted later)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "KaydenTools API v1");
    c.RoutePrefix = "swagger";
});

app.UseSerilogRequestLogging();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

/// <summary>
/// 當前使用者服務實作
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// 建構函式
    /// </summary>
    /// <param name="httpContextAccessor">HTTP 上下文存取器</param>
    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public Guid? UserId
    {
        get
        {
            var userIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value;
            return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
        }
    }

    /// <inheritdoc />
    public string? Email => _httpContextAccessor.HttpContext?.User?.FindFirst("email")?.Value;

    /// <inheritdoc />
    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
}
