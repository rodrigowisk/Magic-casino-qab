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

        // 3. Creditar Prêmio (Versão Simples)
        public async Task<(bool Success, string Message)> CreditFundsAsync(string userCpf, decimal amount)
        {
            var payload = new
            {
                UserCpf = userCpf,
                Amount = amount,
                Source = "Sportbook",
                Type = "Win",
                ReferenceId = Guid.NewGuid().ToString()
            };

            return await SendTransactionAsync("/api/internal/wallet/credit", payload, null);
        }

        // 4. ✅ NOVO MÉTODO OBRIGATÓRIO (Para corrigir o erro de Build do LiveScoreWorker)
        // Permite adicionar saldo passando uma descrição personalizada (ex: "Prêmio Aposta #123")
        public async Task<(bool Success, string Message)> AddBalanceAsync(string userCpf, decimal amount, string description)
        {
            var payload = new
            {
                UserCpf = userCpf,
                Amount = amount,
                Source = "Sportbook",
                Type = "Win",
                Description = description, // Envia a descrição para o extrato
                ReferenceId = Guid.NewGuid().ToString()
            };

            // Reutiliza a rota de crédito
            return await SendTransactionAsync("/api/internal/wallet/credit", payload, null);
        }

        // 5. Estornar Aposta (Reembolso)
        public async Task<(bool Success, string Message)> ProcessRefundAsync(string userCpf, decimal amount, string description)
        {
            var payload = new
            {
                UserCpf = userCpf,
                Amount = amount,
                Source = "Sportbook",
                Type = "Refund",
                Description = description,
                ReferenceId = Guid.NewGuid().ToString()
            };

            return await SendTransactionAsync("/api/internal/wallet/credit", payload, null);
        }

        // 6. Método Auxiliar
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