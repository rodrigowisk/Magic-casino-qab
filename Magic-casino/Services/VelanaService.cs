using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Net.Http.Headers;
using Magic_casino.Models.Integrations;

namespace Magic_casino.Services
{
    public class VelanaService
    {
        private readonly HttpClient _client;
        private readonly string _apiKey;
        private const string BaseUrl = "https://api.velana.com.br/v1";

        public VelanaService(HttpClient client)
        {
            _client = client;
            // Pega a chave da API das variáveis de ambiente
            _apiKey = Environment.GetEnvironmentVariable("VELANA_API_KEY") ?? "";
        }

        public async Task<VelanaTransactionResponse> CreatePixDepositAsync(
            double amountDouble,
            string userCpf,
            string userName,
            string userEmail,
            string? apiKeyOverride = null)
        {
            var effectiveApiKey = apiKeyOverride ?? _apiKey;
            var amountInCents = (int)(amountDouble * 100);

            // Limpa o CPF (deixa apenas números)
            var cleanCpf = Regex.Replace(userCpf, "[^0-9]", "");

            // =========================================================================
            // 🚨 CORREÇÃO CRÍTICA: URL DO WEBHOOK
            // =========================================================================
            // Definimos a URL do seu site fixo. 
            // Removemos a lógica antiga que mandava para o "google.com" quando detectava localhost.
            string postbackUrl = "https://quebrandoabanca.bet/api/Velana/webhook";

            var depositItem = new VelanaItem
            {
                Title = "Credito Cassino",
                Quantity = 1,
                UnitPrice = amountInCents,
                Tangible = false
            };

            var requestBody = new VelanaDepositRequest
            {
                Amount = amountInCents,
                PaymentMethod = "pix",
                Customer = new VelanaCustomer
                {
                    Name = userName,
                    Email = userEmail,
                    Document = new VelanaDocument
                    {
                        Number = cleanCpf,
                        Type = "cpf"
                    }
                },
                PostbackUrl = postbackUrl, // Agora envia a URL certa!
                Items = new List<VelanaItem> { depositItem }
            };

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json"
            );

            var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/transactions");

            // Autenticação Basic Auth
            var credentials = $"{effectiveApiKey}:x";
            var bytes = Encoding.UTF8.GetBytes(credentials);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(bytes));

            // Header User-Agent para evitar bloqueios de segurança
            request.Headers.TryAddWithoutValidation("User-Agent", "MagicCasino/1.0");

            request.Content = jsonContent;

            // Envia a requisição
            var response = await _client.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"--> [RESPOSTA VELANA RAW]: {responseBody}");

            if (response.IsSuccessStatusCode)
            {
                try
                {
                    // Tenta extrair o QR Code usando Regex (mais seguro contra mudanças no JSON)
                    var qrCodeMatch = Regex.Match(responseBody, "\"qrcode\"\\s*:\\s*\"([^\"]+)\"");
                    var qrCodeUrlMatch = Regex.Match(responseBody, "\"qrcode_url\"\\s*:\\s*\"([^\"]+)\"");
                    var idMatch = Regex.Match(responseBody, "\"id\"\\s*:\\s*(\\d+)");

                    var qrCode = qrCodeMatch.Success ? qrCodeMatch.Groups[1].Value : null;
                    var qrCodeUrl = qrCodeUrlMatch.Success ? qrCodeUrlMatch.Groups[1].Value : null;
                    long id = idMatch.Success ? long.Parse(idMatch.Groups[1].Value) : 0L;

                    if (!string.IsNullOrEmpty(qrCode))
                    {
                        Console.WriteLine("--> [SUCESSO] QR Code extraído!");
                        return new VelanaTransactionResponse
                        {
                            Id = id,
                            Status = "created",
                            Amount = amountInCents,
                            Pix = new VelanaPixData
                            {
                                QrCode = qrCode,
                                QrCodeUrl = qrCodeUrl
                            }
                        };
                    }

                    // Fallback: Tenta deserializar o objeto completo se o Regex falhar
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var obj = JsonSerializer.Deserialize<VelanaTransactionResponse>(responseBody, options);
                    if (obj?.Pix?.QrCode != null) return obj;

                    throw new Exception("Campo 'qrcode' não encontrado na resposta.");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"--> [ERRO EXTRAÇÃO]: {e.Message}");
                    throw new Exception($"Erro ao ler dados da Velana: {e.Message}");
                }
            }
            else
            {
                throw new Exception($"Velana recusou o pedido ({response.StatusCode}): {responseBody}");
            }
        }
    }
}