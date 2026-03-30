# Benchmark Results

Generated: 2026-03-29 17:51:55  
Job: dry | N: 1000  
Runtime: .NET 10.0.4  

## Benchmarks.JsonNsjBenchmarks

| Method        | Categories      | N    | Mean        | Ratio | Allocated  | Alloc Ratio |
|-------------- |---------------- |----- |------------:|------:|-----------:|------------:|
| additive      | NSJ Deserialize | 1000 | 13,156.1 μs |  1.00 | 2134.84 KB |        1.00 |
| boxedFields   | NSJ Deserialize | 1000 | 13,238.4 μs |  1.01 | 2359.77 KB |        1.11 |
| boxedTuple    | NSJ Deserialize | 1000 | 13,002.1 μs |  0.99 | 1870.25 KB |        0.88 |
| overlap       | NSJ Deserialize | 1000 | 13,394.7 μs |  1.02 | 2134.81 KB |        1.00 |
| unsafeOverlap | NSJ Deserialize | 1000 | 14,098.7 μs |  1.07 | 2135.09 KB |        1.00 |
| record        | NSJ Deserialize | 1000 | 12,702.5 μs |  0.97 | 1823.99 KB |        0.85 |
| class         | NSJ Deserialize | 1000 |          NA |     ? |         NA |           ? |
|               |                 |      |             |       |            |             |
| additive      | NSJ RoundTrip   | 1000 | 14,182.7 μs |  1.00 | 2538.67 KB |        1.00 |
| boxedFields   | NSJ RoundTrip   | 1000 | 14,176.1 μs |  1.00 | 2810.65 KB |        1.11 |
| boxedTuple    | NSJ RoundTrip   | 1000 | 14,453.3 μs |  1.02 |  2203.8 KB |        0.87 |
| overlap       | NSJ RoundTrip   | 1000 | 14,033.8 μs |  0.99 | 2538.47 KB |        1.00 |
| unsafeOverlap | NSJ RoundTrip   | 1000 | 14,055.5 μs |  0.99 | 2539.04 KB |        1.00 |
| record        | NSJ RoundTrip   | 1000 | 13,744.7 μs |  0.97 | 2126.34 KB |        0.84 |
| class         | NSJ RoundTrip   | 1000 |          NA |     ? |         NA |           ? |
|               |                 |      |             |       |            |             |
| additive      | NSJ Serialize   | 1000 |  1,092.9 μs |  1.00 |  403.83 KB |        1.00 |
| boxedFields   | NSJ Serialize   | 1000 |  1,105.6 μs |  1.01 |  450.88 KB |        1.12 |
| boxedTuple    | NSJ Serialize   | 1000 |  1,075.7 μs |  0.98 |  333.55 KB |        0.83 |
| overlap       | NSJ Serialize   | 1000 |  1,099.4 μs |  1.01 |  403.66 KB |        1.00 |
| unsafeOverlap | NSJ Serialize   | 1000 |  1,106.8 μs |  1.01 |  403.95 KB |        1.00 |
| record        | NSJ Serialize   | 1000 |  1,116.2 μs |  1.02 |  302.34 KB |        0.75 |
| class         | NSJ Serialize   | 1000 |    826.3 μs |  0.76 |  237.48 KB |        0.59 |

## Benchmarks.JsonStjBenchmarks

