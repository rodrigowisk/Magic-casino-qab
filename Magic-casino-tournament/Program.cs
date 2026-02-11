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
using Microsoft.EntityFrameworkCore; // Já deve ter, mas confirme
using Npgsql.EntityFrameworkCore.PostgreSQL; // ⚠️ ESSENCIAL para UseNpgsql
using Microsoft.Extensions.DependencyInjection; // Para AddStackExchangeRedisCache

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

// Worker de ciclo de vida (iniciar/encerrar torneios)
builder.Services.AddHostedService<TournamentLifecycleWorker>();

// ✅ CONFIGURAÇÃO DO REDIS (CACHE)
builder.Services.AddStackExchangeRedisCache(options =>
{
    // "redis" é o nome do serviço no seu docker-compose.yml
    options.Configuration = Environment.GetEnvironmentVariable("ConnectionStrings__Redis") ?? "redis:6379";
    options.InstanceName = "Tournament_"; // Prefixo para organizar as chaves
});


var redisConnection = Environment.GetEnvironmentVariable("ConnectionStrings__Redis") ?? "redis:6379";

// 2. Adicione o SignalR com o Redis Backplane configurado
builder.Services.AddSignalR()
    .AddStackExchangeRedis(redisConnection, options => {
        options.Configuration.ChannelPrefix = "TournamentHub";
    });

// ==============================================================================
// 🐰 3. CONFIGURAÇÃO DO RABBITMQ (MASSTRANSIT)
// ==============================================================================
builder.Services.AddMassTransit(x =>
{
    // Registra os Consumidores
    x.AddConsumer<UserUpdatedConsumer>();
    x.AddConsumer<GameEndedConsumer>(); // ✅ NOVO: Consumidor de Fim de Jogo

    x.UsingRabbitMq((context, cfg) =>
    {
        // 👇 CORREÇÃO: Pegar as variáveis corretas do Docker Compose
        var rabbitHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "rabbitmq";
        var rabbitUser = Environment.GetEnvironmentVariable("RABBITMQ_USER") ?? "admin";
        var rabbitPass = Environment.GetEnvironmentVariable("RABBITMQ_PASS") ?? "admin";

        cfg.Host(rabbitHost, "/", h =>
        {
            h.Username(rabbitUser);
            h.Password(rabbitPass);
        });

        // Fila 1: Atualizações de Usuário (Saldo, Nível)
        cfg.ReceiveEndpoint("user-updated-tournament-queue", e =>
        {
            e.ConfigureConsumer<UserUpdatedConsumer>(context);
        });

        // ✅ Fila 2: Resultados de Jogos (Vem do Sportbook)
        cfg.ReceiveEndpoint("game-ended-tournament-queue", e =>
        {
            e.ConfigureConsumer<GameEndedConsumer>(context);
        });
    });
});

// ==============================================================================
// 4. Configuração do JWT
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
        ValidateIssuer = false, // Em dev geralmente é false
        ValidateAudience = false
    };
});

// ==============================================================================
// 5. Controladores, Swagger e CORS
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
// 6. Pipeline HTTP
// ==============================================================================
app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();
app.MapHub<TournamentHub>("/tournamentHub");
app.MapControllers();

// ==============================================================================
// 7. Migração Automática (Ao iniciar)
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