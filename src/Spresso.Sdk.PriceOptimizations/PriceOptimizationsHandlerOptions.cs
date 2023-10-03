using System;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Spresso.Sdk.Core.Connectivity;
using Spresso.Sdk.Core.Shared;

namespace Spresso.Sdk.PriceOptimizations
{
    public class PriceOptimizationsHandlerOptions
    {
        private TimeSpan _timeout = new TimeSpan(0, 0, 0, seconds: 10);

        /// <summary>
        ///     Http Client Factory to create http clients
        /// </summary>
        public SpressoHttpClientFactory SpressoHttpClientFactory { get; set; } = SpressoHttpClientFactory.Default;

        /// <summary>
        ///     This is the base URL for all Spresso API calls.  Defaults to https://api.spresso.com
        /// </summary>
        public string SpressoBaseUrl { get; set; } = Defaults.DefaultSpressoBaseUrl;

        /// <summary>
        ///     Logger for debug events
        /// </summary>
        public ILogger<IPriceOptimizationHandler> Logger { get; set; } = new NullLogger<IPriceOptimizationHandler>();

        /// <summary>
        ///     Http timeout.  Default is 1 seconds.
        /// </summary>
        public TimeSpan HttpTimeout { get; set; } = new TimeSpan(0, 0, 0, 1);

        /// <summary>
        ///     Price optimization cache expiration.  Default is 1 hour.
        /// </summary>
        public TimeSpan CacheDuration { get; set; } = new TimeSpan(0, 1, 0, 0, 0);

        /// <summary>
        ///     The time to wait before failing a request, including retries. Default is 10 seconds.  Max is 180 seconds.
        /// </summary>
        public TimeSpan Timeout
        {
            get => _timeout;
            set
            {
                if (value.TotalSeconds < 180)
                {
                    _timeout = value;
                }
            }
        }

        /// <summary>
        ///     Additional parameters to be sent to the token endpoint for debug/testing purposes
        /// </summary>
        public string AdditionalParameters { get; set; } = string.Empty;

        /// <summary>
        ///    Throw an exception upon failure.  Default is false.
        /// </summary>
        public bool ThrowOnFailure { get; set; } = false;
    }
}