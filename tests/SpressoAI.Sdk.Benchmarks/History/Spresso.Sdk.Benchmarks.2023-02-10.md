``` ini

BenchmarkDotNet=v0.13.2, OS=Windows 11 (10.0.22621.1105)
AMD Ryzen 9 7950X, 1 CPU, 32 logical and 16 physical cores
.NET SDK=7.0.102
  [Host]     : .NET 7.0.2 (7.0.222.60605), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.2 (7.0.222.60605), X64 RyuJIT AVX2


```
|             Method |       Mean |     Error |    StdDev |   Gen0 |   Gen1 | Allocated |
|------------------- |-----------:|----------:|----------:|-------:|-------:|----------:|
|      GetTokenAsync | 128.872 μs | 1.6064 μs | 1.5026 μs | 3.4180 | 0.9766 |  58.35 KB |
|      OptimizePrice |   3.074 μs | 0.0586 μs | 0.0651 μs | 0.4234 |      - |   6.97 KB |
| OptimizePriceBatch |   8.622 μs | 0.1694 μs | 0.1585 μs | 1.3123 | 0.0153 |  21.44 KB |
