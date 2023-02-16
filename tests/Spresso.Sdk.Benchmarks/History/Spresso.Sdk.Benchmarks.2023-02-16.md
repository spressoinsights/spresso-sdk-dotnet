``` ini

BenchmarkDotNet=v0.13.2, OS=Windows 11 (10.0.22621.1105)
AMD Ryzen 9 7950X, 1 CPU, 32 logical and 16 physical cores
.NET SDK=7.0.102
  [Host]     : .NET 7.0.2 (7.0.222.60605), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.2 (7.0.222.60605), X64 RyuJIT AVX2


```
|             Method |     Mean |     Error |    StdDev |   Gen0 |   Gen1 | Allocated |
|------------------- |---------:|----------:|----------:|-------:|-------:|----------:|
|      GetTokenAsync | 5.866 μs | 0.1752 μs | 0.5055 μs | 0.5417 | 0.1373 |   8.93 KB |
|      OptimizePrice | 3.187 μs | 0.0542 μs | 0.0507 μs | 0.4692 | 0.0038 |   7.67 KB |
| OptimizePriceBatch | 9.222 μs | 0.1697 μs | 0.1587 μs | 1.5259 | 0.0153 |  24.95 KB |
