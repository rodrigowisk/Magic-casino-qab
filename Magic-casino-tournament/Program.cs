using Magic_casino_tournament.Data;
using Magic_casino_tournament.Services;
using Magic_casino_tournament.BackgroundServices;
using Magic_casino_tournament.Consumers;
using Magic_casino_tournament.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Magic_casino.Contracts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL;
// 👇 SEÇÃO DE REDIS E REDLOCK CORRIGIDA
using StackExchange.Redis;
using RedLockNet; // 👈 OBRIGATÓRIO PARA IDistributedLockFactory
using RedLockNet.SERedis;
using RedLockNet.SERedis.Configuration;

var builder = WebApplication.CreateBuilder(args);

// 🛠️ CORREÇÃO DE DATAS NO POSTGRES
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

Console.WriteLine("#############################################################");
Console.WriteLine(">>>>> TOURNAMENT API - MAGIC CASINO <<<<<");
Console.WriteLine("#############################################################");

// ==============================================================================
// 1. Configuração do Banco de Dados
// ==============================================================================
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                        ?? Environment.GetEnvironmentVariable("DB_CONN_TOURNAMENT")
                        ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

if (string.IsNullOrEmpty(connectionString))
{
    throw new Exception("CRITICAL ERROR: ConnectionString not found!");
}

builder.Services.AddDbContext<TournamentDbContext>(options =>
    options.UseNpgsql(connectionString));

// ==============================================================================
// 2. Registro de Serviços e Dependências
// ==============================================================================
builder.Services.AddHttpClient();

// Cliente específico para o Core
builder.Services.AddHttpClient<ICoreGateway, CoreGateway>();

// Serviço principal do Torneio
builder.Services.AddScoped<ITournamentService, TournamentService>();

// Worker de Pagamento de Apostas
builder.Services.AddHostedService<TournamentSettlementWorker>();

// Worker de ciclo de vida (iniciar/encerrar torneios)
builder.Services.AddHostedService<TournamentLifecycleWorker>();

// ✅ CONFIGURAÇÃO DO REDIS (CACHE)
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = Environment.GetEnvironmentVariable("ConnectionStrings__Redis") ?? "redis:6379";
    options.InstanceName = "Tournament_";
});

var redisConnection = Environment.GetEnvironmentVariable("ConnectionStrings__Redis") ?? "redis:6379";

// ==============================================================================
// 🔒 CONFIGURAÇÃO DO REDLOCK (Bloqueio Distribuído) - CORRIGIDO
// ==============================================================================
// 1. Cria a conexão Multiplexer (singleton)
var multiplexer = ConnectionMultiplexer.Connect(redisConnection);
builder.Services.AddSingleton<IConnectionMultiplexer>(multiplexer);

// 2. Registra a fábrica de Locks
builder.Services.AddSingleton<IDistributedLockFactory>(sp =>
{
    // O RedLock precisa de uma lista de conexões (mesmo que seja só uma)
    var endpoints = new List<RedLockMultiplexer>
    {
        new RedLockMultiplexer(multiplexer)
    };

    return RedLockFactory.Create(endpoints);
});

// 3. Adicione o SignalR com o Redis Backplane configurado
builder.Services.AddSignalR()
    .AddStackExchangeRedis(redisConnection, options => {
        options.Configuration.ChannelPrefix = "TournamentHub";
    });

// ==============================================================================
// 🐰 4. CONFIGURAÇÃO DO RABBITMQ (MASSTRANSIT)
// ==============================================================================
builder.Services.AddMassTransit(x =>
{
    // Registra os Consumidores
    x.AddConsumer<UserUpdatedConsumer>();
    x.AddConsumer<GameEndedConsumer>();
    // OBS: O JoinTournamentConsumer é injetado automaticamente pelo framework se estiver na mesma pasta,
    // mas se precisar forçar, descomente: x.AddConsumer<JoinTournamentConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        var rabbitHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "rabbitmq";
        var rabbitUser = Environment.GetEnvironmentVariable("RABBITMQ_USER") ?? "admin";
        var rabbitPass = Environment.GetEnvironmentVariable("RABBITMQ_PASS") ?? "admin";

        cfg.Host(rabbitHost, "/", h =>
        {
            h.Username(rabbitUser);
            h.Password(rabbitPass);
        });

        // Fila 1: Atualizações de Usuário
        cfg.ReceiveEndpoint("user-updated-tournament-queue", e =>
        {
            e.ConfigureConsumer<UserUpdatedConsumer>(context);
        });

        // Fila 2: Resultados de Jogos
        cfg.ReceiveEndpoint("game-ended-tournament-queue", e =>
        {
            e.ConfigureConsumer<GameEndedConsumer>(context);
        });
    });
});

// ==============================================================================
// 5. Configuração do JWT
// ==============================================================================
var jwtKey = Environment.GetEnvironmentVariable("JWT_SECRET")
              ?? builder.Configuration["Jwt:Key"]
              ?? "ChaveSecretaDoCassino2026SuperSeguraNaoMudeIsso";

var keyBytes = Encoding.ASCII.GetBytes(jwtKey);

builder.Services.AddAuthentication(x =>
{
    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(x =>
{
    x.RequireHttpsMetadata = false;
    x.SaveToken = true;
    x.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
        ValidateIssuer = false,
        ValidateAudience = false
    };
});

// ==============================================================================
// 6. Controladores, Swagger e CORS
// ==============================================================================
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// ==============================================================================
// 7. Pipeline HTTP
// ==============================================================================
app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();
app.MapHub<TournamentHub>("/tournamentHub");
app.MapControllers();

// ==============================================================================
// 8. Migração Automática (Ao iniciar)
// ==============================================================================
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<TournamentDbContext>();

        Console.WriteLine(">>>>> [DB] Verificando banco de dados...");
        if (db.Database.CanConnect())
        {
            Console.WriteLine(">>>>> [DB] Aplicando migrações...");
            db.Database.Migrate();
            Console.WriteLine(">>>>> [DB] Banco atualizado com sucesso!");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ [CRITICAL] Erro ao conectar/migrar banco de dados: {ex.Message}");
    }
}

app.Run();