---
name: migrate-polly-to-vard
description: Automated discovery, replacement, and verification pipeline for migrating .NET projects from Polly to Vard resilience library.
---

# Migrate Polly to Vard

Guide AI agents (Claude, Gemini, Codex) to scan a .NET codebase, identify Polly references, install `Vard`, rewrite policies to `Vard`, remove Polly dependencies, and execute existing test suites.

## Step 1: Codebase Audit & Scan
1. Scan `.csproj` / `packages.config` for Polly dependencies:
   - `Polly`, `Polly.Core`, `Polly.Extensions.Http`, `Microsoft.Extensions.Http.Polly`.
2. Locate all C# files containing:
   - `using Polly;`, `using Polly.Retry;`, `using Polly.CircuitBreaker;`, `using Polly.Timeout;`, etc.
   - Usages of `Policy.Handle<T>()`, `Policy.Wrap()`, `Policy.Timeout()`, `Policy.HandleResult()`.

## Step 2: Package Swap
1. Install `Vard` via .NET CLI:
   ```bash
   dotnet add package Vard
   ```
2. Remove Polly packages from project files:
   ```bash
   dotnet remove package Polly
   dotnet remove package Polly.Core
   dotnet remove package Polly.Extensions.Http
   dotnet remove package Microsoft.Extensions.Http.Polly
   ```

## Step 3: API & Code Migration Mapping
Replace `using Polly...;` with `using Vard.Builders;` and `using Vard.Abstractions;`.

| Polly API | Vard Equivalent |
|---|---|
| `Policy.Handle<T>()` | `VardPolicy.Handle<T>()` |
| `Policy.HandleResult<T>(p)` | `VardPolicy.HandleResult<T>(p)` |
| `builder.Or<T>()` | `builder.Or<T>()` |
| `.RetryAsync(n)` | `.RetryWithBackoff(n, TimeSpan.Zero)` or `.Retry(n)` |
| `.WaitAndRetryAsync(n, i => delay)` | `.RetryWithBackoff(n, initialDelay, backoffType, useJitter)` |
| `.CircuitBreakerAsync(n, duration)` | `.CircuitBreaker(exceptionsAllowedBeforeBreaking: n, durationOfBreak: duration)` |
| `.AdvancedCircuitBreakerAsync(...)` | `.AdvancedCircuitBreaker(failureThreshold, samplingDuration, minimumThroughput, durationOfBreak)` |
| `.TimeoutAsync(ts)` | `VardPolicy.Timeout(ts)` |
| `.FallbackAsync(val)` | `VardPolicy.Handle<T>().Fallback(val)` or `builder.Fallback(val)` |
| `.BulkheadAsync(max, queue)` | `VardPolicy.Bulkhead(maxParallelization: max, maxQueuedActions: queue)` |
| `Policy.WrapAsync(p1, p2)` | `p1.Wrap(p2)` or `VardPolicy.Wrap(p1, p2)` |
| `policy.ExecuteAsync(fn)` | `policy.ExecuteAsync(fn)` |
| `policy.ExecuteAndCaptureAsync(fn)` | `policy.ExecuteAndCaptureAsync(fn)` |

## Step 4: Verification & Test Execution
1. Compile solution:
   ```bash
   dotnet build
   ```
2. Run test suites if available:
   ```bash
   dotnet test
   ```
3. Fix any compilation or type mismatches until all tests pass.
