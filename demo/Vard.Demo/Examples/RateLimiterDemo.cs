using System;
using System.Threading.Tasks;
using Vard.Abstractions;
using Vard.Builders;

namespace Vard.Demo.Examples
{
    public static class RateLimiterDemo
    {
        public static async Task RunAsync()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("================================================================================");
            Console.WriteLine(" 5. RATE LIMITER DEMO (Token Bucket: Capacidade 3, Recarga 2 tokens/seg)");
            Console.WriteLine("================================================================================");
            Console.ResetColor();

            // Permite burst inicial de 3 tokens, recarrega a 2 tokens por segundo
            var rateLimiter = VardPolicy.TokenBucket(
                maxTokens: 3,
                tokensPerSecond: 2.0,
                onRateLimitExceeded: (retryAfter, ctx) =>
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"   [OnRateLimitExceeded] Limite atingido! Próximo token disponível em {retryAfter.TotalMilliseconds:F0}ms.");
                    Console.ResetColor();
                });

            Console.WriteLine("-> Disparando 5 requisições rápidas em sequência (burst de 3 tokens esperados)...");

            for (int i = 1; i <= 5; i++)
            {
                try
                {
                    rateLimiter.Execute(() =>
                    {
                        Console.WriteLine($"   [Request #{i}] Consumiu 1 token e executou!");
                        return true;
                    });
                }
                catch (RateLimiterRejectedException ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"   [Request #{i}] REJEITADA! RetryAfter: {ex.RetryAfter.TotalMilliseconds:F0}ms");
                    Console.ResetColor();
                }
            }

            Console.WriteLine("\n-> Aguardando 1.1s para recarga automática e contínua matemática de ~2 tokens...");
            await Task.Delay(1100);

            Console.WriteLine("-> Tentando 2 novas requisições pós-recarga...");
            for (int i = 6; i <= 7; i++)
            {
                try
                {
                    rateLimiter.Execute(() =>
                    {
                        Console.WriteLine($"   [Request #{i}] Sucesso após recarga!");
                        return true;
                    });
                }
                catch (RateLimiterRejectedException ex)
                {
                    Console.WriteLine($"   [Request #{i}] Rejeitado: {ex.Message}");
                }
            }

            Console.WriteLine();
        }
    }
}
