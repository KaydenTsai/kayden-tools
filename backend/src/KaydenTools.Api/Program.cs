using System.Reflection;
using System.Text;
using System.Text.Json;
using FluentMigrator.Runner;
using FluentValidation;
using FluentValidation.AspNetCore;
using KaydenTools.Api.Hubs;
using KaydenTools.Api.Middleware;
using KaydenTools.Api.Services;
using KaydenTools.Api.Validators;
using KaydenTools.Core.Configuration.Settings;
using KaydenTools.Core.Extensions;
using KaydenTools.Core.Interfaces;
using KaydenTools.Migration.Extensions;
using KaydenTools.Repositories.Extensions;
using KaydenTools.Services.Auth;
using KaydenTools.Services.Extensions;
using KaydenTools.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
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
builder.Services.AddScoped<IBillNotificationService, BillNotificationService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// FluentValidation
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<CreateBillDtoValidator>();

// JWT Authentication
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        // 禁用 claim 類型自動映射，保留原始 JWT claim 名稱（sub, email, name 等）
        options.MapInboundClaims = false;

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

        // SignalR 透過 query string 傳遞 access_token
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                // 如果是 SignalR Hub 的請求，從 query string 讀取 token
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs")) context.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });
builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    // 多服務 Swagger 文件
    c.SwaggerDoc("snapsplit", new OpenApiInfo
    {
        Title = "SnapSplit API",
        Version = "v1",
        Description = "分帳計算與即時同步 API"
    });

    c.SwaggerDoc("urlshortener", new OpenApiInfo
    {
        Title = "URL Shortener API",
        Version = "v1",
        Description = "短網址服務 API"
    });

    c.SwaggerDoc("common", new OpenApiInfo
    {
        Title = "Common API",
        Version = "v1",
        Description = "共用 API（認證、健康檢查）"
    });

    // 根據 GroupName 分配到對應文件
    c.DocInclusionPredicate((docName, apiDesc) =>
    {
        var groupName = apiDesc.ActionDescriptor
            .EndpointMetadata
            .OfType<ApiExplorerSettingsAttribute>()
            .FirstOrDefault()?.GroupName;

        return groupName == docName || (groupName == null && docName == "common");
    });

    // JWT Authentication
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme.",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
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
    if (File.Exists(xmlPath)) c.IncludeXmlComments(xmlPath);
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
    c.SwaggerEndpoint("/swagger/snapsplit/swagger.json", "SnapSplit API");
    c.SwaggerEndpoint("/swagger/urlshortener/swagger.json", "URL Shortener API");
    c.SwaggerEndpoint("/swagger/common/swagger.json", "Common API");
    c.RoutePrefix = "swagger";
});

app.UseSerilogRequestLogging();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<BillHub>("/hubs/bill");

app.Run();
