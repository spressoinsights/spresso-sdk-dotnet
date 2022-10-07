using Microsoft.Extensions.Logging;

namespace Spresso.Sdk.Core.Tests ;

    public class MemoryLogger<T> : ILogger<T>
    {

        public List<string> Logs { get; } = new();
        public IDisposable BeginScope<TState>(TState state)
        {
            throw new NotImplementedException();
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Logs.Add(formatter(state, exception));
        }
    }