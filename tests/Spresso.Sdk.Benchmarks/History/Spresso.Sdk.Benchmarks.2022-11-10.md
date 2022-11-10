``` ini

BenchmarkDotNet=v0.13.2, OS=Windows 10 (10.0.19044.2251/21H2/November2021Update)
Intel Core i7-5960X CPU 3.00GHz (Broadwell), 1 CPU, 16 logical and 8 physical cores
.NET SDK=7.0.100
  [Host]     : .NET 7.0.0 (7.0.22.51805), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.0 (7.0.22.51805), X64 RyuJIT AVX2


```
|             Method |       Mean |     Error |    StdDev |   Gen0 |   Gen1 | Allocated |
|------------------- |-----------:|----------:|----------:|-------:|-------:|----------:|
|      GetTokenAsync | 200.265 μs | 1.4819 μs | 1.3861 μs | 5.3711 | 1.4648 |  58.27 KB |
|      OptimizePrice |   5.460 μs | 0.0360 μs | 0.0319 μs | 0.6790 |      - |   6.97 KB |
| OptimizePriceBatch |  15.758 μs | 0.2513 μs | 0.2351 μs | 2.0752 |      - |  21.44 KB |
