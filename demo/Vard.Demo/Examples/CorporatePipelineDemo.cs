using Vard.Abstractions;
using Vard.Builders;

namespace Vard.Demo.Examples
{
    public static class CorporatePipelineDemo
    {
        public static async Task RunAsync()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("================================================================================");
            Console.WriteLine(" 7. PIPELINE CORPORATIVO COMPLETO (Fallback -> Retry -> CircuitBreaker -> Timeout)");
            Console.WriteLine("================================================================================");
            Console.ResetColor();

            // 1. Fallback (Mais externa)
            var fallback = VardPolicy
                .Handle<Exception>()
                .Fallback(
                    fallbackValue: "Resposta de emergência do Cache Local",
                    onFallback: (ex, ctx) =>
                    {
                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                        Console.WriteLine($"   [Camada Fallback] Resgatando falha: {ex?.GetType().Name} -> {ex?.Message}");
                        Console.ResetColor();
                    });

            // 2. Retry
            var retry = VardPolicy
                .Handle<HttpRequestException>()
                .Or<TimeoutRejectedException>()
                .RetryWithBackoff(
                    retryCount: 2,
                    initialDelay: TimeSpan.FromMilliseconds(50),
                    backoffType: BackoffType.Fixed,
                    useJitter: false,
                    onRetryAsync: (ex, res, attempt, delay, ctx) =>
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"   [Camada Retry] Tentativa #{attempt} falhou ({ex?.GetType().Name}). Retentando em {delay.TotalMilliseconds:F0}ms...");
                        Console.ResetColor();
                        return Task.CompletedTask;
                    });

            // 3. Circuit Breaker
            var circuitBreaker = VardPolicy
                .Handle<HttpRequestException>()
                .Or<TimeoutRejectedException>()
                .CircuitBreaker(
                    exceptionsAllowedBeforeBreaking: 2,
                    durationOfBreak: TimeSpan.FromSeconds(2),
                    onBreak: (ex, duration, ctx) =>
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"   [Camada CircuitBreaker] Circuito ABRIU! Protegendo recursos downstream por {duration.TotalSeconds}s.");
                        Console.ResetColor();
                    });

            // 4. Timeout (Mais interna)
            var timeout = VardPolicy.Timeout(
                timeout: TimeSpan.FromMilliseconds(150),
                onTimeout: (ctx, ts) =>
                {
                    Console.WriteLine($"   [Camada Timeout] Operação excedeu {ts.TotalMilliseconds:F0}ms.");
                });

            // Encadeamento canônico: Fallback -> Retry -> CircuitBreaker -> Timeout
            var fullPipeline = fallback
                .Wrap(retry)
                .Wrap(circuitBreaker)
                .Wrap(timeout);

            // Contexto compartilhado com CorrelationId
            var context = new Dictionary<string, object>
            {
                ["CorrelationId"] = Guid.NewGuid().ToString("N")[..8],
                ["Tenant"] = "Enterprise-Client-01"
            };

            Console.WriteLine($"-> Executando chamada via pipeline composto com CorrelationId: {context["CorrelationId"]}...");
            Console.WriteLine("-> Simulando timeout severo de 300ms (superior aos 150ms do Timeout)...");

            int callCount = 0;
            var response = await fullPipeline.ExecuteAsync(async (ctx, ct) =>
            {
                callCount++;
                Console.WriteLine($"   --> [Target Service] Executando chamada #{callCount} (Tenant: {ctx?["Tenant"]})...");
                await Task.Delay(300, ct); // excede os 150ms de timeout
                return "Dados do Gateway";
            }, context);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[RESPOSTA ENTREGUE AO CLIENTE] \"{response}\"");
            Console.WriteLine($"[ESTADO DO CIRCUITO] {circuitBreaker.CircuitState}\n");
            Console.ResetColor();
        }
    }
}
