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

// ── JWT Authentication ───────────────────────────────────────────────────────
var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET")!;
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
builder.Services.AddCors(opt =>
    opt.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

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

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
