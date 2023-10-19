using Microsoft.AspNetCore.Mvc.Testing;

namespace SpressoAI.Sdk.Core.Tests ;

    public class TestHttpClientFactory<T> : IHttpClientFactory where T:class
    {
        private readonly WebApplicationFactory<T> _testServer;
        
        public TestHttpClientFactory(WebApplicationFactory<T> testServer)
        {
            _testServer = testServer;
        }

        public HttpClient CreateClient(string name)
        {
            return _testServer.CreateClient();
        }

    }