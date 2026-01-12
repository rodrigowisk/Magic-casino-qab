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


        // Método novo para consultar saldo
        public async Task<decimal> GetBalanceAsync(string userCpf, string token)
        {
            try
            {
                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", token.Replace("Bearer ", ""));
                }

                // Chama o Core para pegar o saldo da carteira de apostas (Qab)
                // Nota: O Core precisa ter um endpoint GET /api/internal/wallet/{cpf} ou similar.
                // Se não tiver, vamos usar a rota de User do Core se houver, ou criar uma query direta.
                // Vamos assumir uma rota simples de consulta no Core:
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


        public async Task<(bool Success, string Message)> DeductFundsAsync(string userCpf, decimal amount, string token)
        {
            // ✅ CORREÇÃO: Adicionado 'ReferenceId' que o Core exige
            var payload = new
            {
                UserCpf = userCpf,
                Amount = amount,
                Source = "Sportbook",
                ReferenceId = Guid.NewGuid().ToString() // Gera um ID único para a transação
            };

            try
            {
                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", token.Replace("Bearer ", ""));
                }

                var response = await _httpClient.PostAsJsonAsync("/api/internal/wallet/deduct", payload);

                if (response.IsSuccessStatusCode)
                {
                    return (true, "Sucesso");
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                Console.WriteLine($">>>>> [ERRO CORE] Status: {response.StatusCode} | Motivo: {errorMsg}");

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