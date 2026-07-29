```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
Intel Xeon Processor 2.30GHz, 1 CPU, 4 logical and 4 physical cores
.NET SDK 10.0.103
  [Host]     : .NET 8.0.24 (8.0.24, 8.0.2426.7010), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 8.0.24 (8.0.24, 8.0.2426.7010), X64 RyuJIT x86-64-v3


```
| Method                | Mean     | Error    | StdDev   | Ratio | RatioSD |
|---------------------- |---------:|---------:|---------:|------:|--------:|
| ReadSync_ThreadBlock  | 15.46 μs | 0.217 μs | 0.181 μs |  1.00 |    0.02 |
| ReadAsync_NonBlocking | 34.53 μs | 1.167 μs | 3.348 μs |  2.23 |    0.22 |
