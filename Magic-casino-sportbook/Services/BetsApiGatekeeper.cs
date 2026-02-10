using StackExchange.Redis;

namespace Magic_casino_sportbook.Services
{
    public class BetsApiGatekeeper
    {
        private readonly IDatabase _redisDb;

        // A BetsAPI suporta cerca de 3 a 4 por segundo (estamos sendo seguros com 3)
        private const int MAX_GLOBAL_LIMIT = 3;

        // Limite para os robôs pesados (PreMatch e Grade). Eles só podem usar 1 "ficha" por segundo.
        private const int MAX_LOW_PRIORITY_LIMIT = 1;

        public BetsApiGatekeeper(IConnectionMultiplexer redis)
        {
            _redisDb = redis.GetDatabase();
        }

        public async Task WaitAsync(bool isLivePriority = false)
        {
            while (true)
            {
                long currentSecond = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                string redisKey = $"betsapi_ratelimit_{currentSecond}";

                // 1. "Espia" como está a fila ANTES de entrar para não sujar o contador
                var currentValStr = await _redisDb.StringGetAsync(redisKey);
                long currentVal = 0;
                if (!string.IsNullOrEmpty(currentValStr)) long.TryParse(currentValStr, out currentVal);

                // Define quantas vagas esse robô tem direito a enxergar
                int allowedLimit = isLivePriority ? MAX_GLOBAL_LIMIT : MAX_LOW_PRIORITY_LIMIT;

                if (currentVal < allowedLimit)
                {
                    // Tem vaga! Incrementa a catraca de forma atômica
                    long requestCount = await _redisDb.StringIncrementAsync(redisKey);

                    if (requestCount == 1)
                    {
                        // Se foi o primeiro, manda o Redis limpar o lixo depois de 3 segundos
                        await _redisDb.KeyExpireAsync(redisKey, TimeSpan.FromSeconds(3));
                    }

                    // Se confirmou a vaga, passa direto!
                    if (requestCount <= allowedLimit)
                    {
                        return;
                    }
                }

                // Se a fila lotou, dorme e tenta de novo. 
                // O Ao Vivo acorda muito mais rápido para "furar a fila" no segundo seguinte.
                int sleepTime = isLivePriority ? 100 : 600;
                await Task.Delay(sleepTime);
            }
        }

        public void Release()
        {
            // O Redis controla a liberação por tempo. Deixamos vazio.
        }
    }
}