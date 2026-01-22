using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Magic_casino_sportbook.Services
{
    public class CoreWalletService
    {
        private readonly HttpClient _httpClient;

        public CoreWalletService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        // 1. Consultar Saldo
        public async Task<decimal> GetBalanceAsync(string userCpf, string token)
        {
            try
            {
                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", token.Replace("Bearer ", ""));
                }

                // Busca o saldo da carteira de apostas (Qab)
                var response = await _httpClient.GetAsync($"/api/internal/wallet/balance/{userCpf}");

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.Content.ReadFromJsonAsync<decimal>();
                    return data;
                }
                return 0m;
            }
            catch
            {
                return 0m;
            }
        }

        // 2. Debitar Aposta (Usado pelo Controller ao criar a aposta)
        public async Task<(bool Success, string Message)> DeductFundsAsync(string userCpf, decimal amount, string token)
        {
            var payload = new
            {
                UserCpf = userCpf,
                Amount = amount,
                Source = "Sportbook",
                ReferenceId = Guid.NewGuid().ToString()
            };

            return await SendTransactionAsync("/api/internal/wallet/deduct", payload, token);
        }

        // 3. Creditar Prêmio (Usado pelo Worker ao pagar a aposta)
        public async Task<(bool Success, string Message)> CreditFundsAsync(string userCpf, decimal amount)
        {
            var payload = new
            {
                UserCpf = userCpf,
                Amount = amount,
                Source = "Sportbook", // O Core deve identificar "Sportbook" e somar no balance_qab
                Type = "Win",         // Tipo da transação
                ReferenceId = Guid.NewGuid().ToString()
            };

            // Worker não tem token de usuário, passa null (o Core deve aceitar chamada interna)
            return await SendTransactionAsync("/api/internal/wallet/credit", payload, null);
        }

        // 4. Método Auxiliar (Para não repetir código)
        private async Task<(bool Success, string Message)> SendTransactionAsync(string endpoint, object payload, string? token)
        {
            try
            {
                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", token.Replace("Bearer ", ""));
                }

                var response = await _httpClient.PostAsJsonAsync(endpoint, payload);

                if (response.IsSuccessStatusCode)
                {
                    return (true, "Sucesso");
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                Console.WriteLine($">>>>> [ERRO CORE] Endpoint: {endpoint} | Status: {response.StatusCode} | Motivo: {errorMsg}");

                return (false, $"Erro Core ({response.StatusCode}): {errorMsg}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($">>>>> [ERRO CONEXAO] {ex.Message}");
                return (false, $"Erro de Conexão: {ex.Message}");
            }
        }
    }
}