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
        // 1. Timeout Policy: 10 seconds per attempt
        var timeoutPolicy = Policy
            .TimeoutAsync(TimeSpan.FromSeconds(10));

        // 2. Retry Policy: Exponential backoff (2s, 4s, 8s)
        var retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(3, 
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
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