| Method        | Categories      | N    | Mean       | Ratio | Allocated | Alloc Ratio |
|-------------- |---------------- |----- |-----------:|------:|----------:|------------:|
| additive      | STJ Deserialize | 1000 | 6,553.8 μs |  1.00 | 389.38 KB |        1.00 |
| boxedFields   | STJ Deserialize | 1000 | 6,827.5 μs |  1.04 | 567.25 KB |        1.46 |
| boxedTuple    | STJ Deserialize | 1000 | 6,396.0 μs |  0.98 |    195 KB |        0.50 |
| overlap       | STJ Deserialize | 1000 | 6,363.8 μs |  0.97 | 389.51 KB |        1.00 |
| unsafeOverlap | STJ Deserialize | 1000 | 6,569.8 μs |  1.00 | 389.45 KB |        1.00 |
| record        | STJ Deserialize | 1000 | 5,317.4 μs |  0.81 | 179.97 KB |        0.46 |
| class         | STJ Deserialize | 1000 |         NA |     ? |        NA |           ? |
|               |                 |      |            |       |           |             |
| additive      | STJ RoundTrip   | 1000 | 6,881.0 μs |  1.00 | 496.45 KB |        1.00 |
| boxedFields   | STJ RoundTrip   | 1000 | 6,916.6 μs |  1.01 |  674.4 KB |        1.36 |
| boxedTuple    | STJ RoundTrip   | 1000 | 6,740.9 μs |  0.98 | 302.12 KB |        0.61 |
| overlap       | STJ RoundTrip   | 1000 | 6,819.1 μs |  0.99 | 496.59 KB |        1.00 |
| unsafeOverlap | STJ RoundTrip   | 1000 | 6,871.7 μs |  1.00 | 496.49 KB |        1.00 |
| record        | STJ RoundTrip   | 1000 | 5,744.9 μs |  0.83 | 287.06 KB |        0.58 |
| class         | STJ RoundTrip   | 1000 |         NA |     ? |        NA |           ? |
|               |                 |      |            |       |           |             |
| additive      | STJ Serialize   | 1000 |   594.6 μs |  1.00 | 107.07 KB |        1.00 |
| boxedFields   | STJ Serialize   | 1000 |   602.1 μs |  1.01 | 107.15 KB |        1.00 |
| boxedTuple    | STJ Serialize   | 1000 |   596.3 μs |  1.00 | 107.12 KB |        1.00 |
| overlap       | STJ Serialize   | 1000 |   592.5 μs |  1.00 | 107.08 KB |        1.00 |
| unsafeOverlap | STJ Serialize   | 1000 |   587.2 μs |  0.99 | 107.05 KB |        1.00 |
| record        | STJ Serialize   | 1000 |   595.8 μs |  1.00 | 107.09 KB |        1.00 |
| class         | STJ Serialize   | 1000 |   204.7 μs |  0.34 |   6.19 KB |        0.06 |

## Types.BigJsonNsjBenchmarks

| Method        | Categories      | N    | Mean      | Ratio | Allocated  | Alloc Ratio |
|-------------- |---------------- |----- |----------:|------:|-----------:|------------:|
| Additive      | NSJ Deserialize | 1000 | 13.294 ms |  1.00 | 2809.02 KB |        1.00 |
| BoxedFields   | NSJ Deserialize | 1000 | 13.912 ms |  1.05 | 3057.11 KB |        1.09 |
| BoxedTuple    | NSJ Deserialize | 1000 | 13.515 ms |  1.02 | 2717.99 KB |        0.97 |
| Overlap       | NSJ Deserialize | 1000 | 13.259 ms |  1.00 | 2809.02 KB |        1.00 |
| UnsafeOverlap | NSJ Deserialize | 1000 | 13.435 ms |  1.01 |  2800.1 KB |        1.00 |
| Record        | NSJ Deserialize | 1000 | 13.086 ms |  0.98 | 2670.77 KB |        0.95 |
|               |                 |      |           |       |            |             |
| Additive      | NSJ Serialize   | 1000 |  1.427 ms |  1.00 |  558.33 KB |        1.00 |
| BoxedFields   | NSJ Serialize   | 1000 |  1.429 ms |  1.00 |  597.39 KB |        1.07 |
| BoxedTuple    | NSJ Serialize   | 1000 |  1.415 ms |  0.99 |  527.08 KB |        0.94 |
| Overlap       | NSJ Serialize   | 1000 |  1.414 ms |  0.99 |  558.33 KB |        1.00 |
| UnsafeOverlap | NSJ Serialize   | 1000 |  1.419 ms |  0.99 |  558.33 KB |        1.00 |
| Record        | NSJ Serialize   | 1000 |  1.416 ms |  0.99 |  495.83 KB |        0.89 |

## Types.BigJsonStjBenchmarks

