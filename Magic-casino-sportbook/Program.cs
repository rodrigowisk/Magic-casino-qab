using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.Services;
using Magic_casino_sportbook.Hubs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using Magic_casino_sportbook.BackgroundServices;
using StackExchange.Redis; // ✅ ADICIONADO: Necessário para IConnectionMultiplexer

var builder = WebApplication.CreateBuilder(args);

Console.WriteLine("#############################################################");
Console.WriteLine(">>>>> SPORTBOOK API - SISTEMA UNIFICADO (BETS API) <<<<<");
Console.WriteLine("#############################################################");

// =============================================================
// 1. DATABASE
// =============================================================
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString)
           .EnableSensitiveDataLogging() // <--- O PULO DO GATO: Mostra os valores reais no log
           .EnableDetailedErrors());     // <--- Ajuda a detalhar erros de SQL


// =============================================================
// 2. JWT CONFIGURATION 🔐
// =============================================================
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
                    // 🌍 CASO 2: Domínio Real (Produção/Túnel)
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
// Pega a conexão da variável de ambiente ou usa o padrão do Docker Compose
var redisConnectionString = Environment.GetEnvironmentVariable("REDIS_CONNECTION") ?? "redis:6379";

// 4.1. Injeta o Cliente Redis (Para gravar Odds/Dados no Cache)
// Configuração robusta para não morrer se o Redis reiniciar
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var config = ConfigurationOptions.Parse(redisConnectionString);
    config.AbortOnConnectFail = false; // Não trava o boot se o Redis demorar
    config.ConnectRetry = 10;
    config.KeepAlive = 180;
    return ConnectionMultiplexer.Connect(config);
});

// 4.2. Configura o SignalR Backplane (Para Websockets Escalar e Estável)
// 🔥 CONFIGURAÇÃO CORRIGIDA PARA EVITAR "SOCKET CLOSED"
builder.Services.AddSignalR()
    .AddStackExchangeRedis(redisConnectionString, options => {
        // Define um prefixo para não misturar canais
        options.Configuration.ChannelPrefix = "SportbookUpdate";

        // 🚨 O SEGREDO: Não deixa o app travar se o Redis piscar
        options.Configuration.AbortOnConnectFail = false;

        // Tenta reconectar até 20 vezes se cair
        options.Configuration.ConnectRetry = 20;

        // Mantém o "ping" entre o servidor e o Redis a cada 3 minutos (Evita timeout ocioso)
        options.Configuration.KeepAlive = 180;

        // Aumenta o tempo limite para pacotes grandes (Odds) não darem timeout
        options.Configuration.SyncTimeout = 10000; // 10 segundos
        options.Configuration.ConnectTimeout = 10000;

        // Buffer de saída maior para aguentar o tranco das odds
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

// 1. Registra o Factory Global (Essencial para LiveSportService)
builder.Services.AddHttpClient();

// 2. Serviços Legados / Específicos
builder.Services.AddHttpClient<BetsApiService>();
//builder.Services.AddHttpClient<TheOddsApiService>();
builder.Services.AddHttpClient<PreMatchService>(); // Registra como Typed Client

// 3. Registro do LiveSportService
// Mudamos de AddHttpClient<> para AddScoped<> porque o construtor pede IHttpClientFactory
builder.Services.AddScoped<LiveSportService>();

// 4. Fila de Robos - Porteiro
builder.Services.AddSingleton<BetsApiGatekeeper>();

// 5. Seleção de Provedor de Odds
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

// 5. Integração com Core (Carteira)
builder.Services.AddHttpClient<CoreWalletService>(client =>
{
    client.BaseAddress = new Uri("http://core:8080");
});

// =============================================================
// 🤖 BACKGROUND SERVICES
// =============================================================

// 1. Robô de Ingestão UNIFICADO
builder.Services.AddHostedService<PreMatchWorker>();

// 2. Robô Ao Vivo
builder.Services.AddHostedService<LiveUpdateWorker>();

// 3. Robô de Settlement
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
        context.Database.Migrate();
        Console.WriteLine(">>>>> [BANCO DE DADOS] Migrações aplicadas com sucesso!");
    }
    catch (Exception ex)
    {
        Console.WriteLine($">>>>> [ERRO BANCO] Falha ao migrar: {ex.Message}");
    }
}

app.Run();