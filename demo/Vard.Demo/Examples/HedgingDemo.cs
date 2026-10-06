using System;
using System.Threading.Tasks;
using Vard.Builders;

namespace Vard.Demo.Examples
{
    public static class HedgingDemo
    {
        public static async Task RunAsync()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("================================================================================");
            Console.WriteLine(" 6. HEDGING DEMO (Execuções Especulativas Concorrentes para Baixa Latência)");
            Console.WriteLine("================================================================================");
            Console.ResetColor();

            int attemptTracker = 0;

            // Se a primeira chamada demorar mais de 100ms, dispara uma 2ª tentativa em paralelo (hedge).
            // A que responder primeiro vence e cancela imediatamente a outra.
            var hedgingPolicy = VardPolicy
                .Handle<TimeoutException>()
                .Hedging<string>(
                    maxHedges: 1,
                    hedgingDelay: TimeSpan.FromMilliseconds(100),
                    onHedgingResultAsync: (result, attempt, elapsed, ctx) =>
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"   [OnHedgingResult] Tentativa #{attempt} foi a vencedora em {elapsed.TotalMilliseconds:F0}ms!");
                        Console.ResetColor();
                        return Task.CompletedTask;
                    });

            Console.WriteLine("-> Disparando Hedging: Tentativa #1 lenta (400ms) vs Tentativa #2 rápida (50ms pós-delay de 100ms)...");

            var winnerResult = await hedgingPolicy.ExecuteAsync(async ct =>
            {
                int currentAttempt = System.Threading.Interlocked.Increment(ref attemptTracker);
                if (currentAttempt == 1)
                {
                    Console.WriteLine("   --> [Hedge #1] Iniciado (simulando latência de 400ms)...");
                    await Task.Delay(400, ct);
                    Console.WriteLine("   --> [Hedge #1] Concluído.");
                    return "Resposta da Tentativa #1";
                }
                else
                {
                    Console.WriteLine("   --> [Hedge #2 (Especulativo)] Iniciado após 100ms ociosos! (simulando latência rápida de 50ms)...");
                    await Task.Delay(50, ct);
                    Console.WriteLine("   --> [Hedge #2] Concluído!");
                    return "Resposta rápida da Tentativa #2 (VENCEDORA)";
                }
            });

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[VENCEDOR OBTIDO] Resultado retornado: \"{winnerResult}\"\n");
            Console.ResetColor();
        }
    }
}
