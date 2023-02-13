``` ini

BenchmarkDotNet=v0.13.2, OS=Windows 11 (10.0.22621.1105)
AMD Ryzen 9 7950X, 1 CPU, 32 logical and 16 physical cores
.NET SDK=7.0.102
  [Host]     : .NET 7.0.2 (7.0.222.60605), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.2 (7.0.222.60605), X64 RyuJIT AVX2


```
|             Method |     Mean |     Error |    StdDev |   Median |   Gen0 |   Gen1 | Allocated |
|------------------- |---------:|----------:|----------:|---------:|-------:|-------:|----------:|
|      GetTokenAsync | 5.309 μs | 0.1846 μs | 0.5442 μs | 5.154 μs | 0.5417 | 0.1373 |   8.93 KB |
|      OptimizePrice | 3.125 μs | 0.0613 μs | 0.0730 μs | 3.160 μs | 0.4234 |      - |   6.95 KB |
| OptimizePriceBatch | 8.821 μs | 0.1500 μs | 0.2003 μs | 8.855 μs | 1.2970 | 0.0153 |  21.32 KB |