| Method        | Categories      | N    | Mean       | Ratio | Allocated | Alloc Ratio |
|-------------- |---------------- |----- |-----------:|------:|----------:|------------:|
| Additive      | STJ Deserialize | 1000 | 5,694.5 μs |  1.00 | 232.63 KB |        1.00 |
| BoxedFields   | STJ Deserialize | 1000 | 5,687.7 μs |  1.00 | 441.66 KB |        1.90 |
| BoxedTuple    | STJ Deserialize | 1000 | 5,709.2 μs |  1.00 | 172.85 KB |        0.74 |
| Overlap       | STJ Deserialize | 1000 | 5,585.0 μs |  0.98 | 232.63 KB |        1.00 |
| UnsafeOverlap | STJ Deserialize | 1000 | 5,707.9 μs |  1.00 | 223.71 KB |        0.96 |
| Record        | STJ Deserialize | 1000 | 4,593.2 μs |  0.81 | 156.88 KB |        0.67 |
|               |                 |      |            |       |           |             |
| Additive      | STJ Serialize   | 1000 |   735.3 μs |  1.00 | 128.13 KB |        1.00 |
| BoxedFields   | STJ Serialize   | 1000 |   745.5 μs |  1.01 | 128.13 KB |        1.00 |
| BoxedTuple    | STJ Serialize   | 1000 |   987.7 μs |  1.34 | 128.13 KB |        1.00 |
| Overlap       | STJ Serialize   | 1000 |   713.0 μs |  0.97 | 128.13 KB |        1.00 |
| UnsafeOverlap | STJ Serialize   | 1000 |   736.2 μs |  1.00 | 128.13 KB |        1.00 |
| Record        | STJ Serialize   | 1000 |   725.1 μs |  0.99 | 128.13 KB |        1.00 |

## Types.ManagedJsonNsjBenchmarks

| Method        | Categories      | N    | Mean        | Ratio | Allocated  | Alloc Ratio |
|-------------- |---------------- |----- |------------:|------:|-----------:|------------:|
| Additive      | NSJ Deserialize | 1000 | 12,985.0 μs |  1.00 | 1940.01 KB |        1.00 |
| BoxedFields   | NSJ Deserialize | 1000 | 12,168.3 μs |  0.94 | 1989.18 KB |        1.03 |
| BoxedTuple    | NSJ Deserialize | 1000 | 12,927.0 μs |  1.00 | 1906.12 KB |        0.98 |
| Overlap       | NSJ Deserialize | 1000 | 12,717.0 μs |  0.98 | 1940.01 KB |        1.00 |
| UnsafeOverlap | NSJ Deserialize | 1000 | 12,306.0 μs |  0.95 | 1940.01 KB |        1.00 |
| Record        | NSJ Deserialize | 1000 | 11,849.3 μs |  0.91 | 1856.95 KB |        0.96 |
|               |                 |      |             |       |            |             |
| Additive      | NSJ Serialize   | 1000 |    996.0 μs |  1.00 |  241.64 KB |        1.00 |
| BoxedFields   | NSJ Serialize   | 1000 |    995.9 μs |  1.00 |  249.45 KB |        1.03 |
| BoxedTuple    | NSJ Serialize   | 1000 |    995.8 μs |  1.00 |  226.02 KB |        0.94 |
| Overlap       | NSJ Serialize   | 1000 |    984.9 μs |  0.99 |  241.64 KB |        1.00 |
| UnsafeOverlap | NSJ Serialize   | 1000 |  1,000.5 μs |  1.00 |  241.64 KB |        1.00 |
| Record        | NSJ Serialize   | 1000 |    986.7 μs |  0.99 |  194.77 KB |        0.81 |

## Types.ManagedJsonStjBenchmarks

