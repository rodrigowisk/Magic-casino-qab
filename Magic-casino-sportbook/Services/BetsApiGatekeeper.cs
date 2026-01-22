using System.Threading.RateLimiting;

namespace Magic_casino_sportbook.Services
{
    public class BetsApiGatekeeper : IDisposable
    {
        private readonly TokenBucketRateLimiter _limiter;
        private readonly HttpClient _httpClient; // O campo correto é este

        public BetsApiGatekeeper(IHttpClientFactory httpClientFactory)
        {
            // CORREÇÃO: Atribuindo à variável _httpClient declarada acima
            _httpClient = httpClientFactory.CreateClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(10);

            _limiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
            {
                TokenLimit = 1,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 100,
                ReplenishmentPeriod = TimeSpan.FromSeconds(1.1),
                TokensPerPeriod = 1,
                AutoReplenishment = true
            });
        }

        public async Task<string?> SafeGetAsync(string url)
        {
            using RateLimitLease lease = await _limiter.AcquireAsync(permitCount: 1);

            if (lease.IsAcquired)
            {
                try
                {
                    var response = await _httpClient.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        return await response.Content.ReadAsStringAsync();
                    }
                    else if ((int)response.StatusCode == 429)
                    {
                        Console.WriteLine("⛔ [GATEKEEPER] 429 recebido. Pausando.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ [GATEKEEPER] Erro HTTP: {ex.Message}");
                }
            }
            return null;
        }

        public void Dispose()
        {
            _limiter.Dispose();
        }
    }
}