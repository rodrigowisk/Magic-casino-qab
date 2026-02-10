using System.Collections.Concurrent;

namespace Magic_casino_sportbook.Services
{
    public class BetsApiGatekeeper
    {
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        // ⚡ ACELERADO PARA 200ms (5 requisições por segundo)
        private readonly TimeSpan _minInterval = TimeSpan.FromMilliseconds(200);

        private DateTime _nextAllowedExecution = DateTime.MinValue;

        public async Task<T> ExecuteAsync<T>(Func<Task<T>> action)
        {
            await _semaphore.WaitAsync();
            try
            {
                var now = DateTime.UtcNow;
                if (now < _nextAllowedExecution)
                {
                    await Task.Delay(_nextAllowedExecution - now);
                }
                var result = await action();
                _nextAllowedExecution = DateTime.UtcNow.Add(_minInterval);
                return result;
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}