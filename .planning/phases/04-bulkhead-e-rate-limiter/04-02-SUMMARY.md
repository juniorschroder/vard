---
phase: 4
plan: "04-02"
subsystem: policies
status: complete
tags: [rate-limiter, token-bucket, sliding-window, bucketed-sliding-window, retry-after, wait-timeout, variable-cost, rejection-exception, cancellation, fail-fast, observability, callbacks]
requires: [Phase 1, Phase 2, Phase 3, 04-01]
provides: [TokenBucketRateLimiterPolicy, SlidingWindowRateLimiterPolicy, IRateLimiterPolicy, RateLimiterRejectedException, RateLimiterContextKeys, RateLimiterPolicyExtensions]
affects: [Phase 4 (04-03 Concurrency Tests), Phase 5]
tech-stack:
  added: [netstandard2.1, net8.0, xUnit 2.7.0, FluentAssertions 6.12.0]
  patterns: [Lazy Mathematical Replenishment, Circular Bucketed Sliding Window, Dynamic RetryAfter Calculation, Wait Timeout with Cooperative Cancellation, Variable Permit Cost via Context, Dual Sync/Async Observability]
key-files:
  created:
    - src/Vard/Abstractions/IRateLimiterPolicy.cs
    - src/Vard/Abstractions/RateLimiterRejectedException.cs
    - src/Vard/Abstractions/RateLimiterContextKeys.cs
    - src/Vard/Policies/TokenBucketRateLimiterPolicy.cs
    - src/Vard/Policies/SlidingWindowRateLimiterPolicy.cs
    - src/Vard/Builders/RateLimiterPolicyExtensions.cs
    - tests/Vard.Tests/TokenBucketRateLimiterPolicyTests.cs
    - tests/Vard.Tests/SlidingWindowRateLimiterPolicyTests.cs
  modified:
    - src/Vard/Common/BucketedSlidingWindow.cs
    - src/Vard/Builders/VardPolicy.cs
decisions:
  - "D-05: Duas classes concretas de Rate Limiter (TokenBucketRateLimiterPolicy e SlidingWindowRateLimiterPolicy) implementando IPolicy, ISyncPolicy, IAsyncPolicy e IRateLimiterPolicy."
  - "D-06: Reutilização de BucketedSlidingWindow para SlidingWindowRateLimiterPolicy, com anel circular de buckets, O(1) de CPU, contadores atômicos e zero alocações GC no heap."
  - "D-07: Recarga contínua lazy matemática sob demanda no TokenBucketRateLimiterPolicy baseada em ticks monotônicos (Stopwatch.GetTimestamp), sem timers de background e zero threads ociosas."
  - "D-08: Fluent API no IPolicyBuilder (.TokenBucketRateLimiter, .SlidingWindowRateLimiter) e entry points estáticos em VardPolicy (TokenBucket, SlidingWindow)."
  - "D-09: Rejeição imediata por padrão (fail-fast), com suporte opcional a tempo de espera configurável (maxWaitTime) antes de rejeitar."
  - "D-10: Cálculo dinâmico e exato de RetryAfter: proporcional aos tokens faltantes no Token Bucket e tempo até a expiração do bucket mais antigo no Sliding Window."
  - "D-11: Consumo padrão de 1 permit por execução com suporte a consumo de peso variável via chave de contexto Vard.RateLimit.Cost."
  - "D-12: Cancelamento cooperativo limpo durante espera: OperationCanceledException propagada sem consumir cotas e sem disparar callbacks OnRateLimitExceeded."
  - "D-13: Observabilidade com suporte dual síncrono (Action<TimeSpan, IDictionary<string, object>?>) e assíncrono (Func<TimeSpan, IDictionary<string, object>?, Task>) com fail-fast."
  - "D-14: Em ExecuteAndCapture / ExecuteAndCaptureAsync, rejeições de Rate Limiter são registradas com IsSuccess = false e ExceptionType = ExceptionType.PolicyBypassed."
metrics:
  tasks_completed: 5
  tasks_total: 5
  tests_passed: 189
  tests_failed: 0
  completed_date: "2026-10-02"
---

# Phase 04 Plan 02: Rate Limiter Policies & Abstractions Summary

Implementação completa das políticas de controle de vazão de requisições: `TokenBucketRateLimiterPolicy` (com burst configurável e recarga contínua lazy matemática, sem timers de background) e `SlidingWindowRateLimiterPolicy` (com anel circular de buckets reutilizando `BucketedSlidingWindow`, O(1) e zero alocações heap), ambas equipadas com a exceção especializada `RateLimiterRejectedException`, cálculo dinâmico de `RetryAfter`, suporte opcional a timeout de espera (`maxWaitTime`), consumo de permits de custo variável via `RateLimiterContextKeys.Cost`, observabilidade dual sync/async, extensões fluentes no builder e 30 novos testes unitários automatizados elevando a suíte para 189 testes com 0 falhas e 0 warnings.

