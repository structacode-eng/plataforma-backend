using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Plataforma.Api.Seed;
using Plataforma.Application.Auth;
using Plataforma.Application.Licensing;
using Plataforma.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// PaaS (Railway, etc.) injetam a porta via variável PORT — a app precisa escutar nela.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Infraestrutura: DbContext (Npgsql), repositórios, hasher Argon2id, JWT e lease service.
builder.Services.AddInfrastructure(builder.Configuration);

// Casos de uso (camada Application).
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<DesktopAuthService>();
builder.Services.AddScoped<LicenseService>();
builder.Services.AddScoped<AdminCatalogService>();

// Validação de JWT nas requisições autenticadas (a chave vem de segredo).
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key não configurada (dotnet user-secrets set \"Jwt:Key\" \"...\").");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // preserva os claims originais (sub, role) sem renomear
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Garante o Owner inicial (a partir de Seed:Owner:* em segredo/config).
await DataSeeder.SeedOwnerAsync(app.Services, app.Configuration);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// TLS é terminado na borda (proxy/host) em produção — Cap. 17. Local roda em HTTP.
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
