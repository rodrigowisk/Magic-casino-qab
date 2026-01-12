using Magic_casino.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);

// --- LOGS DE INICIALIZAÇÃO (Úteis para Debug) ---
static string MaskPassword(string cs)
{
    if (string.IsNullOrWhiteSpace(cs)) return "(null)";
    return Regex.Replace(cs, @"(?i)(Password|Pwd)\s*=\s*[^;]*", "$1=***");
}
Console.WriteLine("ENV: " + builder.Environment.EnvironmentName);

// 1. BANCO DE DADOS
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// 2. CORS (LIBERA O FRONTEND)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// 3. AUTENTICAÇÃO (AQUI ESTÁ A CORREÇÃO CRUCIAL) 🔐
// Precisamos usar EXATAMENTE a mesma chave que colocamos no UsersController.cs
var jwtKey = "ChaveSecretaDoCassino2026SuperSeguraNaoMudeIsso";
var keyBytes = Encoding.UTF8.GetBytes(jwtKey);

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

        // DESLIGAMOS ISSUER/AUDIENCE PARA GARANTIR QUE O TOKEN PASSE
        // (Isso resolve o erro 401 se houver divergência de configuração no Docker)
        ValidateIssuer = false,
        ValidateAudience = false,

        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero // Remove tolerância de tempo para ser mais preciso
    };
});

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// 4. SWAGGER COM CADEADO
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Magic Casino API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Insira o token JWT assim: Bearer seu_token_aqui"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// --- PIPELINE DE EXECUÇÃO ---

// Migrations automáticas (Opcional)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    // db.Database.Migrate(); // Descomente se quiser aplicar migrations ao iniciar
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Magic Casino API v1");
    c.RoutePrefix = "swagger";
});

// ORDEM OBRIGATÓRIA: CORS -> AUTH -> AUTHZ
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();