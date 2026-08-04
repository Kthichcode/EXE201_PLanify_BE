using Microsoft.AspNetCore.Authentication.JwtBearer;
using Npgsql;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Planify.Infrastructure;
using Planify.Infrastructure.Data;
using System.Text;

// Fix: Npgsql 6+ rejects DateTime with Kind=Unspecified for 'timestamp with time zone' columns.
// AI-returned date strings (e.g. "2024-01-01") parse as Kind=Unspecified → need legacy mode.
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// Fix: Render.com Linux containers have a low inotify instance limit (128).
// ASP.NET Core's WebApplication.CreateBuilder() immediately starts watching appsettings*.json
// via FileSystemWatcher (inotify on Linux), which exhausts the system limit and crashes.
// Setting this env var BEFORE CreateBuilder() disables all inotify-based file watching.
Environment.SetEnvironmentVariable("DOTNET_USE_POLLING_FILE_WATCHER", "true");

var builder = WebApplication.CreateBuilder(args);

// Additionally disable reload-on-change for all JSON config sources.
builder.Host.ConfigureAppConfiguration((_, config) =>
{
    foreach (var source in config.Sources
                 .OfType<Microsoft.Extensions.Configuration.Json.JsonConfigurationSource>())
    {
        source.ReloadOnChange = false;
    }
});

// CORS
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)       // đọc từ appsettings
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();               // cần thiết nếu dùng cookie / credentials
    });
});

// Controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Swagger with JWT Bearer support
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Planify API",
        Version = "v1",
        Description = "API cho ứng dụng Planify - Hỗ trợ xác thực JWT"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Nhập JWT token theo định dạng: Bearer {token}"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
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

    // Enable XML comments for Swagger
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);
});

// Infrastructure (DbContext + Identity + Services)
builder.Services.AddInfrastructure(builder.Configuration);

// JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var key = Encoding.UTF8.GetBytes(jwtSettings["Key"]!);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ClockSkew = TimeSpan.Zero
    };
});

var app = builder.Build();



var cs = builder.Configuration.GetConnectionString("DefaultConnection");

try
{
    using var con = new NpgsqlConnection(cs);
    con.Open();
    Console.WriteLine("PostgreSQL CONNECT OK");
}
catch (Exception ex)
{
    Console.WriteLine(ex.Message);
}

// Pipeline

    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Planify API v1");
    });


// Serve static files (google-login-test.html, etc.)
app.UseStaticFiles();

app.UseHttpsRedirection();

app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
