using System;
using System.Threading;
using System.Threading.Tasks;
using Polly;
using Polly.Timeout;

namespace Spresso.Sdk.Core.Resiliency
{
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
        ///     Creates a policy that times out after a set duration.  This override will bubble up exceptions
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="timeoutOptions"></param>
        /// <returns></returns>
        public static IAsyncPolicy<T> BuildPolicy<T>(TimeoutOptions timeoutOptions)
        {
            var timeoutPolicy = Policy.TimeoutAsync<T>(timeoutOptions.Timeout, TimeoutStrategy.Pessimistic);

            return Policy.WrapAsync(timeoutPolicy);
        }

        /// <summary>
        ///     Creates a policy that times out after a set duration.  This override will will execute a fallback policy upon error
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="timeoutOptions"></param>
        /// <param name="fallbackOptions"></param>
        /// <returns></returns>
        public static IAsyncPolicy<T> BuildPolicy<T>(TimeoutOptions timeoutOptions, FallbackOptions<T> fallbackOptions)
        {
            var timeoutPolicy = Policy.TimeoutAsync<T>(timeoutOptions.Timeout, TimeoutStrategy.Pessimistic);

            var fallbackPolicy = Policy.Handle<Exception>().OrResult(fallbackOptions.FallbackPredicate).FallbackAsync(fallbackOptions.FallbackAction,
                fallbackOptions.OnFallback);

            return Policy.WrapAsync(timeoutPolicy, fallbackPolicy);
        }
    }
}