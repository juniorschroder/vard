# Changelog

Todas as mudanças notáveis deste projeto serão documentadas neste arquivo.

O formato é baseado em [Keep a Changelog](https://keepachangelog.com/pt-BR/1.0.0/),
e este projeto adere ao [Semantic Versioning](https://semver.org/lang/pt-BR/).

## [1.0.0] - 2026-10-05

### Added
- **Abstrações Core:** Interfaces `IPolicy`, `ISyncPolicy`, `IAsyncPolicy`, `IPolicyBuilder`, tipos de resultado `PolicyResult<T>`, dicionário estruturado `Context`, enum `ExceptionType` e sentinela `Void`.
- **Política de Retry:** Tentativas com backoff fixo, linear e exponencial com algoritmo Full Jitter (AWS), suporte a filtros por tipo de exceção (`Handle<T>`) e por predicado de resultado (`HandleResult<T>`), com callbacks `OnRetry`.
- **Política de Circuit Breaker:** Count-based (`CircuitBreakerPolicy`) e rate-based (`AdvancedCircuitBreakerPolicy`), máquina atômica de 4 estados (`Closed`, `Open`, `HalfOpen`, `Isolated`), *Single Pilot Request* em `HalfOpen`, anel circular `BucketedSlidingWindow` O(1) zero-alloc, controles manuais `Isolate()` e `Reset()`, exceção `CircuitBreakerOpenException` e callbacks `OnBreak`, `OnReset`, `OnHalfOpen`.
- **Política de Timeout:** Cancelamento cooperativo com `CancellationTokenSource` vinculado, unwrap de exceções síncronas e exceção `TimeoutRejectedException` com callback `OnTimeout`.
- **Política de Fallback:** Fallback síncrono e assíncrono para valores substitutos, delegates alternativos com contexto de erro, suporte para métodos void e callback `OnFallback`.
- **Política de Bulkhead:** Isolamento de concorrência com duplo semáforo (`SemaphoreSlim`), limite de execuções paralelas e slots de fila configuráveis, fail-fast instantâneo em fila zero, exceção `BulkheadRejectedException` e callback `OnBulkheadRejected`.
- **Política de Rate Limiter:** Algoritmos *Token Bucket* (recarga contínua lazy matemática) e *Sliding Window* (anel circular particionado), exceção `RateLimiterRejectedException` e callback `OnRateLimitExceeded`.
- **Política de Hedging:** Execuções paralelas especulativas após delay ou falha antecipada, retorno da resposta mais rápida, cancelamento instantâneo de perdedores via linked CTS e callback `OnHedgingResult`.
- **Composição de Pipelines:** Motor `PolicyWrap` e métodos fluentes `.Wrap()`, permitindo composição encadeada outside-in (ex: `Fallback -> Retry -> CircuitBreaker -> Timeout -> Bulkhead`) com propagação e enriquecimento contínuo de `Context`.
- **Qualidade & Distribuição:** 226 testes unitários e de integração (100% de aprovação), zero dependências externas em runtime (pure .NET Standard 2.1 BCL), documentação XML integral sem warnings e metadados NuGet completos.
