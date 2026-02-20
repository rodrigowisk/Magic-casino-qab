using System.Text;
using Magic_casino_sportbook.BackgroundServices;
using Magic_casino_sportbook.Consumers;
using Magic_casino_sportbook.Events;
using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.Hubs;
using Magic_casino_sportbook.Services;
using MassTransit; 
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

Console.WriteLine("#############################################################");
Console.WriteLine(">>>>> SPORTBOOK API - SISTEMA UNIFICADO (BETS API) <<<<<");
Console.WriteLine("#############################################################");

// =============================================================
// 1. DATABASE
// =============================================================
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
// 2. JWT CONFIGURATION
// =============================================================
var jwtKey = Environment.GetEnvironmentVariable("JWT_SECRET")
              ?? builder.Configuration["Jwt:Key"]
              ?? "ChaveSecretaDoCassino2026SuperSeguraNaoMudeIsso";

Console.WriteLine($"[DEBUG PROGRAM] Chave JWT definida.");

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
// 4. REDIS CONFIGURATION
// =============================================================
var redisConnectionString = Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING")
                            ?? Environment.GetEnvironmentVariable("REDIS_CONNECTION")
                            ?? "localhost:6379,abortConnect=false";

Console.WriteLine($">>>>> REDIS TARGET: {redisConnectionString}");

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var config = ConfigurationOptions.Parse(redisConnectionString);
    config.AbortOnConnectFail = false;
    config.ConnectRetry = 10;
    config.KeepAlive = 180;
    if (redisConnectionString.Contains("ssl=true")) config.Ssl = true;
    return ConnectionMultiplexer.Connect(config);
});

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
// ✅ 6. RABBITMQ (MASSTRANSIT) - CONFIGURAÇÃO ATUALIZADA
// =============================================================
builder.Services.AddMassTransit(x =>
{
    // 👇 1. Registro de Todos os Consumidores
    x.AddConsumer<BetPlacedConsumer>();
    x.AddConsumer<BetWonConsumer>();
    x.AddConsumer<LiveGamePersistenceConsumer>(); // O consumidor de lote

    x.UsingRabbitMq((context, cfg) =>
    {
        // 👇 2. Configuração do Host RabbitMQ
        var rabbitHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "rabbitmq";
        var rabbitUser = Environment.GetEnvironmentVariable("RABBITMQ_USER") ?? "admin";
        var rabbitPass = Environment.GetEnvironmentVariable("RABBITMQ_PASS") ?? "admin";

        Console.WriteLine($">>>>> RABBITMQ CONFIG: Host={rabbitHost}, User={rabbitUser}");

        cfg.Host(rabbitHost, "/", h =>
        {
            h.Username(rabbitUser);
            h.Password(rabbitPass);
        });

        // 👇 3. Configuração ESPECÍFICA para a Fila de Persistência (Batch)
        // Isso é necessário para ativar o agrupamento de mensagens (Bulk Insert)
        cfg.ReceiveEndpoint("live-game-persistence-queue", e =>
        {
            e.PrefetchCount = 100; // Baixa até 100 mensagens para a memória

            // Configuração do Lote:
            // Processa se juntar 500 jogos OU se passar 45 segundos
            e.Batch<LiveGameUpdatedEvent>(b =>
            {
                b.MessageLimit = 500;
                b.TimeLimit = TimeSpan.FromSeconds(45);
                b.ConcurrencyLimit = 1; // Processa 1 lote por vez (protege o banco)
            });

            e.ConfigureConsumer<LiveGamePersistenceConsumer>(context);
        });

        // 👇 4. Configuração AUTOMÁTICA para os outros consumidores
        // (BetPlacedConsumer e BetWonConsumer serão configurados aqui automaticamente)
        cfg.ConfigureEndpoints(context);
    });
});

// =============================================================
// ✅ SERVICES DE NEGÓCIO (DI E GATEKEEPER)
// =============================================================

// Injeção do BetsApiGatekeeper usando o Redis que já configuramos acima
builder.Services.AddSingleton<BetsApiGatekeeper>();
builder.Services.AddTransient<Magic_casino_sportbook.Services.Gateways.BetsApiHttpService>();
builder.Services.AddTransient<Magic_casino_sportbook.Services.Prematch.IScheduleService, Magic_casino_sportbook.Services.Prematch.ScheduleService>();
builder.Services.AddTransient<Magic_casino_sportbook.Services.Prematch.IPrematchOddsService, Magic_casino_sportbook.Services.Prematch.PrematchOddsService>();
builder.Services.AddTransient<Magic_casino_sportbook.Services.Live.LiveGameUpdater>();
builder.Services.AddTransient<Magic_casino_sportbook.Services.Live.LiveCleanupService>();
builder.Services.AddTransient<Magic_casino_sportbook.Services.Live.HotZoneService>();

builder.Services.AddHttpClient();
//builder.Services.AddHttpClient<BetsApiService>();
//builder.Services.AddHttpClient<PreMatchService>();
//builder.Services.AddScoped<LiveSportService>();

string provider = Environment.GetEnvironmentVariable("ODDS_PROVIDER") ?? "BetsApi";


builder.Services.AddHttpClient<CoreWalletService>(client =>
{
    var coreUrl = Environment.GetEnvironmentVariable("CORE_API_URL") ?? "http://core:8080";
    client.BaseAddress = new Uri(coreUrl);
});

// =============================================================
// 🤖 BACKGROUND SERVICES
// =============================================================

builder.Services.AddHostedService<OddsBackgroundService>();
builder.Services.AddHostedService<PrematchOddsWorker>();

builder.Services.AddHostedService<LiveOddsWorker>();
builder.Services.AddHostedService<GameStatusWorker>();
builder.Services.AddHostedService<LiveScoreWorker>();

// ✅ ADICIONADO: O novo worker que salva do Redis para o SQL
builder.Services.AddHostedService<LivePersistenceWorker>();

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