## Key Changes

1. **Abstrações e Aprimoramento de Utilitários Comuns (Task 04-02-01):**
   - `IRateLimiterPolicy`: interface pública especializada herdando de `IPolicy`, `ISyncPolicy`, `IAsyncPolicy`, expondo `PermitLimit`, `AvailablePermits` e `AlgorithmName`.
   - `RateLimiterRejectedException`: exceção especializada contendo `RetryAfter`, `PermitLimit`, `AlgorithmName` e mensagem informativa.
   - `RateLimiterContextKeys`: classe de constantes contendo `Cost = "Vard.RateLimit.Cost"` para consumo de cotas ponderadas.
   - `BucketedSlidingWindow`: métodos atômicos `TryConsume(permits, permitLimit, out retryAfter, out currentCount)` e `GetCurrentCount()` operando sob `_syncLock` com cálculo de tempo restante até a expiração do bucket mais antigo ativo na janela circular.

2. **Implementação de `TokenBucketRateLimiterPolicy` (Task 04-02-02):**
   - Algoritmo Token Bucket com capacidade máxima (`maxTokens`), taxa de regeneração (`tokensPerSecond`) e espera opcional (`maxWaitTime`).
   - Recarga contínua sob demanda (lazy replenishment) calculada a partir de ticks monotônicos (`Stopwatch.GetTimestamp()`) na chegada de requisições, sem loops de polling ou `System.Threading.Timer`.
   - Cálculo exato de `RetryAfter` proporcional às frações de tokens em falta.
   - Suporte a consumo de custo variável via `RateLimiterContextKeys.Cost`.
   - Suporte a espera configurável (`maxWaitTime`) com cancelamento cooperativo: cancelamento durante a espera aborta sem gastar tokens e sem disparar callbacks.
   - Integração com `ExecuteAndCapture` e `ExecuteAndCaptureAsync` registrando falhas com `ExceptionType.PolicyBypassed`.

3. **Implementação de `SlidingWindowRateLimiterPolicy` (Task 04-02-03):**
   - Algoritmo de janela deslizante particionada com amostragem circular O(1) e zero alocações GC no heap após instanciação.
   - Cálculo dinâmico de `RetryAfter` com base no tempo para o bucket mais antigo sair da janela.
   - Suporte a fail-fast imediato e espera opcional com `maxWaitTime`.
   - Suporte a consumo de cota variável e observabilidade dual sync/async.
   - Captura com `PolicyResult<T>` classificando rejeição como `PolicyBypassed`.

4. **Fluent Builder & Pontos de Entrada Estáticos (Task 04-02-04):**
   - `RateLimiterPolicyExtensions`: métodos de extensão `.TokenBucketRateLimiter(...)` e `.SlidingWindowRateLimiter(...)` em `IPolicyBuilder`.
   - `VardPolicy.TokenBucket(...)` e `VardPolicy.SlidingWindow(...)`: pontos de entrada estáticos para instanciação direta.

5. **Suíte Abrangente de Testes Unitários (Task 04-02-05):**
   - `TokenBucketRateLimiterPolicyTests`: 14 testes cobrindo validação de parâmetros, execução dentro do limite de burst, rejeição com `RetryAfter` dinâmico, recarga com tempo simulado determinístico, espera com `maxWaitTime`, fail-fast quando `retryAfter > maxWaitTime`, cancelamento limpo sem efeitos colaterais, consumo de cotas variáveis, callbacks de observabilidade e captura via `PolicyResult`.
   - `SlidingWindowRateLimiterPolicyTests`: 14 testes cobrindo validações, execuções dentro da cota, rejeições dinâmicas, expiração de janela com tempo simulado, espera configurável com `maxWaitTime`, cancelamento durante espera, cotas variáveis com `Cost`, callbacks síncrono/assíncrono e captura `PolicyBypassed`.

## Verification Results

- `dotnet build src/Vard/Vard.csproj`: 0 Warning(s), 0 Error(s).
- `dotnet build tests/Vard.Tests/Vard.Tests.csproj`: 0 Warning(s), 0 Error(s).
- `dotnet test tests/Vard.Tests/Vard.Tests.csproj`: 189 passed, 0 failed, 0 skipped.
- Total test suite progression: 22 (Phase 1) -> 93 (Phase 2) -> 139 (Phase 3) -> 159 (Phase 4 Plan 01) -> 189 (Phase 4 Plan 02).