| Method        | Categories      | N    | Mean       | Ratio | Allocated | Alloc Ratio |
|-------------- |---------------- |----- |-----------:|------:|----------:|------------:|
| Additive      | STJ Deserialize | 1000 | 4,985.1 μs |  1.00 | 243.83 KB |        1.00 |
| BoxedFields   | STJ Deserialize | 1000 | 5,026.0 μs |  1.01 | 285.19 KB |        1.17 |
| BoxedTuple    | STJ Deserialize | 1000 | 4,963.7 μs |  1.00 | 225.56 KB |        0.93 |
| Overlap       | STJ Deserialize | 1000 | 4,969.9 μs |  1.00 | 243.83 KB |        1.00 |
| UnsafeOverlap | STJ Deserialize | 1000 | 5,008.0 μs |  1.00 | 243.83 KB |        1.00 |
| Record        | STJ Deserialize | 1000 | 4,036.7 μs |  0.81 | 207.64 KB |        0.85 |
|               |                 |      |            |       |           |             |
| Additive      | STJ Serialize   | 1000 |   460.1 μs |  1.00 |  81.17 KB |        1.00 |
| BoxedFields   | STJ Serialize   | 1000 |   465.4 μs |  1.01 |  81.17 KB |        1.00 |
| BoxedTuple    | STJ Serialize   | 1000 |   470.7 μs |  1.02 |  81.17 KB |        1.00 |
| Overlap       | STJ Serialize   | 1000 |   480.6 μs |  1.04 |  81.17 KB |        1.00 |
| UnsafeOverlap | STJ Serialize   | 1000 |   464.9 μs |  1.01 |  81.17 KB |        1.00 |
| Record        | STJ Serialize   | 1000 |   446.1 μs |  0.97 |  81.17 KB |        1.00 |

## Types.ManyJsonNsjBenchmarks

| Method        | Categories      | N    | Mean      | Ratio | Allocated  | Alloc Ratio |
|-------------- |---------------- |----- |----------:|------:|-----------:|------------:|
| Additive      | NSJ Deserialize | 1000 | 13.009 ms |  1.00 | 2316.01 KB |        1.00 |
| BoxedFields   | NSJ Deserialize | 1000 | 12.965 ms |  1.00 |  2476.6 KB |        1.07 |
| BoxedTuple    | NSJ Deserialize | 1000 | 13.164 ms |  1.01 | 2312.22 KB |        1.00 |
| Overlap       | NSJ Deserialize | 1000 | 12.903 ms |  0.99 | 2316.01 KB |        1.00 |
| UnsafeOverlap | NSJ Deserialize | 1000 | 12.862 ms |  0.99 | 2307.09 KB |        1.00 |
| Record        | NSJ Deserialize | 1000 | 12.992 ms |  1.00 | 2258.66 KB |        0.98 |
|               |                 |      |           |       |            |             |
| Additive      | NSJ Serialize   | 1000 |  1.250 ms |  1.00 |  449.58 KB |        1.00 |
| BoxedFields   | NSJ Serialize   | 1000 |  1.284 ms |  1.03 |  473.02 KB |        1.05 |
| BoxedTuple    | NSJ Serialize   | 1000 |  1.318 ms |  1.05 |  441.77 KB |        0.98 |
| Overlap       | NSJ Serialize   | 1000 |  1.259 ms |  1.01 |  449.58 KB |        1.00 |
| UnsafeOverlap | NSJ Serialize   | 1000 |  1.261 ms |  1.01 |  449.58 KB |        1.00 |
| Record        | NSJ Serialize   | 1000 |  1.302 ms |  1.04 |  410.52 KB |        0.91 |

## Types.ManyJsonStjBenchmarks

| Method        | Categories      | N    | Mean       | Ratio | Allocated | Alloc Ratio |
|-------------- |---------------- |----- |-----------:|------:|----------:|------------:|
| Additive      | STJ Deserialize | 1000 | 5,854.1 μs |  1.00 | 173.17 KB |        1.00 |
| BoxedFields   | STJ Deserialize | 1000 | 5,827.6 μs |  1.00 | 310.33 KB |        1.79 |
| BoxedTuple    | STJ Deserialize | 1000 | 5,699.6 μs |  0.97 |  177.2 KB |        1.02 |
| Overlap       | STJ Deserialize | 1000 | 5,661.9 μs |  0.97 | 173.17 KB |        1.00 |
| UnsafeOverlap | STJ Deserialize | 1000 | 5,617.5 μs |  0.96 | 164.26 KB |        0.95 |
| Record        | STJ Deserialize | 1000 | 4,762.2 μs |  0.81 | 154.89 KB |        0.89 |
|               |                 |      |            |       |           |             |
| Additive      | STJ Serialize   | 1000 |   662.0 μs |  1.00 | 109.77 KB |        1.00 |
| BoxedFields   | STJ Serialize   | 1000 |   666.4 μs |  1.01 | 109.77 KB |        1.00 |
| BoxedTuple    | STJ Serialize   | 1000 |   683.8 μs |  1.03 | 109.77 KB |        1.00 |
| Overlap       | STJ Serialize   | 1000 |   686.9 μs |  1.04 | 109.77 KB |        1.00 |
| UnsafeOverlap | STJ Serialize   | 1000 | 1,114.4 μs |  1.68 | 109.77 KB |        1.00 |
| Record        | STJ Serialize   | 1000 |   666.5 μs |  1.01 | 109.77 KB |        1.00 |

