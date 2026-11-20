using System.Text;
using AppFronton.Data;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

// Carga el archivo .env (variables de entorno)
Env.Load();

var builder = WebApplication.CreateBuilder(args);

// ── CLAVE: Permitir que escuche peticiones desde cualquier IP de la red local (Wi-Fi) ──
// Railway (y otros hosts) indican el puerto en la variable PORT; en tu PC sigue siendo 5000
var puerto = Environment.GetEnvironmentVariable("PORT") ?? "5000";
builder.WebHost.UseUrls($"http://0.0.0.0:{puerto}");

// ── Inyectar variables de .env en IConfiguration ─────────────────────────────
builder.Configuration.AddEnvironmentVariables();

// ── DbContext ────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<AppDbContext>();

// ── NUEVO (APP-AVO-041): envío de notificaciones push (solo si PUSH_WORKER=true en .env) ──
builder.Services.AddHttpClient();
builder.Services.AddHostedService<AppFronton.Services.PushNotificationWorker>();
// NUEVO (08): confirma solos los resultados sin respuesta en 24 h (también con PUSH_WORKER=true)
builder.Services.AddHostedService<AppFronton.Services.ResultadosVencidosWorker>();

// ── JWT Authentication ───────────────────────────────────────────────────────
var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET");
if (string.IsNullOrWhiteSpace(jwtSecret) || System.Text.Encoding.UTF8.GetByteCount(jwtSecret) < 32)
    throw new InvalidOperationException("Falta la variable de entorno JWT_SECRET (mínimo 32 caracteres).");
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = Environment.GetEnvironmentVariable("JWT_ISSUER"),
            ValidAudience            = Environment.GetEnvironmentVariable("JWT_AUDIENCE"),
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

builder.Services.AddAuthorization();

// ── CORS (permite el front React Native en dev) ──────────────────────────────
// La app móvil no usa CORS. Solo se permiten navegadores desde los orígenes listados en CORS_ORIGINS
// (separados por comas, p. ej. "https://app.geeky-tech.es,https://admin.geeky-tech.es"). Vacío = ninguno.
var origenesPermitidos = (Environment.GetEnvironmentVariable("CORS_ORIGINS") ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(opt => opt.AddDefaultPolicy(p =>
{
    if (origenesPermitidos.Length > 0)
        p.WithOrigins(origenesPermitidos).AllowAnyHeader().AllowAnyMethod();
}));

// ── Controllers ──────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "AppFronton API", Version = "v1" });

    // Botón Authorize en Swagger para JWT
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme       = "bearer",
        BearerFormat = "JWT",
        In           = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description  = "Ingresa el token JWT. Ejemplo: Bearer eyJhb..."
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// ── Build ────────────────────────────────────────────────────────────────────
var app = builder.Build();

// Swagger solo en desarrollo, o si se activa a propósito con ENABLE_SWAGGER=true (nunca en producción por defecto)
if (app.Environment.IsDevelopment() || Environment.GetEnvironmentVariable("ENABLE_SWAGGER") == "true")
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
