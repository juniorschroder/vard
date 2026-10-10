using Vard.Demo.Examples;

namespace Vard.Demo
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(@"
 __      __                 .___
 \ \    / /____ _______   __| _/
  \ \  / /\__  \\_  __ \ / __ | 
   \ \/ /  / __ \|  | \// /_/ | 
    \__/  (____  /__|   \____ | 
               \/            \/ 
      Toolkit de Resiliência para .NET
================================================");
            Console.ResetColor();

            try
            {
                // 1. Retry com Jitter AWS
                await RetryDemo.RunAsync();

                // 2. Circuit Breaker com Single Pilot Probe
                await CircuitBreakerDemo.RunAsync();

                // 3. Timeout cooperativo & Fallback gracioso
                await TimeoutAndFallbackDemo.RunAsync();

                // 4. Bulkhead para isolamento de concorrência
                await BulkheadDemo.RunAsync();

                // 5. Rate Limiter (Token Bucket)
                await RateLimiterDemo.RunAsync();

                // 6. Hedging para chamadas de baixa latência
                await HedgingDemo.RunAsync();

                // 7. Pipeline Corporativo em Camadas (Wrap)
                await CorporatePipelineDemo.RunAsync();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("================================================================================");
                Console.WriteLine(" TODOS OS EXEMPLOS DO VARD FORAM EXECUTADOS COM SUCESSO!");
                Console.WriteLine("================================================================================");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Erro inesperado na execução da demo: {ex}");
                Console.ResetColor();
            }
        }
    }
}