## Types.PhysJsonNsjBenchmarks

| Method        | Categories      | N    | Mean      | Ratio | Allocated  | Alloc Ratio |
|-------------- |---------------- |----- |----------:|------:|-----------:|------------:|
| Additive      | NSJ Deserialize | 1000 | 13.239 ms |  1.00 | 2215.84 KB |        1.00 |
| BoxedFields   | NSJ Deserialize | 1000 | 13.446 ms |  1.02 | 2489.73 KB |        1.12 |
| BoxedTuple    | NSJ Deserialize | 1000 | 13.331 ms |  1.01 | 1987.51 KB |        0.90 |
| Overlap       | NSJ Deserialize | 1000 | 13.068 ms |  0.99 | 2184.25 KB |        0.99 |
| UnsafeOverlap | NSJ Deserialize | 1000 | 13.142 ms |  0.99 | 2175.34 KB |        0.98 |
| Record        | NSJ Deserialize | 1000 | 13.385 ms |  1.01 | 1935.41 KB |        0.87 |
|               |                 |      |           |       |            |             |
| Additive      | NSJ Serialize   | 1000 |  1.143 ms |  1.00 |  457.25 KB |        1.00 |
| BoxedFields   | NSJ Serialize   | 1000 |  1.182 ms |  1.03 |  511.94 KB |        1.12 |
| BoxedTuple    | NSJ Serialize   | 1000 |  1.148 ms |  1.00 |  394.75 KB |        0.86 |
| Overlap       | NSJ Serialize   | 1000 |  1.154 ms |  1.01 |  449.44 KB |        0.98 |
| UnsafeOverlap | NSJ Serialize   | 1000 |  1.185 ms |  1.04 |  449.44 KB |        0.98 |
| Record        | NSJ Serialize   | 1000 |  1.165 ms |  1.02 |   363.5 KB |        0.79 |

## Types.PhysJsonStjBenchmarks

| Method        | Categories      | N    | Mean       | Ratio | Allocated | Alloc Ratio |
|-------------- |---------------- |----- |-----------:|------:|----------:|------------:|
| Additive      | STJ Deserialize | 1000 | 5,984.2 μs |  1.00 |  345.5 KB |        1.00 |
| BoxedFields   | STJ Deserialize | 1000 | 6,201.2 μs |  1.04 |  564.7 KB |        1.63 |
| BoxedTuple    | STJ Deserialize | 1000 | 6,003.8 μs |  1.00 | 179.66 KB |        0.52 |
| Overlap       | STJ Deserialize | 1000 | 5,979.8 μs |  1.00 | 321.72 KB |        0.93 |
| UnsafeOverlap | STJ Deserialize | 1000 | 5,986.3 μs |  1.00 |  312.8 KB |        0.91 |
| Record        | STJ Deserialize | 1000 | 5,010.5 μs |  0.84 | 158.81 KB |        0.46 |
|               |                 |      |            |       |           |             |
| Additive      | STJ Serialize   | 1000 |   670.7 μs |  1.00 | 108.95 KB |        1.00 |
| BoxedFields   | STJ Serialize   | 1000 |   635.3 μs |  0.95 | 108.95 KB |        1.00 |
| BoxedTuple    | STJ Serialize   | 1000 |   642.0 μs |  0.96 | 108.95 KB |        1.00 |
| Overlap       | STJ Serialize   | 1000 |   630.6 μs |  0.94 | 108.95 KB |        1.00 |
| UnsafeOverlap | STJ Serialize   | 1000 |   617.0 μs |  0.92 | 108.95 KB |        1.00 |
| Record        | STJ Serialize   | 1000 |   606.1 μs |  0.90 | 108.95 KB |        1.00 |

