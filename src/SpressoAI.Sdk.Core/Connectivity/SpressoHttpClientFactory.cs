using System.Net.Http;
using HttpClientFactoryLite;
using IHttpClientFactory = HttpClientFactoryLite.IHttpClientFactory;

namespace SpressoAI.Sdk.Core.Connectivity
{
    /// <summary>
    ///     Manages http client creation for Spresso.  You can provide an HttpClientFactory, or one can be created and managed
    ///     for you.
    /// </summary>
    public class SpressoHttpClientFactory
    {
        public static readonly SpressoHttpClientFactory Default = new SpressoHttpClientFactory();
        private readonly IHttpClientFactory? _managedHttpClientFactory;
        private readonly System.Net.Http.IHttpClientFactory? _providedHttpClientFactory;

        public SpressoHttpClientFactory(System.Net.Http.IHttpClientFactory? providedHttpClientFactory)
        {
            _providedHttpClientFactory = providedHttpClientFactory;
            if (_providedHttpClientFactory == null)
            {
                _managedHttpClientFactory = new HttpClientFactory();
            }
        }

        public SpressoHttpClientFactory()
        {
            _managedHttpClientFactory = new HttpClientFactory();
        }

        public HttpClient GetClient()
        {
            if (_providedHttpClientFactory != null)
            {
                return _providedHttpClientFactory.CreateClient("spresso");
            }
            return _managedHttpClientFactory!.CreateClient("spresso");
        }
    }
}