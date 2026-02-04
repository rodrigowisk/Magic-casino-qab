using Magic_casino_tournament.Data;
using Magic_casino_tournament.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 1. Configuração do Banco de Dados
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

builder.Services.AddDbContext<TournamentDbContext>(options =>
    options.UseNpgsql(connectionString));

// 2. Registro de Serviços e Dependências
builder.Services.AddHttpClient(); // Cliente HTTP genérico
builder.Services.AddHttpClient<ICoreGateway, CoreGateway>(); // ✅ Cliente específico para o Core

builder.Services.AddScoped<ITournamentService, TournamentService>();

// 3. Configuração do JWT (Para proteger o endpoint /join)
var key = Encoding.ASCII.GetBytes(builder.Configuration["Jwt:Key"] ?? Environment.GetEnvironmentVariable("Jwt__Key") ?? "Segredo_Padrao_Se_Nao_Tiver_Env");
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
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = false,
        ValidateAudience = false
    };
});

// 4. Controladores e Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 5. CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");

// ✅ Ativa autenticação e autorização
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// 6. Migração Automática
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<TournamentDbContext>();
        db.Database.EnsureCreated();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Erro ao criar banco: {ex.Message}");
    }
}

app.Run();