## Types.WideJsonNsjBenchmarks

| Method        | Categories      | N    | Mean      | Ratio | Allocated  | Alloc Ratio |
|-------------- |---------------- |----- |----------:|------:|-----------:|------------:|
| Additive      | NSJ Deserialize | 1000 | 12.603 ms |  1.00 | 1963.95 KB |        1.00 |
| BoxedFields   | NSJ Deserialize | 1000 | 13.475 ms |  1.07 | 2074.02 KB |        1.06 |
| BoxedTuple    | NSJ Deserialize | 1000 | 12.562 ms |  1.00 | 1767.55 KB |        0.90 |
| Overlap       | NSJ Deserialize | 1000 | 12.527 ms |  0.99 | 1963.95 KB |        1.00 |
| UnsafeOverlap | NSJ Deserialize | 1000 | 12.663 ms |  1.00 | 1943.15 KB |        0.99 |
| Record        | NSJ Deserialize | 1000 | 12.308 ms |  0.98 | 1716.41 KB |        0.87 |
|               |                 |      |           |       |            |             |
| Additive      | NSJ Serialize   | 1000 |  1.092 ms |  1.00 |  398.63 KB |        1.00 |
| BoxedFields   | NSJ Serialize   | 1000 |  1.179 ms |  1.08 |  414.25 KB |        1.04 |
| BoxedTuple    | NSJ Serialize   | 1000 |  1.095 ms |  1.00 |  343.94 KB |        0.86 |
| Overlap       | NSJ Serialize   | 1000 |  1.110 ms |  1.02 |  398.63 KB |        1.00 |
| UnsafeOverlap | NSJ Serialize   | 1000 |  1.096 ms |  1.00 |  398.63 KB |        1.00 |
| Record        | NSJ Serialize   | 1000 |  1.089 ms |  1.00 |  312.69 KB |        0.78 |

## Types.WideJsonStjBenchmarks

| Method        | Categories      | N    | Mean       | Ratio | Allocated | Alloc Ratio |
|-------------- |---------------- |----- |-----------:|------:|----------:|------------:|
| Additive      | STJ Deserialize | 1000 | 6,108.3 μs |  1.00 | 322.38 KB |        1.00 |
| BoxedFields   | STJ Deserialize | 1000 | 6,182.2 μs |  1.01 | 416.82 KB |        1.29 |
| BoxedTuple    | STJ Deserialize | 1000 | 6,030.6 μs |  0.99 | 180.66 KB |        0.56 |
| Overlap       | STJ Deserialize | 1000 | 6,176.6 μs |  1.01 | 322.38 KB |        1.00 |
| UnsafeOverlap | STJ Deserialize | 1000 | 6,163.6 μs |  1.01 | 301.58 KB |        0.94 |
| Record        | STJ Deserialize | 1000 | 5,089.2 μs |  0.83 | 160.77 KB |        0.50 |
|               |                 |      |            |       |           |             |
| Additive      | STJ Serialize   | 1000 |   550.3 μs |  1.00 | 101.09 KB |        1.00 |
| BoxedFields   | STJ Serialize   | 1000 |   580.6 μs |  1.06 | 101.09 KB |        1.00 |
| BoxedTuple    | STJ Serialize   | 1000 |   571.1 μs |  1.04 | 101.09 KB |        1.00 |
| Overlap       | STJ Serialize   | 1000 |   586.7 μs |  1.07 | 101.09 KB |        1.00 |
| UnsafeOverlap | STJ Serialize   | 1000 |   565.9 μs |  1.03 | 101.09 KB |        1.00 |
| Record        | STJ Serialize   | 1000 |   572.8 μs |  1.04 | 101.09 KB |        1.00 |

