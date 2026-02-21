using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Microsoft.Extensions.Logging;
using System;

namespace Infrastructure.Resilience;

public static class EmailPolicies
{
    public static IAsyncPolicy GetResiliencePolicy(ILogger logger)
    {
        // 1. Timeout Policy: 15 seconds per attempt (was 30s)
        var timeoutPolicy = Policy
            .TimeoutAsync(TimeSpan.FromSeconds(15));

        // 2. Retry Policy: Single retry with 2s delay (was 3 retries × exponential)
        // Reduced because EmailQueueService already retries 3x with its own backoff.
        // Combined: 1 Polly retry × 3 queue retries = 6 attempts max (was 4×3=12)
        var retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(1, 
                retryAttempt => TimeSpan.FromSeconds(2),
                (exception, timeSpan, retryCount, context) =>
                {
                    logger.LogWarning(exception, $"[RESILIENCE_RETRY] Attempt {retryCount} failed. Retrying in {timeSpan.TotalSeconds}s.");
                });

        // 3. Circuit Breaker Policy: Open after 3 consecutive failures for 30 seconds
        var circuitBreakerPolicy = Policy
            .Handle<Exception>()
            .CircuitBreakerAsync(
                exceptionsAllowedBeforeBreaking: 3,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (exception, timespan) =>
                {
                    logger.LogCritical(exception, $"[CIRCUIT_BREAKER_OPEN] Circuit opened for {timespan.TotalSeconds}s due to consecutive failures.");
                },
                onReset: () =>
                {
                    logger.LogInformation("[CIRCUIT_BREAKER_RESET] Circuit reset. Normal operation resumed.");
                },
                onHalfOpen: () =>
                {
                    logger.LogInformation("[CIRCUIT_BREAKER_HALF_OPEN] Circuit is half-open. Testing next request.");
                });

        // Wrap policies: Timeout -> Retry -> CircuitBreaker
        // Order matters: Timeout is innermost/outermost depending on goal. 
        // Here: Timeout per attempt, then retry if it fails, then circuit breaker tracks the retries.
        return Policy.WrapAsync(circuitBreakerPolicy, retryPolicy, timeoutPolicy);
    }
}
