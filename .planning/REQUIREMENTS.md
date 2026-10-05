# Requirements: Vard

**Defined:** 2026-10-01
**Core Value:** Fornecer para desenvolvedores .NET um toolkit de resiliência sem atrito, type-safe, zero-deps e fácil de compor — publicado como pacote NuGet profissional e testado de forma abrangente.

## v1 Requirements

### Políticas de Resiliência — Retry

- [x] **RET-01**: Política de retry executa ação com N tentativas máximas configuráveis
- [x] **RET-02**: Suporte a backoff fixo (fixed delay entre tentativas)
- [x] **RET-03**: Suporte a backoff exponencial (delay cresce exponencialmente)
- [x] **RET-04**: Suporte a jitter aleatório no delay para evitar thundering herd
- [x] **RET-05**: Retry dispara apenas para exceptions/resultados configurados via HandleException/HandleResult

### Políticas de Resiliência — Circuit Breaker

- [x] **CB-01**: Count-based Circuit Breaker — abre após N falhas consecutivas configuráveis
- [x] **CB-02**: Rate-based Circuit Breaker — abre quando % de falhas ultrapassa threshold em janela de tempo
- [x] **CB-03**: Circuit Breaker tem estados Closed, Open e Half-Open com transições corretas
- [x] **CB-04**: Período de Half-Open configurável (tempo antes de tentar novamente)
- [x] **CB-05**: Callbacks: OnBreak, OnReset, OnHalfOpen
- [x] **CB-06**: Lança `CircuitBreakerOpenException` quando circuito está aberto

### Políticas de Resiliência — Timeout

- [x] **TO-01**: Política de timeout cancela execução via CancellationToken após duração configurável
- [x] **TO-02**: Lança `TimeoutRejectedException` quando timeout expira
- [x] **TO-03**: Respeita CancellationToken externo passado pelo caller (composição)

### Políticas de Resiliência — Fallback

- [x] **FB-01**: Fallback retorna valor alternativo configurável quando política de trigger dispara
- [x] **FB-02**: Fallback executa ação alternativa (delegate) configurável
- [x] **FB-03**: Callback OnFallback chamado com contexto da falha original

### Políticas de Resiliência — Bulkhead

- [x] **BH-01**: Bulkhead limita número máximo de execuções paralelas simultâneas
- [x] **BH-02**: Fila de espera configurável para requisições além do limite
- [x] **BH-03**: Lança `BulkheadRejectedException` quando sem slot disponível e fila cheia
- [x] **BH-04**: Callback OnBulkheadRejected

### Políticas de Resiliência — Rate Limiter

- [x] **RL-01**: Rate Limiter Token Bucket — permite burst até N tokens, recarregando a taxa configurável
- [x] **RL-02**: Rate Limiter Sliding Window — permite N requisições em janela de tempo deslizante
- [x] **RL-03**: Lança `RateLimiterRejectedException` quando limite excedido
- [x] **RL-04**: Callback OnRateLimitExceeded

### Políticas de Resiliência — Hedging

- [ ] **HE-01**: Hedging dispara execução paralela após delay configurável se a primeira não completou
- [ ] **HE-02**: Retorna resultado da execução mais rápida bem-sucedida
- [ ] **HE-03**: Cancela execuções pendentes quando resultado obtido
- [ ] **HE-04**: Número máximo de hedges paralelos configurável
- [ ] **HE-05**: Callback OnHedgingResult

### API e Composição

- [x] **API-01**: Pipeline fluente via builder — `VardPolicy.Handle<Exception>().Retry(3).AndTimeout(30)` etc.
- [x] **API-02**: Wrap explícito — cada política pode envolver outra: `retryPolicy.Wrap(circuitBreakerPolicy)`
- [x] **API-03**: `Execute(Func<T>)` — execução síncrona que lança exceção ao esgotar
- [x] **API-04**: `ExecuteAsync(Func<CancellationToken, Task<T>>)` — execução async que lança
- [x] **API-05**: `ExecuteAndCapture(Func<T>)` — execução síncrona que retorna `PolicyResult<T>`
- [x] **API-06**: `ExecuteAndCaptureAsync(Func<CancellationToken, Task<T>>)` — execução async que retorna `PolicyResult<T>`
- [x] **API-07**: `PolicyResult<T>` com propriedades: IsSuccess, Result, Exception, FinalException, ExceptionType
- [x] **API-08**: `HandleException<TException>()` — trigger para tipo específico de exception
- [x] **API-09**: `HandleException<TException>(predicate)` — trigger com condição adicional
- [x] **API-10**: `HandleResult<T>(predicate)` — trigger baseado em resultado (ex: StatusCode == 500)
- [x] **API-11**: Condições `HandleException` e `HandleResult` combináveis via `Or()`

