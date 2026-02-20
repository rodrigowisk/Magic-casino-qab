using Magic_casino_sportbook.Services; // Para referenciar o BetsApiGatekeeper existente
using Microsoft.Extensions.Logging;

namespace Magic_casino_sportbook.Services.Gateways
{
    public class BetsApiHttpService
    {
        private readonly HttpClient _client;
        private readonly string _token;
        private readonly BetsApiGatekeeper _gatekeeper;
        private readonly ILogger<BetsApiHttpService> _logger;
        private const string BASE_URL = "https://api.b365api.com";

        public BetsApiHttpService(
            IHttpClientFactory factory,
            BetsApiGatekeeper gatekeeper,
            ILogger<BetsApiHttpService> logger)
        {
            _client = factory.CreateClient();
            // Timeout generoso para evitar cancelamentos prematuros em lotes grandes
            _client.Timeout = TimeSpan.FromSeconds(60);
            _gatekeeper = gatekeeper;
            _logger = logger;
            _token = Environment.GetEnvironmentVariable("BETSAPI_TOKEN") ?? "";
        }

        /// <summary>
        /// Realiza um GET na BetsAPI gerenciando o Rate Limit automaticamente.
        /// </summary>
        /// <param name="endpoint">O caminho da API (ex: /v1/bet365/upcoming)</param>
        /// <param name="isLivePriority">Se true, fura a fila (usa prioridade alta do Gatekeeper)</param>
        public async Task<HttpResponseMessage> GetAsync(string endpoint, bool isLivePriority = false)
        {
            // 1. Aguarda a vez no semáforo global (evita 429 Too Many Requests)
            await _gatekeeper.WaitAsync(isLivePriority);

            try
            {
                // 2. Monta a URL garantindo que o token esteja presente
                var url = $"{BASE_URL}{endpoint}";

                // Adiciona o token se ainda não estiver na query string
                if (!url.Contains("token="))
                {
                    var separator = url.Contains("?") ? "&" : "?";
                    url += $"{separator}token={_token}";
                }

                // 3. Executa a requisição
                var response = await _client.GetAsync(url);

                // 4. Log de aviso se der Rate Limit (para debug)
                if ((int)response.StatusCode == 429)
                {
                    _logger.LogWarning($"⛔ [BetsApiHttpService] 429 Detectado na rota: {endpoint}");
                    await _gatekeeper.Report429Async();
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ [BetsApiHttpService] Falha silenciosa (retornando null): {ex.Message}");
                return null;
            }
            // O Gatekeeper não precisa de 'Release' manual aqui pois ele usa 
            // um atraso fixo interno (Task.Delay) no método WaitAsync, baseado na sua implementação atual.
        }
    }
}