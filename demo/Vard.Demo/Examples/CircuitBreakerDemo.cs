using System;
using System.Net.Http;
using System.Threading.Tasks;
using Vard.Abstractions;
using Vard.Builders;

namespace Vard.Demo.Examples
{
    public static class CircuitBreakerDemo
    {
        public static async Task RunAsync()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("================================================================================");
            Console.WriteLine(" 2. CIRCUIT BREAKER DEMO (Count-Based + Single Pilot Probe em HalfOpen)");
            Console.WriteLine("================================================================================");
            Console.ResetColor();

            // Abre após 2 falhas consecutivas e permanece aberto por 400ms antes do probe teste
            var circuitBreaker = VardPolicy
                .Handle<HttpRequestException>()
                .CircuitBreaker(
                    exceptionsAllowedBeforeBreaking: 2,
                    durationOfBreak: TimeSpan.FromMilliseconds(400),
                    onBreak: (ex, duration, ctx) =>
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"   [OnBreak] Circuito ABRIU por {duration.TotalMilliseconds:F0}ms devido à falha: {ex?.Message}");
                        Console.ResetColor();
                    },
                    onReset: ctx =>
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("   [OnReset] Circuito FECHOU! Sistema restabelecido com sucesso.");
                        Console.ResetColor();
                    },
                    onHalfOpen: ctx =>
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("   [OnHalfOpen] Circuito em HALF-OPEN! Permitindo um único teste piloto (trial probe)...");
                        Console.ResetColor();
                    });

            Console.WriteLine("-> Passo 1: Provocando 2 falhas consecutivas para abrir o circuito...");
            for (int i = 1; i <= 2; i++)
            {
                try
                {
                    circuitBreaker.Execute(() =>
                    {
                        Console.WriteLine($"   --> Requisição #{i}: simulando erro de conexão...");
                        throw new HttpRequestException("Falha no banco de dados.");
                    });
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"   --> Exceção capturada: {ex.Message} (Estado atual: {circuitBreaker.CircuitState})");
                }
            }

            Console.WriteLine("\n-> Passo 2: Tentando requisição com circuito ABERTO (fail-fast imediato sem onerar o recurso)...");
            try
            {
                circuitBreaker.Execute(() =>
                {
                    Console.WriteLine("   --> ESTA LINHA NÃO DEVE EXECUTAR!");
                    return true;
                });
            }
            catch (CircuitBreakerOpenException ex)
            {
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine($"   [FAST-FAIL] Rejeitado por CircuitBreakerOpenException! Estado: {ex.State}, RetryAfter: {ex.RetryAfter?.TotalMilliseconds:F0}ms");
                Console.ResetColor();
            }

            Console.WriteLine("\n-> Passo 3: Aguardando 450ms para transição de estado para Half-Open...");
            await Task.Delay(450);

            Console.WriteLine("-> Passo 4: Executando requisição piloto com sucesso para restabelecer o circuito...");
            var probeResult = circuitBreaker.Execute(() =>
            {
                Console.WriteLine("   --> [Pilot Probe] Executando chamada de teste...");
                return "Sucesso na recuperação!";
            });

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[RECUPERADO] Resultado: {probeResult} | Estado final: {circuitBreaker.CircuitState}\n");
            Console.ResetColor();
        }
    }
}
