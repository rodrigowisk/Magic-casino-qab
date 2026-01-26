using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.Services;
using Magic_casino_sportbook.Hubs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using Magic_casino_sportbook.BackgroundServices;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

Console.WriteLine("#############################################################");
Console.WriteLine(">>>>> SPORTBOOK API - SISTEMA UNIFICADO (BETS API) <<<<<");
Console.WriteLine("#############################################################");

// =============================================================
// 1. DATABASE (CORREÇÃO: Prioridade para Variável de Ambiente)
// =============================================================
// Tenta pegar do .ENV (Docker/AWS) primeiro. Se for nulo, tenta do appsettings/secrets (Local)
var connectionString = Environment.GetEnvironmentVariable("DB_CONN_SPORTBOOK")
                       ?? builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrEmpty(connectionString))
{
    throw new Exception("CRITICAL ERROR: ConnectionString 'DB_CONN_SPORTBOOK' or 'DefaultConnection' not found!");
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString)
           .EnableSensitiveDataLogging()
           .EnableDetailedErrors());


// =============================================================
// 2. JWT CONFIGURATION 🔐 (CORREÇÃO: Pega do .ENV)
// =============================================================
// Pega do .ENV ou usa um fallback (apenas para dev, nunca use fallback em prod)
var jwtKey = Environment.GetEnvironmentVariable("JWT_SECRET")
             ?? builder.Configuration["Jwt:Key"]
             ?? "ChaveSecretaDoCassino2026SuperSeguraNaoMudeIsso";

// >>>>> LOG DEBUG INICIO <<<<<
Console.WriteLine("#############################################################");
Console.WriteLine($"[DEBUG PROGRAM - STARTUP] Chave definida para VALIDACAO: '{jwtKey}'");
Console.WriteLine("#############################################################");
// >>>>> LOG DEBUG FIM <<<<<

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
            ValidateIssuer = false, // Ajuste conforme necessário
            ValidateAudience = false // Ajuste conforme necessário
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/gameHub"))
                {
                    context.Token = accessToken.ToString().Replace("\"", "").Replace("'", "");
                }
                else
                {
                    var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
                    if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        context.Token = authHeader.Substring("Bearer ".Length).Trim().Replace("\"", "").Replace("'", "");
                    }
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// =============================================================
// 3. CORS
// =============================================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy =>
        {
            policy.WithOrigins(
                    "http://localhost:5173",
                    "http://127.0.0.1:5173",
                    "http://localhost:8080",
                    "http://127.0.0.1:8080",
                    "https://quebrandoabanca.bet",
                    "https://www.quebrandoabanca.bet"
                  )
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
});

builder.Services.AddControllers();

// =============================================================
// 4. REDIS CONFIGURATION (CACHE + SIGNALR) 🚀
// =============================================================
// CORREÇÃO: Busca exatamente o nome que definimos no .env ("REDIS_CONNECTION_STRING")
var redisConnectionString = Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING")
                            ?? Environment.GetEnvironmentVariable("REDIS_CONNECTION") // Fallback legado
                            ?? "localhost:6379,abortConnect=false";

Console.WriteLine($">>>>> REDIS TARGET: {redisConnectionString}");

// 4.1. Injeta o Cliente Redis
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var config = ConfigurationOptions.Parse(redisConnectionString);
    config.AbortOnConnectFail = false;
    config.ConnectRetry = 10;
    config.KeepAlive = 180;

    // Importante para AWS:
    if (redisConnectionString.Contains("ssl=true"))
    {
        config.Ssl = true;
    }

    return ConnectionMultiplexer.Connect(config);
});

// 4.2. SignalR Backplane
builder.Services.AddSignalR()
    .AddStackExchangeRedis(redisConnectionString, options => {
        options.Configuration.ChannelPrefix = "SportbookUpdate";
        options.Configuration.AbortOnConnectFail = false;
        options.Configuration.ConnectRetry = 20;
        options.Configuration.KeepAlive = 180;
        options.Configuration.SyncTimeout = 10000;
        options.Configuration.ConnectTimeout = 10000;
    });

// =============================================================
// 5. SWAGGER
// =============================================================
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
builder.Services.AddHttpClient<BetsApiService>();
builder.Services.AddHttpClient<PreMatchService>();
builder.Services.AddScoped<LiveSportService>();
builder.Services.AddSingleton<BetsApiGatekeeper>();

// Seleção de Provedor de Odds
string provider = Environment.GetEnvironmentVariable("ODDS_PROVIDER") ?? "BetsApi";

if (provider == "BetsApi")
{
    Console.WriteLine("🚀 MOTOR DE ODDS SELECIONADO: BetsAPI");
    builder.Services.AddScoped<IOddsService, BetsApiService>();
}
else
{
    Console.WriteLine("🚀 MOTOR DE ODDS SELECIONADO: The Odds API");
    builder.Services.AddScoped<IOddsService, TheOddsApiService>();
}

// Integração com Core
builder.Services.AddHttpClient<CoreWalletService>(client =>
{
    // Tenta pegar URL do .env ou usa padrão Docker
    var coreUrl = Environment.GetEnvironmentVariable("CORE_API_URL") ?? "http://core:8080";
    client.BaseAddress = new Uri(coreUrl);
});

// =============================================================
// 🤖 BACKGROUND SERVICES
// =============================================================

builder.Services.AddHostedService<PreMatchWorker>();
builder.Services.AddHostedService<LiveOddsWorker>();
builder.Services.AddHostedService<GameStatusWorker>();
builder.Services.AddHostedService<LiveScoreWorker>();

var app = builder.Build();

app.UseRouting();
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
app.MapHub<GameHub>("/gameHub");

// =============================================================
// AUTO-MIGRATE
// =============================================================
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        // Tenta pegar migração. Se falhar conexão, não derruba o app inteiro na hora
        // Mas loga o erro crítico
        if (context.Database.CanConnect())
        {
            context.Database.Migrate();
            Console.WriteLine(">>>>> [BANCO DE DADOS] Migrações aplicadas com sucesso!");
        }
        else
        {
            Console.WriteLine(">>>>> [ALERTA] Banco de dados ainda não está acessível.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($">>>>> [ERRO BANCO] Falha ao migrar: {ex.Message}");
    }
}

app.Run();