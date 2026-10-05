# Roadmap: Vard

## Overview

6 fases para entregar a biblioteca Vard completa — do scaffold inicial até o pacote NuGet publicado.
Cada fase entrega funcionalidade testável e commitável de forma independente.
Fase 1 estabelece as abstrações core; fases 2–5 implementam as políticas com testes;
fase 6 finaliza empacotamento e documentação.

## Phases

### Phase 1: Scaffold e Abstrações Core

**Goal:** Criar a estrutura da solução e todas as abstrações/contratos fundamentais da biblioteca — interfaces, tipos base, PolicyResult, Context e o sistema de condições HandleException/HandleResult.
**Status:** Complete
**Delivers:**

- Solução `.sln` com projetos `Vard` (netstandard2.1) e `Vard.Tests` (net8.0)
- Interfaces: `IPolicy`, `IAsyncPolicy`, `ISyncPolicy`, `IPolicyBuilder`
- Tipos: `PolicyResult<T>`, `Context`, `ExceptionType` enum
- Sistema de condições: `HandleCondition`, `HandleException<T>()`, `HandleResult<T>(predicate)`, `Or()` combinator
- Builder fluente: `VardPolicy.Handle<T>()` entry point
- `PolicyWrap` — wrap explícito de políticas
- Testes unitários para abstrações core (API-07, API-08, API-09, API-10, API-11)

**Covers:** API-01, API-02, API-03, API-04, API-05, API-06, API-07, API-08, API-09, API-10, API-11, TEST-01

---

### Phase 2: Retry, Timeout e Fallback

**Goal:** Implementar as três políticas mais fundamentais de resiliência — Retry com backoff configurável, Timeout com cancelamento e Fallback com valor/ação alternativa — com callbacks de observabilidade e testes completos.
**Status:** Complete
**Delivers:**

- `RetryPolicy` — fixed, linear e exponential backoff com jitter, HandleException/HandleResult
- `TimeoutPolicy` — cancelamento via CancellationToken, composição com token externo
- `FallbackPolicy<T>` — valor alternativo, delegate alternativo
- Exceções customizadas: `TimeoutRejectedException`
- Callbacks: `OnRetry`, `OnTimeout`, `OnFallback`
- Testes unitários e de callbacks para todas as três políticas

**Covers:** RET-01, RET-02, RET-03, RET-04, RET-05, TO-01, TO-02, TO-03, FB-01, FB-02, FB-03, OBS-01, OBS-03, OBS-04, TEST-02, TEST-03

---

### Phase 3: Circuit Breaker

**Goal:** Implementar as duas variantes do Circuit Breaker — count-based e rate-based — com máquina de estados Closed/Open/Half-Open/Isolated, Single Pilot Request em Half-Open, callbacks e testes incluindo thread-safety.
**Status:** Complete
**Delivers:**

- `CircuitBreakerPolicy` — abre após N falhas consecutivas
- `AdvancedCircuitBreakerPolicy` — abre quando % falhas excede threshold em janela deslizante (BucketedSlidingWindow)
- Estados: `Closed`, `Open`, `HalfOpen`, `Isolated` com transições atômicas
- Exceção: `CircuitBreakerOpenException` com State, RetryAfter e InnerException
- Controles manuais: `Isolate()` e `Reset()`
- Callbacks: `OnBreak`, `OnReset`, `OnHalfOpen` (síncronos e assíncronos)
- Testes de estados, transições, single pilot probe, thread-safety sob alta concorrência e callbacks

**Covers:** CB-01, CB-02, CB-03, CB-04, CB-05, CB-06, OBS-02, TEST-02, TEST-03, TEST-05

---

### Phase 4: Bulkhead e Rate Limiter

**Goal:** Implementar isolamento de concorrência via Bulkhead e controle de taxa via Rate Limiter (token bucket + sliding window), com exceções de rejeição e callbacks.
**Status:** Complete
**Delivers:**

- `BulkheadPolicy` — limite de execuções paralelas, fila de espera configurável
- `TokenBucketRateLimiterPolicy` — burst + recarga em taxa configurável
- `SlidingWindowRateLimiterPolicy` — N requisições por janela deslizante
- Exceções: `BulkheadRejectedException`, `RateLimiterRejectedException`
- Callbacks: `OnBulkheadRejected`, `OnRateLimitExceeded`
- Testes de concorrência, limites e rejeições

**Covers:** BH-01, BH-02, BH-03, BH-04, RL-01, RL-02, RL-03, RL-04, OBS-05, OBS-06, TEST-02, TEST-03, TEST-05

---

### Phase 5: Hedging e Testes de Integração

**Goal:** Implementar a política de Hedging (execuções paralelas com retorno do mais rápido) e criar testes de integração de pipeline cobrindo composição de múltiplas políticas.
**Status:** Pending
**Delivers:**

- `HedgingPolicy<T>` — disparo paralelo após delay, retorno do resultado mais rápido, cancelamento dos pendentes
- Callback: `OnHedgingResult`
- Testes unitários do Hedging
- Testes de integração: pipelines compostos (ex: Retry + CircuitBreaker + Timeout + Fallback)
- Testes de wrap explícito entre políticas
- Testes de Context propagado por pipeline completo

**Covers:** HE-01, HE-02, HE-03, HE-04, HE-05, OBS-07, TEST-04, TEST-05, TEST-06

---

### Phase 6: NuGet, Documentação e Publicação

**Goal:** Preparar a biblioteca para publicação profissional no nuget.org — metadados NuGet completos, documentação XML, README com exemplos de uso, CHANGELOG e instruções de publicação.
**Status:** Pending
**Delivers:**

- `Vard.csproj` com metadados NuGet completos (PackageId, Version, Authors, Description, Tags, RepositoryUrl, PackageLicenseExpression, PackageReadmeFile, PackageIcon)
- `README.md` com getting started e exemplos de todas as políticas
- Documentação XML (/// summary) em todos os tipos e membros públicos
- `CHANGELOG.md` com entry v1.0.0
- `LICENSE` (MIT)
- Verificação `dotnet pack` gerando `.nupkg` válido
- Instruções de publicação via `dotnet nuget push`
- `.gitignore` e `.editorconfig` finais

**Covers:** PKG-01, PKG-02, PKG-03, PKG-04, PKG-05, PKG-06, PKG-07
