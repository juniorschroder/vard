using Vard.Abstractions;
using Vard.Builders;

namespace Vard.Demo.Examples
{
    public static class RetryDemo
    {
        public static async Task RunAsync()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("================================================================================");
            Console.WriteLine(" 1. RETRY POLICY DEMO (Exponential Backoff com AWS Full Jitter)");
            Console.WriteLine("================================================================================");
            Console.ResetColor();

            int attemptCounter = 0;

            // Configura uma política de retry com 3 tentativas e backoff exponencial com Jitter
            var retryPolicy = VardPolicy
                .Handle<HttpRequestException>()
                .Or<TimeoutException>()
                .RetryWithBackoff(
                    retryCount: 3,
                    initialDelay: TimeSpan.FromMilliseconds(100),
                    backoffType: BackoffType.Exponential,
                    useJitter: true,
                    onRetryAsync: (ex, result, attempt, delay, context) =>
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"   [Retry Callback] Tentativa #{attempt} falhou ({ex?.GetType().Name}: {ex?.Message}). Aguardando {delay.TotalMilliseconds:F0}ms antes de retentar...");
                        Console.ResetColor();
                        return Task.CompletedTask;
                    });

            Console.WriteLine("-> Executando chamada com falhas simuladas nas 2 primeiras tentativas...");

            try
            {
                var result = await retryPolicy.ExecuteAsync(async ct =>
                {
                    attemptCounter++;
                    Console.WriteLine($"   --> Executando tentativa #{attemptCounter}...");
                    await Task.Yield();
                    if (attemptCounter < 3)
                    {
                        throw new HttpRequestException($"Falha transitória na rede HTTP (503 Service Unavailable) na tentativa #{attemptCounter}");
                    }

                    return "Payload HTTP recebido com sucesso!";
                });

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[SUCESSO] Resultado: \"{result}\" (Completado na tentativa #{attemptCounter})\n");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[FALHA] {ex.Message}\n");
                Console.ResetColor();
            }
        }
    }
}
