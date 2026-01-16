using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.Services;
using Magic_casino_sportbook.Hubs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using Magic_casino_sportbook.BackgroundServices;

var builder = WebApplication.CreateBuilder(args);

// LOG DE INICIALIZAÇÃO
Console.WriteLine("#############################################################");
Console.WriteLine(">>>>> SPORTBOOK API - SISTEMA DE MÚLTIPLOS MOTORES <<<<<");
Console.WriteLine("#############################################################");

// 1. DATABASE
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// 2. JWT CONFIGURATION 🔐
var jwtKey = "ChaveSecretaDoCassino2026SuperSeguraNaoMudeIsso";
var keyBytes = Encoding.UTF8.GetBytes(jwtKey);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.UseSecurityTokenValidators = true;
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            ValidateIssuer = false,
            ValidateAudience = false
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                // ✅ CONFIGURAÇÃO PARA SIGNALR: Captura o token via Query String
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/gameHub"))
                {
                    context.Token = accessToken.ToString().Replace("\"", "").Replace("'", "");
                }
                else
                {
                    var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
                    if (!string.IsNullOrEmpty(authHeader) &&
                        authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        var token = authHeader.Substring("Bearer ".Length).Trim();
                        token = token.Replace("\"", "").Replace("'", "");
                        context.Token = token;
                    }
                }
                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine($">>>>> [ERRO AUTH] {context.Exception.Message}");
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// 3. CORS (SESSÃO CRÍTICA) ⚠️
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy =>
        {
            policy.WithOrigins(
                    "http://localhost:5173",
                    "http://127.0.0.1:5173",
                    "http://localhost:8080",
                    "http://127.0.0.1:8080"
                  )
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials(); // ⚠️ OBRIGATÓRIO PARA SIGNALR
        });
});

// 4. MVC / SIGNALR
builder.Services.AddControllers();

// ✅ SIGNALR + REDIS
builder.Services.AddSignalR().AddStackExchangeRedis(o =>
{
    o.Configuration.EndPoints.Add("redis:6379");
    o.Configuration.AbortOnConnectFail = false;
});

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "MAGIC CASINO SPORTBOOK API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Bearer {token}"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// =============================================================
// ✅ SERVICES DE NEGÓCIO (DI)
// =============================================================

builder.Services.AddHttpClient();

// Registro dos serviços de API
builder.Services.AddHttpClient<BetsApiService>();
builder.Services.AddHttpClient<TheOddsApiService>();

string provider = Environment.GetEnvironmentVariable("ODDS_PROVIDER") ?? "BetsApi";

if (provider == "BetsApi")
{
    Console.WriteLine("🚀 MOTOR DE ODDS SELECIONADO: BetsAPI");
    // Registro para uso como interface
    builder.Services.AddScoped<IOddsService, BetsApiService>();
    // Registro para uso direto (LiveEventsWorker)
    builder.Services.AddScoped<BetsApiService>();
}
else
{
    Console.WriteLine("🚀 MOTOR DE ODDS SELECIONADO: The Odds API");
    builder.Services.AddScoped<IOddsService, TheOddsApiService>();
}

builder.Services.AddHttpClient<CoreWalletService>(client =>
{
    client.BaseAddress = new Uri("http://core:8080");
});

// =============================================================
// 🤖 BACKGROUND SERVICES (ROBÔS)
// =============================================================

// Robô Gerente de Odds Sincronizadas
builder.Services.AddHostedService<Magic_casino_sportbook.BackgroundServices.OddsBackgroundService>();

// Robô de Eventos em Tempo Real (O que manda as odds pro front)
builder.Services.AddHostedService<LiveEventsWorker>();

// Robô de Placar (Scores/Settlement)
builder.Services.AddHostedService<LiveScoreWorker>();


var app = builder.Build();

// --- CONFIGURAÇÃO DE MIDDLEWARE (ORDEM VITAL) ---
app.UseRouting();

// CORS deve vir LOGO APÓS Routing e ANTES de Auth
app.UseCors("AllowFrontend");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "SPORTBOOK API V1");
        c.RoutePrefix = "swagger";
    });
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// ✅ ROTA DO SIGNALR
app.MapHub<GameHub>("/gameHub");

// AUTO-MIGRATE
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        context.Database.Migrate();
        Console.WriteLine(">>>>> [BANCO DE DADOS] Migrações aplicadas com sucesso!");
    }
    catch (Exception ex)
    {
        Console.WriteLine($">>>>> [ERRO BANCO] Falha ao migrar: {ex.Message}");
    }
}

app.Run();