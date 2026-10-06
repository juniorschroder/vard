using System;
using System.Threading;
using System.Threading.Tasks;
using Vard.Abstractions;
using Vard.Builders;

namespace Vard.Demo.Examples
{
    public static class TimeoutAndFallbackDemo
    {
        public static async Task RunAsync()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("================================================================================");
            Console.WriteLine(" 3. TIMEOUT & FALLBACK DEMO (Cancelamento Cooperativo + Degradação Graciosa)");
            Console.WriteLine("================================================================================");
            Console.ResetColor();

            // Configura política de Timeout de 200ms
            var timeoutPolicy = VardPolicy.Timeout(
                timeout: TimeSpan.FromMilliseconds(200),
                onTimeoutAsync: (ctx, ts) =>
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"   [OnTimeout] Operação excedeu {ts.TotalMilliseconds:F0}ms e foi cancelada cooperativamente.");
                    Console.ResetColor();
                    return Task.CompletedTask;
                });

            // Configura política de Fallback para retornar dados em cache/offline caso o Timeout ocorra
            var fallbackPolicy = VardPolicy
                .Handle<TimeoutRejectedException>()
                .Or<OperationCanceledException>()
                .Fallback(
                    fallbackValue: "Dados estáticos de fallback (modo degradado/cache local)",
                    onFallback: (ex, ctx) =>
                    {
                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                        Console.WriteLine($"   [OnFallback] Acionado devido a {ex?.GetType().Name}. Fornecendo resposta alternativa segura.");
                        Console.ResetColor();
                    });

            // Compõe Fallback(Timeout)
            var resilientPipeline = fallbackPolicy.Wrap(timeoutPolicy);

            Console.WriteLine("-> Executando operação de longa duração (500ms) sob pipeline Fallback -> Timeout(200ms)...");

            var finalResult = await resilientPipeline.ExecuteAsync(async ct =>
            {
                Console.WriteLine("   --> Operação iniciada. Simulando processamento pesado de 500ms...");
                await Task.Delay(500, ct); // Respeita o CancellationToken cooperativo
                return "Dados frescos do servidor remoto";
            });

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[RESULTADO FINAL] {finalResult}\n");
            Console.ResetColor();
        }
    }
}
