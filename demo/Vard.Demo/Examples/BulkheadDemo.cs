using Vard.Abstractions;
using Vard.Builders;

namespace Vard.Demo.Examples
{
    public static class BulkheadDemo
    {
        public static async Task RunAsync()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("================================================================================");
            Console.WriteLine(" 4. BULKHEAD DEMO (Isolamento de Concorrência: Max 2 Paralelos, Fila de 1)");
            Console.WriteLine("================================================================================");
            Console.ResetColor();

            // Bulkhead: Max 2 tarefas simultâneas, fila para mais 1. Total = 3 suportadas, 4ª é rejeitada imediatamente
            var bulkhead = VardPolicy.Bulkhead(
                maxParallelization: 2,
                maxQueuedActions: 1,
                onBulkheadRejectedAsync: ctx =>
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("   [OnBulkheadRejected] Requisição rejeitada! Slots de execução e fila saturados.");
                    Console.ResetColor();
                    return Task.CompletedTask;
                });

            Console.WriteLine("-> Disparando 5 requisições assíncronas concorrentes simultaneamente...\n");

            var tasks = new List<Task>();

            for (int i = 1; i <= 5; i++)
            {
                int requestId = i;
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await bulkhead.ExecuteAsync(async ct =>
                        {
                            Console.WriteLine($"   [Request #{requestId}] Entrou no Bulkhead! Executando por 200ms...");
                            await Task.Delay(200);
                            Console.WriteLine($"   [Request #{requestId}] Concluída com sucesso.");
                        });
                    }
                    catch (BulkheadRejectedException ex)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkMagenta;
                        Console.WriteLine($"   [Request #{requestId}] FALHA POR REJEIÇÃO: {ex.Message} (Motivo: {ex.Reason})");
                        Console.ResetColor();
                    }
                }));
            }

            await Task.WhenAll(tasks);
            Console.WriteLine();
        }
    }
}