### Observabilidade

- [x] **OBS-01**: Callback `OnRetry(attempt, timeSpan, exception, context)` no Retry
- [x] **OBS-02**: Callbacks `OnBreak`, `OnReset`, `OnHalfOpen` no Circuit Breaker
- [x] **OBS-03**: Callback `OnTimeout(context, timeSpan, task)` no Timeout
- [x] **OBS-04**: Callback `OnFallback(exception, context)` no Fallback
- [x] **OBS-05**: Callback `OnBulkheadRejected(context)` no Bulkhead
- [ ] **OBS-06**: Callback `OnHedgingResult(result, attempt)` no Hedging
- [ ] **OBS-07**: `Context` passado para todos os callbacks (chave-valor extensível por caller)

### Testes

- [x] **TEST-01**: Testes unitários com xUnit + FluentAssertions para todas as políticas
- [x] **TEST-02**: Testes de casos de sucesso e falha para cada política
- [x] **TEST-03**: Testes de callbacks/delegates (verificar que são chamados)
- [ ] **TEST-04**: Testes de composição de pipeline (múltiplas políticas combinadas)
- [x] **TEST-05**: Testes de thread-safety para Circuit Breaker e Bulkhead
- [x] **TEST-06**: Testes de timeout com controle de tempo via `ISystemClock` / `TimeProvider`

### Distribuição e Qualidade

- [ ] **PKG-01**: Projeto Vard.csproj configurado como .NET Standard 2.1 com metadados NuGet completos
- [ ] **PKG-02**: README.md com getting started, exemplos de uso de todas as políticas
- [ ] **PKG-03**: Documentação XML (/// summary) em todos os tipos e membros públicos
- [ ] **PKG-04**: `.nupkg` gerado via `dotnet pack` com versão semântica
- [ ] **PKG-05**: Instruções para publicação via `dotnet nuget push` em nuget.org
- [ ] **PKG-06**: Zero dependências externas (apenas .NET Standard 2.1 BCL)
- [ ] **PKG-07**: `CHANGELOG.md` com v1.0.0 entry

## v2 Requirements

### Integrações

- **INT-01**: Extensão para Microsoft.Extensions.DependencyInjection (AddVard())
- **INT-02**: Integração com Microsoft.Extensions.Logging
- **INT-03**: Exportadores de telemetria para OpenTelemetry e Serilog
- **INT-04**: Integração com `System.Threading.RateLimiting` do .NET 7+

### Features Avançadas

- **ADV-01**: Política `Cache` — memorizar resultado para evitar chamadas redundantes
- **ADV-02**: `PolicyRegistry` — registry nomeado de políticas reutilizáveis
- **ADV-03**: Retry com `IAsyncEnumerable` streaming
- **ADV-04**: Suporte a .NET 7+ `RateLimiter` abstraction

## Out of Scope

| Feature | Reason |
|---|---|
| Microsoft.Extensions.DependencyInjection | Evitar deps externas na v1 |
| OpenTelemetry / Serilog out-of-the-box | Delegates são suficientes e zero-deps para v1 |
| Suporte a .NET Framework < netstandard2.1 | Complexidade adicional sem valor imediato |
| UI/dashboard de monitoramento | Fora do escopo de biblioteca |
| .NET 7+ System.Threading.RateLimiting | Conflita com target netstandard2.1 para v1 |

## Traceability

| Requirement | Phase | Status |
|---|---|---|
| RET-01 a RET-05 | Phase 2 | Complete |
| CB-01 a CB-06 | Phase 3 | Complete |
| TO-01 a TO-03 | Phase 2 | Complete |
| FB-01 a FB-03 | Phase 2 | Complete |
| BH-01 a BH-04 | Phase 4 | Complete |
| RL-01 a RL-04 | Phase 4 | Complete |
| HE-01 a HE-05 | Phase 5 | Pending |
| API-01 a API-11 | Phase 1 | Complete |
| OBS-01 a OBS-07 | Phase 2–5 (cada fase) | In Progress |
| TEST-01 a TEST-06 | Phase 1–5 (cada fase) | In Progress |
| PKG-01 a PKG-07 | Phase 6 | Pending |

**Coverage:**

- v1 requirements: 52 total
- Mapped to phases: 52
- Unmapped: 0 ✓

---
*Requirements defined: 2026-10-01*
*Last updated: 2026-10-01 após definição inicial*
