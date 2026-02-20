using StackExchange.Redis;
using System.Diagnostics;

namespace Magic_casino_sportbook.Services
{
    public class BetsApiGatekeeper
    {
        private readonly IDatabase _redisDb;

        // ⚙️ CONFIGURAÇÃO DE VELOCIDADE
        // 3 requisições/segundo = 333ms. Usamos 340ms para margem.
        private const int INTERVAL_MS = 500;

        // ✅ SCRIPT CORRIGIDO:
        // 1. Usa @key, @interval, @now para o C# fazer o binding correto.
        // 2. Retorna string formatada para evitar notação científica (ex: 1.2e+12)
        private const string LUA_SCRIPT = @"
            local key = @key
            local interval = tonumber(@interval)
            local now = tonumber(@now)

            -- Pega o último horário agendado (ou 0 se não existir)
            local last_scheduled = tonumber(redis.call('GET', key) or 0)

            -- O próximo slot é o maior entre (Agora) e (Último + Intervalo)
            local next_slot = last_scheduled + interval

            if next_slot < now then
                next_slot = now
            end

            -- Formata como número inteiro string para salvar sem perder precisão
            local next_slot_str = string.format('%.0f', next_slot)

            redis.call('SET', key, next_slot_str)
            redis.call('EXPIRE', key, 3600)

            return next_slot_str
        ";

        public BetsApiGatekeeper(IConnectionMultiplexer redis)
        {
            _redisDb = redis.GetDatabase();
        }

        public async Task WaitAsync(bool isLivePriority = false)
        {
            // 1. Verifica Circuit Breaker
            while (await _redisDb.KeyExistsAsync("api_blocked"))
            {
                await Task.Delay(1000);
            }

            // 2. Prepara os dados em Milissegundos (Unix Time)
            // Ticks estouram a precisão do Lua (52 bits), mas Milliseconds cabem bem.
            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long intervalMs = INTERVAL_MS;

            // 3. Executa o Script
            var result = await _redisDb.ScriptEvaluateAsync(
                LuaScript.Prepare(LUA_SCRIPT),
                new
                {
                    key = (RedisKey)"betsapi_schedule_queue",
                    interval = intervalMs,
                    now = nowMs
                }
            );

            // O Lua retorna o agendamento como String. Convertemos para long.
            long scheduledMs = long.Parse((string)result);

            // 4. Calcula espera
            long msToWait = scheduledMs - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            if (msToWait > 0)
            {
                // Console.WriteLine($"⏳ [FILA] Aguardando {msToWait}ms...");
                await Task.Delay((int)msToWait);
            }
        }

        public async Task Report429Async()
        {
            await _redisDb.StringSetAsync("api_blocked", "true", TimeSpan.FromSeconds(60));
            Console.WriteLine("🚨 [PÂNICO] 429 Detectado! Fila pausada globalmente por 60s.");
        }
    }
}