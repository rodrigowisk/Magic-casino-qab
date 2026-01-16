using Magic_casino_tournament.Data;
using Magic_casino_tournament.Services; // Necessário para registrar o Service
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Configuração do Banco de Dados
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

builder.Services.AddDbContext<TournamentDbContext>(options =>
    options.UseNpgsql(connectionString));

// 2. Registro de Serviços e Dependências
// Permite fazer chamadas HTTP para o Core (cobrança)
builder.Services.AddHttpClient();

// Registra nosso serviço de lógica de torneio
builder.Services.AddScoped<ITournamentService, TournamentService>();

// 3. Controladores e Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 4. CORS (Para o Frontend acessar)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

// 5. Pipeline de Requisição
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");

app.UseAuthorization();

app.MapControllers();

// 6. Migração Automática (Cria as tabelas novas ao iniciar o container)
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<TournamentDbContext>();
        db.Database.EnsureCreated();
    }
    catch (Exception ex)
    {
        // É bom logar o erro caso o banco falhe, mas o catch vazio evita crash imediato
        Console.WriteLine($"Erro ao criar banco: {ex.Message}");
    }
}

app.Run();