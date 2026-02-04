using System.Net.Http.Headers;
using System.Net.Http.Json; // Certifique-se de que este using existe para os métodos de extensão Json

namespace Magic_casino_tournament.Services
{
    public interface ICoreGateway
    {
        Task<CoreResponse> DeductFundsAsync(string userId, decimal amount, string token);
    }

    public class CoreGateway : ICoreGateway
    {
        private readonly HttpClient _httpClient;

        // O nome do serviço no Docker é "core", porta 8080
        // Rota interna correta
        private const string CORE_URL = "http://core:8080/api/internal/wallet/deduct";

        public CoreGateway(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<CoreResponse> DeductFundsAsync(string userId, decimal amount, string token)
        {
            try
            {
                // Configura o Token JWT para o Core autorizar
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Replace("Bearer ", ""));

                // ✅ CORREÇÃO AQUI: Adicionado Type e Source para o filtro dinâmico
                var payload = new
                {
                    UserCpf = userId,
                    Amount = amount,
                    Description = "Inscrição em Torneio",
                    ReferenceId = Guid.NewGuid().ToString(), // Idempotência

                    // Campos novos para o Core identificar corretamente
                    Type = "tournament",   // Para cair no filtro "Torneio"
                    Source = "Tournament"  // Origem da transação
                };

                var response = await _httpClient.PostAsJsonAsync(CORE_URL, payload);

                if (response.IsSuccessStatusCode)
                {
                    // Lê o ID da transação que o Core gerou
                    var result = await response.Content.ReadFromJsonAsync<CoreTransactionResult>();
                    return new CoreResponse { Success = true, TransactionId = result?.TransactionId };
                }

                // Tenta ler o erro retornado pelo Core se houver
                var errorContent = await response.Content.ReadAsStringAsync();
                return new CoreResponse { Success = false, Message = $"Falha no Core: {response.StatusCode} - {errorContent}" };
            }
            catch (Exception ex)
            {
                return new CoreResponse { Success = false, Message = $"Erro de comunicação: {ex.Message}" };
            }
        }
    }

    // DTOs auxiliares
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