using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.BackgroundServices;
using Magic_casino_sportbook.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// LOG DE INICIALIZAÇÃO
Console.WriteLine("#############################################################");
Console.WriteLine(">>>>> SPORTBOOK API - CORRECAO DE CHAVE (FINAL) <<<<<");
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
                var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    var token = authHeader.Substring("Bearer ".Length).Trim();
                    token = token.Replace("\"", "").Replace("'", "");
                    context.Token = token;
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

// 3. CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", p => p.SetIsOriginAllowed(_ => true).AllowAnyMethod().AllowAnyHeader());
});

// 4. SERVICES
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Swagger
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
    c.AddSecurityRequirement(new OpenApiSecurityRequirement { { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() } });
});

// Services de Negócio
builder.Services.AddHttpClient(); // ✅ ADICIONADO: Necessário para o LiveScoreWorker fazer requisições
builder.Services.AddHttpClient<OddsService>();
builder.Services.AddScoped<OddsService>();

// Aponta para o container do Core na porta interna
builder.Services.AddHttpClient<CoreWalletService>(client => { client.BaseAddress = new Uri("http://core:8080"); });

// Background Services (Workers)
builder.Services.AddHostedService<OddsBackgroundService>();
builder.Services.AddHostedService<LiveScoreWorker>(); // ✅ ADICIONADO: Ativa o monitoramento de gols em tempo real

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", "SPORTBOOK API V1"); c.RoutePrefix = "swagger"; });

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// =============================================================
// 🛠️ BLOCO MÁGICO: AUTO-DB
// =============================================================
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        context.Database.EnsureCreated();
        Console.WriteLine(">>>>> [BANCO DE DADOS] Tabelas verificadas/criadas com sucesso!");
    }
    catch (Exception ex)
    {
        Console.WriteLine($">>>>> [ERRO BANCO] Falha ao criar tabelas: {ex.Message}");
    }
}

app.Run();