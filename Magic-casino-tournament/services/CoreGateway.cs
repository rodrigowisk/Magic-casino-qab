using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;

namespace Magic_casino_tournament.Services
{
    public interface ICoreGateway
    {
        Task<CoreResponse> DeductFundsAsync(string userId, decimal amount, string token);
        Task<CoreResponse> AddFundsAsync(string userId, decimal amount, string description = "Prêmio de Torneio");
    }

    public class CoreGateway : ICoreGateway
    {
        private readonly HttpClient _httpClient;
        private readonly string _systemMasterToken;

        // Rotas do Core
        private const string CORE_DEDUCT_URL = "http://core:8080/api/internal/wallet/deduct";
        private const string CORE_CREDIT_URL = "http://core:8080/api/internal/wallet/credit";

        public CoreGateway(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            // Pega o token do appsettings.json ou Variável de Ambiente, ou usa um default para dev
            _systemMasterToken = config["SYSTEM_MASTER_TOKEN"] ?? "SEU_TOKEN_INTERNO_SECRETO";
        }

        public async Task<CoreResponse> DeductFundsAsync(string userId, decimal amount, string token)
        {
            try
            {
                // Usa o token do usuário que veio na requisição
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Replace("Bearer ", ""));

                var payload = new
                {
                    UserCpf = userId,
                    Amount = amount,
                    Description = "Inscrição em Torneio",
                    ReferenceId = Guid.NewGuid().ToString(),
                    Type = "tournament",
                    Source = "Tournament"
                };

                var response = await _httpClient.PostAsJsonAsync(CORE_DEDUCT_URL, payload);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<CoreTransactionResult>();
                    return new CoreResponse { Success = true, TransactionId = result?.TransactionId };
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                return new CoreResponse { Success = false, Message = $"Falha no Core: {response.StatusCode} - {errorContent}" };
            }
            catch (Exception ex)
            {
                return new CoreResponse { Success = false, Message = $"Erro de comunicação: {ex.Message}" };
            }
        }

        public async Task<CoreResponse> AddFundsAsync(string userId, decimal amount, string description)
        {
            try
            {
                // Usa o token MESTRE do sistema, pois o robô não tem sessão de usuário
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _systemMasterToken);

                var payload = new
                {
                    UserCpf = userId,
                    Amount = amount,
                    Description = description,
                    ReferenceId = Guid.NewGuid().ToString(),
                    Type = "tournament_prize", // Tag para extrato
                    Source = "Tournament"
                };

                var response = await _httpClient.PostAsJsonAsync(CORE_CREDIT_URL, payload);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<CoreTransactionResult>();
                    return new CoreResponse { Success = true, TransactionId = result?.TransactionId };
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                return new CoreResponse { Success = false, Message = $"Core Error: {response.StatusCode} - {errorContent}" };
            }
            catch (Exception ex)
            {
                return new CoreResponse { Success = false, Message = ex.Message };
            }
        }
    }

    public class CoreResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? TransactionId { get; set; }
    }

    public class CoreTransactionResult
    {
        public string TransactionId { get; set; }
        public decimal NewBalance { get; set; }
    }
}