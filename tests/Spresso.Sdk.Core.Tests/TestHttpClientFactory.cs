
using Microsoft.AspNetCore.Mvc.Testing;

namespace Spresso.Sdk.Core.Tests ;

    public class TestHttpClientFactory : IHttpClientFactory
    {
        private readonly WebApplicationFactory<Program> _testServer;


        public TestHttpClientFactory(WebApplicationFactory<Program> testServer)
        {
            _testServer = testServer;
        }

        public HttpClient CreateClient(string name)
        {
            return _testServer.CreateClient();
        }

    }