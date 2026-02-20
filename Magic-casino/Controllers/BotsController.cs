using System.Globalization;
using System.Text;
using BCrypt.Net;
using Magic_casino.Data;
using Magic_casino.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Magic_casino.Controllers
{
    [ApiController]
    [Route("api/bots")]
    public class BotsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public BotsController(AppDbContext context)
        {
            _context = context;
        }

        // =================================================================================
        // 🔍 O QUE FALTAVA: Endpoint para o Worker saber quem são os bots
        // =================================================================================
        [HttpGet("list")]
        public async Task<IActionResult> ListBots()
        {
            // ✅ AGORA RETORNANDO O AVATAR JUNTO COM CPF E USERNAME
            var bots = await _context.Users
                .AsNoTracking()
                .Where(u => u.Level == "5") // ou email endsWith @bot.com, dependendo da sua versão
                .Select(u => new
                {
                    Cpf = u.Cpf,
                    UserName = u.UserName, // Pega o UserName real (ex: amanda1997)
                    Avatar = u.Avatar      // 👈 CORREÇÃO: Enviando o Avatar para o robô!
                })
                .ToListAsync();

            return Ok(bots);
        }

        // =================================================================================
        // 🌱 CRIAR BOTS (Mantido igual, apenas garantindo Level="5")
        // =================================================================================
        [HttpGet("create")]
        public async Task<IActionResult> CreateBots([FromQuery] int count = 1000, [FromQuery] decimal balance = 50000m)
        {
            if (count > 2000)
                return BadRequest("Para segurança do banco, crie no máximo 2000 bots por vez.");

            var gerados = new List<object>();
            var newUsers = new List<User>();
            var newWallets = new List<Wallet>();
            var rnd = new Random();

            string defaultPasswordHash = BCrypt.Net.BCrypt.HashPassword("SenhaPadraoBot123!");

            for (int i = 0; i < count; i++)
            {
                // 1. Define Gênero
                bool isMale = rnd.Next(2) == 0;

                // 2. Escolhe Nomes
                string firstName = isMale ? MaleNames[rnd.Next(MaleNames.Length)] : FemaleNames[rnd.Next(FemaleNames.Length)];
                string lastName = LastNames[rnd.Next(LastNames.Length)];
                string fullName = $"{firstName} {lastName}";

                // 3. Define Avatar
                string avatarPath = "";
                // ✅ ALTERADO: 90% de chance de gerar um avatar (rnd.Next de 0 a 99)
                if (rnd.Next(0, 100) < 90)
                {
                    int avatarId = isMale ? rnd.Next(1, 42) : rnd.Next(1, 14);
                    string folder = isMale ? "man" : "woman";
                    avatarPath = $"/images/avatars/{folder}/{avatarId}.svg";
                }

                // 4. Busca Apelido
                string nameForUsername = firstName;
                string normalizedKey = RemoveAccents(firstName);

                if (Nicknames.ContainsKey(normalizedKey))
                {
                    if (rnd.Next(0, 100) > 50)
                    {
                        var options = Nicknames[normalizedKey];
                        nameForUsername = options[rnd.Next(options.Length)];
                    }
                }

                // 5. Gera Username e CPF
                string username = GerarUsernameHumano(nameForUsername, lastName, rnd);
                string cpf = GenerateInvalidCpf(rnd);

                var user = new User
                {
                    Cpf = cpf,
                    Name = fullName,
                    UserName = username,
                    Password = defaultPasswordHash,
                    Level = "5", // ✅ Identificador oficial de BOT
                    CreatedAt = DateTime.UtcNow.AddDays(-rnd.Next(0, 90)).AddMinutes(rnd.Next(-1000, 1000)),
                    EmailVerified = true,
                    Avatar = avatarPath,
                    SecurityStamp = Guid.NewGuid().ToString(),
                    Email = $"{username}@bot.com" // Opcional, mas ajuda a organizar
                };

                var wallet = new Wallet
                {
                    UserCpf = cpf,
                    BalanceFiver = 0,
                    BalanceQab = balance,
                    BalanceBonus = 0
                };

                newUsers.Add(user);
                newWallets.Add(wallet);

                if (i < 15) gerados.Add(new { Nome = fullName, User = username, Avatar = avatarPath == "" ? "SEM FOTO" : avatarPath });
            }

            try
            {
                await _context.Users.AddRangeAsync(newUsers);
                await _context.Wallets.AddRangeAsync(newWallets);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    mensagem = $"✅ SUCESSO! {count} bots (Level 5) criados.",
                    amostra = gerados
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { erro = "Falha ao salvar", detalhe = ex.Message });
            }
        }

        // =================================================================================
        // 📚 DADOS AUXILIARES (Mantidos originais)
        // =================================================================================

        private static readonly Dictionary<string, string[]> Nicknames = new()
        {
            { "Francisco", new[] { "chico", "kiko", "fran" } },
            { "Eduardo", new[] { "dudu", "edu" } },
            { "Leonardo", new[] { "leo", "leozinho" } },
            { "Gabriela", new[] { "gabi", "bibi" } },
            { "Carolina", new[] { "carol", "nina" } },
            { "Beatriz", new[] { "bia", "bea" } },
            { "Matheus", new[] { "math", "teteu" } },
            { "Rafael", new[] { "rafa", "fael" } },
            { "Juliana", new[] { "ju", "juju" } },
            { "Larissa", new[] { "lari", "lala" } },
            { "Mariana", new[] { "mari", "mah" } },
            { "Isabella", new[] { "isa", "bella" } },
            { "Lucas", new[] { "lukinhas", "lucao" } },
            { "Pedro", new[] { "pepe", "pedrinho" } },
            { "Antonio", new[] { "tonho", "tom" } },
            { "Jose", new[] { "ze", "zezinho" } },
            { "Daniel", new[] { "dani", "dan" } },
            { "Marcelo", new[] { "celo", "marcelinho" } },
            { "Fernanda", new[] { "nanda", "fe" } },
            { "Amanda", new[] { "mandinha", "amandin" } },
            { "Roberto", new[] { "beto", "betinho" } },
            { "Ricardo", new[] { "rick", "cadu" } },
            { "Vinicius", new[] { "vini", "vinny" } },
            { "Guilherme", new[] { "gui", "guiga" } },
            { "Leticia", new[] { "le", "leh" } },
            { "Camila", new[] { "cami", "mila" } },
            { "Tatiane", new[] { "tati" } },
            { "Alexandre", new[] { "ale", "xande" } },
            { "Alessandra", new[] { "ale", "lele" } },
            { "Cristina", new[] { "cris", "tina" } },
            { "Patricia", new[] { "pati", "path" } },
            { "Rodrigo", new[] { "diguinho", "rodriguinho" } },
            { "Tiago", new[] { "tiaguinho" } },
            { "Felipe", new[] { "lipe", "felipinho" } },
            { "Bruno", new[] { "bruninho", "bru" } },
            { "Gustavo", new[] { "gus", "guga" } }
        };

        private static readonly string[] MaleNames = {
            "Miguel", "Arthur", "Gael", "Heitor", "Theo", "Davi", "Gabriel", "Bernardo", "Samuel", "João",
            "Enzo", "Pedro", "Lorenzo", "Lucas", "Matheus", "Nicolas", "Guilherme", "Gustavo", "Henrique", "Rafael",
            "Benjamin", "Isaac", "Benício", "Vinícius", "Luca", "Leon", "Vicente", "Francisco", "Antônio", "Joaquim",
            "Eduardo", "Caio", "Calebe", "Enrico", "Davi Lucca", "Breno", "Bryan", "Davi Lucas", "Thomas", "Danilo",
            "Vitor", "André", "Yuri", "Igor", "Murilo", "Luan", "Noah", "Ravi", "Levi", "Daniel",
            "Marcelo", "Ricardo", "Luiz", "Felipe", "Thiago", "Rodrigo", "Bruno", "Leonardo", "Victor", "Alexandre",
            "Renan", "Fernando", "Jorge", "Otávio", "Paulo", "César", "Diego", "Fábio", "Roberto", "Sérgio",
            "Marcos", "Rogério", "Márcio", "Cláudio", "Gilberto", "Júlio", "Augusto", "Renato", "Leandro", "Wagner",
            "Anderson", "Douglas", "Willian", "Everton", "Jonas", "Elias", "Giovanni", "Tales", "Mário", "Silvio"
        };

        private static readonly string[] FemaleNames = {
            "Helena", "Alice", "Laura", "Sophia", "Manuela", "Maitê", "Liz", "Cecília", "Isabella", "Luísa",
            "Eloá", "Heloísa", "Júlia", "Ayla", "Isis", "Elisa", "Antonella", "Valentina", "Maya", "Maria",
            "Esmeralda", "Giovanna", "Beatriz", "Aurora", "Mariana", "Lara", "Lívia", "Lorena", "Melissa", "Nicole",
            "Olívia", "Pietra", "Rafaela", "Sarah", "Yasmin", "Bianca", "Fernanda", "Camila", "Bruna", "Aline",
            "Amanda", "Larissa", "Gabriela", "Natália", "Carolina", "Paula", "Patrícia", "Vanessa", "Juliana", "Cláudia",
            "Priscila", "Tatiane", "Débora", "Renata", "Jaqueline", "Daniele", "Raquel", "Luana", "Regina", "Sônia",
            "Márcia", "Adriana", "Simone", "Sandra", "Vera", "Tânia", "Fátima", "Eliane", "Cristina", "Mônica",
            "Alessandra", "Rosana", "Sueli", "Marta", "Luciana", "Rita", "Cíntia", "Flávia", "Gisele", "Viviane"
        };

        private static readonly string[] LastNames = {
            "Silva", "Santos", "Oliveira", "Souza", "Rodrigues", "Ferreira", "Alves", "Pereira", "Lima", "Gomes",
            "Costa", "Ribeiro", "Martins", "Carvalho", "Almeida", "Lopes", "Soares", "Fernandes", "Vieira", "Barbosa",
            "Rocha", "Dias", "Mendes", "Nunes", "Machado", "Moura", "Castro", "Cardoso", "Medeiros", "Freitas",
            "Batista", "Ramos", "Gonçalves", "Teixeira", "Araújo", "Melo", "Barros", "Cavalcanti", "Correia", "Campos",
            "Moreira", "Neves", "Dantas", "Siqueira", "Monteiro", "Farias", "Guimarães", "Aguiar", "Borges", "Santana",
            "Marques", "Lemos", "Pinto", "Coelho", "Britto", "Nascimento", "Leal", "Pacheco", "Rezende", "Peixoto",
            "Macedo", "Viana", "Fonseca", "Moraes", "Duarte", "Tavares", "Henriques", "Paiva", "Figueiredo", "Braga",
            "Pessoa", "Franco", "Muniz", "Bastos", "Saldanha", "Xavier", "Brandão", "Sales", "Maia", "Lacerda",
            "Antunes", "Guerra", "Vilas Boas", "Furtado", "Veloso", "Drummond", "Chagas", "Padilha", "Goulart", "Assis"
        };

        private string GerarUsernameHumano(string nome, string sobrenome, Random rnd)
        {
            string n = RemoveAccents(nome).ToLower().Replace(" ", "");
            string s = RemoveAccents(sobrenome).ToLower().Replace(" ", "");

            int estrategia = rnd.Next(0, 15);

            switch (estrategia)
            {
                case 0: return $"{n}{s}{rnd.Next(1, 999)}";
                case 1: return $"{n}.{s}{rnd.Next(1, 99)}";
                case 2: return $"{n}_{s}{rnd.Next(10, 99)}";
                case 3: return $"{n}{rnd.Next(1980, 2006)}";
                case 4: return $"{n}_{rnd.Next(100, 9999)}";
                case 5: return $"{s}{n}{rnd.Next(1, 99)}";
                case 6: return $"{n[0]}{s}{rnd.Next(100, 9999)}";
                case 7: return $"{n}_{s[0]}{rnd.Next(10, 999)}";
                case 8: return $"{s}.{n}{rnd.Next(1, 99)}";
                case 9: return $"im_{n}{rnd.Next(1, 999)}";
                case 10: return $"{n}_oficial{rnd.Next(1, 99)}";
                case 11: return $"eu_sou_{n}{rnd.Next(1, 99)}";
                case 12: return $"{n}{s[0]}{s[1]}{rnd.Next(10, 999)}";
                case 13: return $"{n}x{rnd.Next(10, 99)}";
                case 14: return $"{s}_{n}_{rnd.Next(10, 99)}";
                default: return $"{n}{s}{rnd.Next(10000, 99999)}";
            }
        }

        private string GenerateInvalidCpf(Random rnd)
        {
            string baseCpf = "";
            for (int i = 0; i < 9; i++) baseCpf += rnd.Next(0, 10).ToString();
            return baseCpf + rnd.Next(10, 99).ToString();
        }

        private string RemoveAccents(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            var normalizedString = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var c in normalizedString)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}