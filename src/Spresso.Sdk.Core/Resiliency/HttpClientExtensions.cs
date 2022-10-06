using System.Net.Http;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System;

namespace Spresso.Sdk.Core.Resiliency
{
    public static class HttpClientExtensions
    {
        /// <summary>
        ///     Execute a http POST request and handle json response or various error conditions
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="httpClient"></param>
        /// <param name="requestUri"></param>
        /// <param name="requestJson"></param>
        /// <param name="onSuccessFunc"></param>
        /// <param name="onAuthErrorFailure"></param>
        /// <param name="onBadRequestFailure"></param>
        /// <param name="onTimeoutFailure"></param>
        /// <param name="onUnknownFailure"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task<T> ExecutePostApiRequestAsync<T>(this HttpClient httpClient, string requestUri, string requestJson, 
            Func<string, HttpStatusCode, Task<T>> onSuccessFunc,
            Func<HttpStatusCode, T> onAuthErrorFailure, 
            Func<T> onBadRequestFailure,
            Func<Exception?, T> onTimeoutFailure,
            Func<Exception?, HttpStatusCode?, T> onUnknownFailure,
            CancellationToken cancellationToken)
        {
            try
            {
                var apiResponse = await httpClient.PostAsync(requestUri, new StringContent(requestJson, Encoding.UTF8, "application/json"),
                    cancellationToken);

                if (apiResponse.IsSuccessStatusCode)
                {
                    var json = await apiResponse.Content.ReadAsStringAsync();
                    return await onSuccessFunc(json, apiResponse.StatusCode);
                }
                switch (apiResponse.StatusCode)
                {
                    case HttpStatusCode.Unauthorized:
                    case HttpStatusCode.Forbidden:
                        return onAuthErrorFailure(apiResponse.StatusCode);
                    case HttpStatusCode.BadRequest:
                        return onBadRequestFailure();
                    default:
                        return onUnknownFailure(null, apiResponse.StatusCode);
                }
            }
            catch (HttpRequestException e) when (e.Message.Contains("No connection could be made because the target machine actively refused it."))
            {
                return onTimeoutFailure(e);
            }
            catch (OperationCanceledException e)
            {
                return onTimeoutFailure(e);
            }
            catch (Exception e)
            {
                return onUnknownFailure(e, null);
            }
        }

        /// <summary>
        ///     Execute a http GET request and handle json response or various error conditions
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="httpClient"></param>
        /// <param name="requestUri"></param>
        /// <param name="onSuccessFunc"></param>
        /// <param name="onAuthErrorFailure"></param>
        /// <param name="onBadRequestFailure"></param>
        /// <param name="onTimeoutFailure"></param>
        /// <param name="onUnknownFailure"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task<T> ExecuteGetApiRequestAsync<T>(this HttpClient httpClient, string requestUri,
            Func<string, HttpStatusCode, Task<T>> onSuccessFunc,
            Func<HttpStatusCode, T> onAuthErrorFailure,
            Func<T> onBadRequestFailure,
            Func<Exception?, T> onTimeoutFailure,
            Func<Exception?, HttpStatusCode?, T> onUnknownFailure,
            CancellationToken cancellationToken)
        {
            try
            {
                var apiResponse = await httpClient.GetAsync(requestUri, cancellationToken);

                if (apiResponse.IsSuccessStatusCode)
                {
                    var json = await apiResponse.Content.ReadAsStringAsync();
                    return await onSuccessFunc(json, apiResponse.StatusCode);
                }
                switch (apiResponse.StatusCode)
                {
                    case HttpStatusCode.Unauthorized:
                    case HttpStatusCode.Forbidden:
                        return onAuthErrorFailure(apiResponse.StatusCode);
                    case HttpStatusCode.BadRequest:
                        return onBadRequestFailure();
                    default:
                        return onUnknownFailure(null, apiResponse.StatusCode);
                }
            }
            catch (HttpRequestException e) when (e.Message.Contains("No connection could be made because the target machine actively refused it."))
            {
                return onTimeoutFailure(e);
            }
            catch (OperationCanceledException e)
            {
                return onTimeoutFailure(e);
            }
            catch (Exception e)
            {
                return onUnknownFailure(e, null);
            }
        }
    }
}