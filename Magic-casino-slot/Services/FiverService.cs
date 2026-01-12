using Magic_casino_slot.DTOs;
using System.Text.Json;
using System.Net.Http.Json; // Necessário para o PostAsJsonAsync

namespace Magic_casino_slot.Services
{
    public class FiverService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public FiverService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        // --- SEU MÉTODO ORIGINAL (Mantido) ---
        public async Task<string> LaunchGameAsync(string userCode, string providerCode, string gameCode)
        {
            var apiUrl = _configuration["Fiver:Url"];
            var agentCode = _configuration["Fiver:AgentCode"];
            var agentToken = _configuration["Fiver:AgentToken"];

            var payload = new
            {
                method = "game_launch",
                agent_code = agentCode,
                agent_token = agentToken,
                user_code = userCode,
                provider_code = providerCode,
                game_code = gameCode,
                lang = "pt"
            };

            var response = await _httpClient.PostAsJsonAsync(apiUrl, payload);
            var responseString = await response.Content.ReadAsStringAsync();

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var fiverResponse = JsonSerializer.Deserialize<FiverLaunchResponse>(responseString, options);

            if (fiverResponse == null || fiverResponse.Status != 1 || string.IsNullOrEmpty(fiverResponse.LaunchUrl))
            {
                throw new Exception(fiverResponse?.Msg ?? "Erro desconhecido ao lançar jogo na Fiver.");
            }

            return fiverResponse.LaunchUrl;
        }

        // --- MÉTODOS QUE FALTAVAM (Adicionados para corrigir o erro do SyncController) ---

        public async Task<string> GetProviderListAsync()
        {
            var apiUrl = _configuration["Fiver:Url"];
            var agentCode = _configuration["Fiver:AgentCode"];
            var agentToken = _configuration["Fiver:AgentToken"];

            var payload = new
            {
                method = "provider_list",
                agent_code = agentCode,
                agent_token = agentToken
            };

            // Envia para a Fiver pedindo a lista
            var response = await _httpClient.PostAsJsonAsync(apiUrl, payload);
            return await response.Content.ReadAsStringAsync();
        }

        // Ajustado para receber o providerCode (string) que o SyncController está enviando
        public async Task<string> GetGameListAsync(string providerCode)
        {
            var apiUrl = _configuration["Fiver:Url"];
            var agentCode = _configuration["Fiver:AgentCode"];
            var agentToken = _configuration["Fiver:AgentToken"];

            var payload = new
            {
                method = "game_list",
                agent_code = agentCode,
                agent_token = agentToken,
                provider_code = providerCode // <--- Agora enviamos o filtro que o Controller pediu
            };

            // Envia para a Fiver pedindo a lista de jogos desse provedor
            var response = await _httpClient.PostAsJsonAsync(apiUrl, payload);
            return await response.Content.ReadAsStringAsync();
        }
    }
}