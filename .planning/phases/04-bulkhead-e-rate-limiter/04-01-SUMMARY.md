---
phase: 4
plan: "04-01"
subsystem: policies
status: complete
tags: [bulkhead, concurrency-isolation, dual-semaphore, rejection-exception, cancellation, fail-fast, observability, callbacks]
requires: [Phase 1, Phase 2, Phase 3]
provides: [BulkheadPolicy, IBulkheadPolicy, BulkheadRejectionReason, BulkheadRejectedException, BulkheadPolicyExtensions]
affects: [Phase 4 (04-02 Rate Limiter), Phase 5]
tech-stack:
  added: [netstandard2.1, net8.0, xUnit 2.7.0, FluentAssertions 6.12.0]
  patterns: [Dual-Semaphore Coordination, Fail-Fast Zero-Queue, Cooperative Cancellation with Clean Queue Release, Typed Rejection Reason, Dual Sync/Async Observability]
key-files:
  created:
    - src/Vard/Abstractions/BulkheadRejectionReason.cs
    - src/Vard/Abstractions/BulkheadRejectedException.cs
    - src/Vard/Abstractions/IBulkheadPolicy.cs
    - src/Vard/Policies/BulkheadPolicy.cs
    - src/Vard/Builders/BulkheadPolicyExtensions.cs
    - tests/Vard.Tests/BulkheadPolicyTests.cs
  modified:
    - src/Vard/Builders/VardPolicy.cs
decisions:
  - "D-01: Concorrência e fila coordenadas via dois SemaphoreSlim (_executionSemaphore e _queueSemaphore), suportando cancelamento cooperativo e esperas síncronas e assíncronas nativas sem dependências externas."
  - "D-02: Fail-fast imediato quando maxQueuedActions = 0: saturação de execução rejeita instantaneamente lançando BulkheadRejectedException com Reason = ExecutionSaturated sem fila."
  - "D-03: Interface especializada IBulkheadPolicy expõe MaxParallelization, MaxQueuedActions, BulkheadAvailableCount e QueueAvailableCount para inspeção em tempo real."
  - "D-04: Suporte a cancelamento e IDisposable: cancelamento durante espera na fila via CancellationToken devolve a vaga de fila no finally e não dispara callback de rejeição; IDisposable descarta semáforos."
  - "D-13: Observabilidade com suporte dual síncrono (Action<IDictionary<string, object>?>) e assíncrono (Func<IDictionary<string, object>?, Task>) com fail-fast."
  - "D-14: Em ExecuteAndCapture / ExecuteAndCaptureAsync, rejeições de Bulkhead são registradas com IsSuccess = false e ExceptionType = ExceptionType.PolicyBypassed."
metrics:
  tasks_completed: 4
  tasks_total: 4
  tests_passed: 159
  tests_failed: 0
  completed_date: "2026-10-02"
---

# Phase 04 Plan 01: Bulkhead Policy Summary

Implementação completa da política de isolamento de concorrência Bulkhead (`BulkheadPolicy`) com controle de paralelismo e fila coordenados via `SemaphoreSlim`, fail-fast para filas zeradas, cancelamento cooperativo sem vazamento de recursos, exceção tipada `BulkheadRejectedException`, observabilidade dual sync/async, extensões fluentes no builder e 20 novos testes automatizados elevando a suíte para 159 testes com 0 falhas e 0 warnings.

## Key Changes

1. **Abstrações e Contratos (Task 04-01-01):**
   - `BulkheadRejectionReason`: enum especificando `ExecutionSaturated = 1` (quando todos os slots de execução estão tomados e a fila é zero) e `QueueFull = 2` (quando a fila de espera está saturada).
   - `BulkheadRejectedException`: exceção customizada contendo `MaxParallelization`, `MaxQueuedActions`, `Reason` e mensagem formatada detalhada.
   - `IBulkheadPolicy`: interface especializada herdando de `IPolicy`, `ISyncPolicy` e `IAsyncPolicy`, expondo `MaxParallelization`, `MaxQueuedActions`, `BulkheadAvailableCount` e `QueueAvailableCount`.

2. **Implementação de `BulkheadPolicy` (Task 04-01-02):**
   - Coordenação de concorrência com `_executionSemaphore` (`maxParallelization`) e `_queueSemaphore` (`maxQueuedActions`).
   - Fail-fast imediato quando `maxQueuedActions == 0`: não aloca semáforo de fila e rejeita com `ExecutionSaturated` instantaneamente ao saturar.
   - Quando `maxQueuedActions > 0`, aloca vaga de fila via `_queueSemaphore.Wait(0)`. Se a fila estiver esgotada, rejeita imediatamente com `QueueFull`. Ao obter o slot de execução, desaloca a vaga de fila no bloco `finally`.
   - Suporte robusto a `CancellationToken`: se o cancelamento ocorrer durante a espera na fila, a vaga de fila é desalocada no `finally` e `OnBulkheadRejected` **não** é disparado.
   - Suporte completo a `IDisposable` com descarte seguro dos semáforos subjacentes e verificação de `ThrowIfDisposed()`.
   - `ExecuteAndCapture` e `ExecuteAndCaptureAsync` capturam `BulkheadRejectedException` retornando `PolicyResult<T>` com `IsSuccess = false` e `ExceptionType = ExceptionType.PolicyBypassed`.

3. **Fluent Builder & Pontos de Entrada Estáticos (Task 04-01-03):**
   - `BulkheadPolicyExtensions`: método de extensão `builder.Bulkhead(maxParallelization, maxQueuedActions, onBulkheadRejected, onBulkheadRejectedAsync)` em `IPolicyBuilder`.
   - `VardPolicy.Bulkhead(...)`: ponto de entrada estático com as mesmas sobrecargas para criação direta e concisa.

4. **Suíte Abrangente de Testes Unitários (Task 04-01-04):**
   - `BulkheadPolicyTests`: 20 novos testes cobrindo:
     - Validação de argumentos no construtor.
     - Execução dentro do limite de paralelismo (síncrona e assíncrona, com e sem contexto).
     - Fail-fast com fila zero (`ExecutionSaturated`).
     - Rejeição quando a fila está saturada (`QueueFull`).
     - Liberação sequencial de requisições enfileiradas conforme slots são liberados.
     - Cancelamento via `CancellationToken` durante espera na fila liberando a vaga sem disparar callbacks.
     - Liberação garantida de slots de execução quando a ação do usuário lança exceção.
     - Disparo dos callbacks de observabilidade síncrono e assíncrono repassando o contexto.
     - Propagação fail-fast quando o callback de observabilidade lança erro.
     - Integração com `ExecuteAndCapture` e `ExecuteAndCaptureAsync` registrando `PolicyBypassed`.
     - Descarte limpo com `Dispose()` impedindo execuções subsequentes com `ObjectDisposedException`.
     - Construção via Fluent Builder e `VardPolicy`.
     - Suporte a actions void e async void.

## Verification Results

- `dotnet build src/Vard/Vard.csproj`: 0 Warning(s), 0 Error(s).
- `dotnet build tests/Vard.Tests/Vard.Tests.csproj`: 0 Warning(s), 0 Error(s).
- `dotnet test tests/Vard.Tests/Vard.Tests.csproj`: 159 passed, 0 failed, 0 skipped.
- Total test suite progression: 22 (Phase 1) -> 93 (Phase 2) -> 139 (Phase 3) -> 159 (Phase 4 Plan 01).
