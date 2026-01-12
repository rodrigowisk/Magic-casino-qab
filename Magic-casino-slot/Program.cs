using Magic_casino_slot.Data;
using Magic_casino_slot.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

//
// ======================
// 1. DATABASE (SLOT)
// ======================
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    connectionString =
        Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
        ?? Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")
        ?? Environment.GetEnvironmentVariable("DATABASE_URL");
}

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ERRO CRÍTICO: Connection String não encontrada (SLOT).");
}

Console.WriteLine("--------------------------------------------------");
Console.WriteLine("[SLOT] API INICIADA");
Console.WriteLine($"[SLOT] CONNECTION STRING OK");
Console.WriteLine("--------------------------------------------------");

//
// ======================
// 2. SERVICES
// ======================
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "MAGIC CASINO SLOT API",
        Version = "v1",
        Description = "API exclusiva do módulo SLOT"
    });
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddHttpClient<FiverService>();

var app = builder.Build();

//
// ======================
// 3. MIDDLEWARE PIPELINE
// ======================
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "MAGIC CASINO SLOT API v1");
    c.RoutePrefix = "swagger"; // IMPORTANTE: NÃO usar string.Empty
});

app.UseAuthorization();

//
// ======================
// 4. ROUTES
// ======================
app.MapControllers();

// Rota de verificação (teste no Docker)
app.MapGet("/whoami", () => "SLOT API");

app.Run();
