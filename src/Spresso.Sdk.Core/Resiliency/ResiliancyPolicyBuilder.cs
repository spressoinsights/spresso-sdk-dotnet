using System;
using System.Threading;
using System.Threading.Tasks;
using Polly;
using Polly.Timeout;

namespace Spresso.Sdk.Core.Resiliency
{
    public class RetryOptions<T>
    {
        public Func<T, bool> RetryPredicate { get; }
        public int NumberOfRetries { get; }

        public RetryOptions(Func<T, bool> retryPredicate, int numberOfRetries)
        {
            RetryPredicate = retryPredicate;
            NumberOfRetries = numberOfRetries;
        }
    }

    public class CircuitBreakerOptions<T>
    {
        public Func<T, bool> BreakPredicate { get; }
        public int NumberOfFailuresBeforeTrippingCircuitBreaker { get; }
        public TimeSpan CircuitBreakerBreakDuration { get; }
        public Action<DelegateResult<T>, TimeSpan, Context> OnBreakAction { get; }
        public Action<Context> OnResetAction { get; set; }

        public CircuitBreakerOptions(Func<T, bool> breakPredicate, int numberOfFailuresBeforeTrippingCircuitBreaker, TimeSpan circuitBreakerBreakDuration,
            Action<DelegateResult<T>, TimeSpan, Context>? onBreakAction = null, Action<Context>? onResetAction = null)
        {
            BreakPredicate = breakPredicate;
            NumberOfFailuresBeforeTrippingCircuitBreaker = numberOfFailuresBeforeTrippingCircuitBreaker;
            CircuitBreakerBreakDuration = circuitBreakerBreakDuration;
            OnBreakAction = onBreakAction ?? new Action<DelegateResult<T>, TimeSpan, Context>((a, b, c) => { });
            OnResetAction = onResetAction ?? new Action<Context>(ctx => { });
        }
    }

    public class FallbackOptions<T>
    {
        public Func<T, bool> FallbackPredicate { get; }
        public Func<DelegateResult<T>, Context, CancellationToken, Task<T>> FallbackAction { get; }
        public Func<DelegateResult<T>, Context, Task> OnFallback { get; }

        public FallbackOptions(Func<T, bool> fallbackPredicate, Func<DelegateResult<T>, Context, CancellationToken, Task<T>> fallbackAction,
            Func<DelegateResult<T>, Context, Task> onFallback)
        {
            FallbackPredicate = fallbackPredicate;
            FallbackAction = fallbackAction;
            OnFallback = onFallback;
        }
    }

    public class TimeoutOptions
    {
        public TimeSpan Timeout { get; }

        public TimeoutOptions(TimeSpan timeout)
        {
            Timeout = timeout;
        }
    }

    public static class ResiliencyPolicyBuilder
    {
        /// <summary>
        ///     Creates a policy that retries a configurable number of times, times out after a set duration, and trips a circuit breaker after a set number of failures.  This override will bubble up exceptions
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="retryOptions"></param>
        /// <param name="timeoutOptions"></param>
        /// <param name="circuitBreakerOptions"></param>
        /// <returns></returns>
        public static IAsyncPolicy<T> BuildPolicy<T>(RetryOptions<T> retryOptions, TimeoutOptions timeoutOptions,
            CircuitBreakerOptions<T> circuitBreakerOptions)
        {
            var timeoutPolicy = Policy.TimeoutAsync<T>(timeoutOptions.Timeout, TimeoutStrategy.Pessimistic);
            var retryPolicy = Policy.HandleResult(retryOptions.RetryPredicate).RetryAsync(retryOptions.NumberOfRetries);

            var circuitBreakerPolicy = Policy.HandleResult(circuitBreakerOptions.BreakPredicate)
                .Or<TimeoutRejectedException>()
                .CircuitBreakerAsync(circuitBreakerOptions.NumberOfFailuresBeforeTrippingCircuitBreaker, circuitBreakerOptions.CircuitBreakerBreakDuration,
                    circuitBreakerOptions.OnBreakAction, circuitBreakerOptions.OnResetAction);


            return Policy.WrapAsync(circuitBreakerPolicy, timeoutPolicy, retryPolicy);
        }

        /// <summary>
        ///     Creates a policy that retries a configurable number of times, times out after a set duration, and trips a circuit breaker after a set number of failures.  This override will will execute a fallback policy upon error
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="retryOptions"></param>
        /// <param name="timeoutOptions"></param>
        /// <param name="circuitBreakerOptions"></param>
        /// <param name="fallbackOptions"></param>
        /// <returns></returns>
        public static IAsyncPolicy<T> BuildPolicy<T>(RetryOptions<T> retryOptions, TimeoutOptions timeoutOptions,
            CircuitBreakerOptions<T> circuitBreakerOptions, FallbackOptions<T> fallbackOptions)
        {
            var fallbackPolicy = Policy.Handle<Exception>().OrResult(fallbackOptions.FallbackPredicate).FallbackAsync(fallbackOptions.FallbackAction,
                fallbackOptions.OnFallback);

            return Policy.WrapAsync(fallbackPolicy, BuildPolicy(retryOptions, timeoutOptions, circuitBreakerOptions));
        }
    }
}