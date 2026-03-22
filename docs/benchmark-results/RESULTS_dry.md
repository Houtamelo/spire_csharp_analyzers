# Benchmark Results

Generated: 2026-03-22 07:25:50  
Job: dry | N: 1000  
Runtime: .NET 10.0.0  

## BigConstructBenchmarks

| Method        | N    | Mean     | Ratio | Allocated | Alloc Ratio |
|-------------- |----- |---------:|------:|----------:|------------:|
| Additive      | 1000 | 495.9 μs |  1.00 |  43.29 KB |        1.00 |
| BoxedFields   | 1000 | 568.8 μs |  1.15 | 164.49 KB |        3.80 |
| BoxedTuple    | 1000 | 637.2 μs |  1.28 |  39.41 KB |        0.91 |
| Overlap       | 1000 | 474.1 μs |  0.96 |  43.29 KB |        1.00 |
| UnsafeOverlap | 1000 | 515.3 μs |  1.04 |  40.36 KB |        0.93 |
| Record        | 1000 | 491.6 μs |  0.99 |   39.4 KB |        0.91 |
| Class         | 1000 | 458.1 μs |  0.92 |   39.4 KB |        0.91 |

## BigCopyBenchmarks

| Method        | N    | Mean     | Ratio | Allocated | Alloc Ratio |
|-------------- |----- |---------:|------:|----------:|------------:|
| Additive      | 1000 | 131.7 μs |  1.00 |         - |          NA |
| BoxedFields   | 1000 | 152.6 μs |  1.16 |         - |          NA |
| BoxedTuple    | 1000 | 147.9 μs |  1.12 |         - |          NA |
| Overlap       | 1000 | 125.0 μs |  0.95 |         - |          NA |
| UnsafeOverlap | 1000 | 128.6 μs |  0.98 |         - |          NA |
| Record        | 1000 | 126.5 μs |  0.96 |         - |          NA |
| Class         | 1000 | 128.5 μs |  0.98 |         - |          NA |

## BigJsonNsjBenchmarks

| Method        | Categories      | N    | Mean      | Ratio | Allocated  | Alloc Ratio |
|-------------- |---------------- |----- |----------:|------:|-----------:|------------:|
| Additive      | NSJ Deserialize | 1000 | 16.255 ms |  1.00 | 2840.27 KB |        1.00 |
| BoxedFields   | NSJ Deserialize | 1000 | 16.111 ms |  0.99 | 3088.36 KB |        1.09 |
| BoxedTuple    | NSJ Deserialize | 1000 | 16.138 ms |  0.99 | 2749.24 KB |        0.97 |
| Overlap       | NSJ Deserialize | 1000 | 15.938 ms |  0.98 | 2840.27 KB |        1.00 |
| UnsafeOverlap | NSJ Deserialize | 1000 | 16.261 ms |  1.00 | 2831.35 KB |        1.00 |
| Record        | NSJ Deserialize | 1000 | 15.760 ms |  0.97 | 2702.02 KB |        0.95 |
| Class         | NSJ Deserialize | 1000 | 15.760 ms |  0.97 | 2702.02 KB |        0.95 |
|               |                 |      |           |       |            |             |
| Additive      | NSJ Serialize   | 1000 |  1.351 ms |  1.00 |  558.33 KB |        1.00 |
| BoxedFields   | NSJ Serialize   | 1000 |  1.395 ms |  1.03 |  597.39 KB |        1.07 |
| BoxedTuple    | NSJ Serialize   | 1000 |  1.371 ms |  1.02 |  527.08 KB |        0.94 |
| Overlap       | NSJ Serialize   | 1000 |  1.361 ms |  1.01 |  558.33 KB |        1.00 |
| UnsafeOverlap | NSJ Serialize   | 1000 |  1.358 ms |  1.01 |  558.33 KB |        1.00 |
| Record        | NSJ Serialize   | 1000 |  1.393 ms |  1.03 |  495.83 KB |        0.89 |
| Class         | NSJ Serialize   | 1000 |  1.359 ms |  1.01 |  495.83 KB |        0.89 |

## BigJsonStjBenchmarks

| Method        | Categories      | N    | Mean       | Ratio | Allocated | Alloc Ratio |
|-------------- |---------------- |----- |-----------:|------:|----------:|------------:|
| Additive      | STJ Deserialize | 1000 | 7,380.1 μs |  1.00 | 232.63 KB |        1.00 |
| BoxedFields   | STJ Deserialize | 1000 | 7,375.3 μs |  1.00 | 441.66 KB |        1.90 |
| BoxedTuple    | STJ Deserialize | 1000 | 7,425.9 μs |  1.01 | 172.85 KB |        0.74 |
| Overlap       | STJ Deserialize | 1000 | 7,472.9 μs |  1.01 | 232.63 KB |        1.00 |
| UnsafeOverlap | STJ Deserialize | 1000 | 7,407.1 μs |  1.00 | 223.71 KB |        0.96 |
| Record        | STJ Deserialize | 1000 | 6,225.8 μs |  0.84 | 156.88 KB |        0.67 |
| Class         | STJ Deserialize | 1000 | 6,302.7 μs |  0.85 | 156.88 KB |        0.67 |
|               |                 |      |            |       |           |             |
| Additive      | STJ Serialize   | 1000 |   730.4 μs |  1.00 | 128.13 KB |        1.00 |
| BoxedFields   | STJ Serialize   | 1000 |   718.2 μs |  0.98 | 128.13 KB |        1.00 |
| BoxedTuple    | STJ Serialize   | 1000 |   724.5 μs |  0.99 | 128.13 KB |        1.00 |
| Overlap       | STJ Serialize   | 1000 |   717.9 μs |  0.98 | 128.13 KB |        1.00 |
| UnsafeOverlap | STJ Serialize   | 1000 |   728.2 μs |  1.00 | 128.13 KB |        1.00 |
| Record        | STJ Serialize   | 1000 |   718.7 μs |  0.98 | 128.13 KB |        1.00 |
| Class         | STJ Serialize   | 1000 |   726.5 μs |  0.99 | 128.13 KB |        1.00 |

## BigPropertyBenchmarks

| Method        | Categories  | N    | Mean     | Ratio | Allocated | Alloc Ratio |
|-------------- |------------ |----- |---------:|------:|----------:|------------:|
| Additive      | Deconstruct | 1000 | 336.0 μs |  1.00 |   15984 B |        1.00 |
| BoxedFields   | Deconstruct | 1000 | 328.2 μs |  0.98 |         - |        0.00 |
| Overlap       | Deconstruct | 1000 | 330.8 μs |  0.98 |   15984 B |        1.00 |
| UnsafeOverlap | Deconstruct | 1000 | 375.0 μs |  1.12 |   15984 B |        1.00 |
| Record        | Deconstruct | 1000 | 368.5 μs |  1.10 |         - |        0.00 |
| Class         | Deconstruct | 1000 | 338.9 μs |  1.01 |         - |        0.00 |
|               |             |      |          |       |           |             |
| Additive      | Property    | 1000 | 330.2 μs |  1.00 |         - |          NA |
| BoxedFields   | Property    | 1000 | 346.9 μs |  1.05 |         - |          NA |
| BoxedTuple    | Property    | 1000 | 352.3 μs |  1.07 |         - |          NA |
| Overlap       | Property    | 1000 | 323.4 μs |  0.98 |         - |          NA |
| UnsafeOverlap | Property    | 1000 | 382.9 μs |  1.16 |         - |          NA |
| Record        | Property    | 1000 | 323.5 μs |  0.98 |         - |          NA |
| Class         | Property    | 1000 | 342.5 μs |  1.04 |         - |          NA |

## ConstructBenchmarks

| Method        | Categories        | N    | Mean       | Ratio | Allocated | Alloc Ratio |
|-------------- |------------------ |----- |-----------:|------:|----------:|------------:|
| additive      | Event Construct   | 1000 | 1,164.8 μs |  1.00 |  86.26 KB |        1.00 |
| boxedFields   | Event Construct   | 1000 | 1,237.7 μs |  1.06 | 168.29 KB |        1.95 |
| boxedTuple    | Event Construct   | 1000 | 1,309.4 μs |  1.12 |  35.48 KB |        0.41 |
| overlap       | Event Construct   | 1000 | 1,128.7 μs |  0.97 |  86.26 KB |        1.00 |
| unsafeOverlap | Event Construct   | 1000 | 1,219.6 μs |  1.05 |  86.26 KB |        1.00 |
| record        | Event Construct   | 1000 |   923.0 μs |  0.79 |  36.45 KB |        0.42 |
| class         | Event Construct   | 1000 |   852.6 μs |  0.73 |  36.45 KB |        0.42 |
|               |                   |      |            |       |           |             |
| additive      | Physics Construct | 1000 |   870.7 μs |  1.00 |  78.45 KB |        1.00 |
| boxedFields   | Physics Construct | 1000 | 1,017.8 μs |  1.17 | 185.87 KB |        2.37 |
| boxedTuple    | Physics Construct | 1000 | 1,031.1 μs |  1.18 |  40.36 KB |        0.51 |
| overlap       | Physics Construct | 1000 |   844.3 μs |  0.97 |  70.63 KB |        0.90 |
| unsafeOverlap | Physics Construct | 1000 |   927.5 μs |  1.07 |   67.7 KB |        0.86 |
| record        | Physics Construct | 1000 |   645.3 μs |  0.74 |  35.48 KB |        0.45 |
| class         | Physics Construct | 1000 |   626.3 μs |  0.72 |  35.48 KB |        0.45 |

## CopyBenchmarks

| Method        | N    | Mean     | Ratio | Allocated | Alloc Ratio |
|-------------- |----- |---------:|------:|----------:|------------:|
| additive      | 1000 | 168.6 μs |  1.00 |         - |          NA |
| boxedFields   | 1000 | 152.4 μs |  0.90 |         - |          NA |
| boxedTuple    | 1000 | 129.3 μs |  0.77 |         - |          NA |
| overlap       | 1000 | 146.2 μs |  0.87 |         - |          NA |
| unsafeOverlap | 1000 | 152.5 μs |  0.90 |         - |          NA |
| record        | 1000 | 140.5 μs |  0.83 |         - |          NA |
| class         | 1000 | 134.5 μs |  0.80 |         - |          NA |

## JsonNsjBenchmarks

| Method   | Categories      | N    | Mean      | Ratio | Allocated  | Alloc Ratio |
|--------- |---------------- |----- |----------:|------:|-----------:|------------:|
| additive | NSJ Deserialize | 1000 | 16.011 ms |  1.00 | 2166.29 KB |        1.00 |
| record   | NSJ Deserialize | 1000 | 16.130 ms |  1.01 | 1855.13 KB |        0.86 |
|          |                 |      |           |       |            |             |
| additive | NSJ RoundTrip   | 1000 | 17.142 ms |  1.00 | 2570.16 KB |        1.00 |
| record   | NSJ RoundTrip   | 1000 | 16.749 ms |  0.98 | 2157.47 KB |        0.84 |
|          |                 |      |           |       |            |             |
| additive | NSJ Serialize   | 1000 |  1.206 ms |  1.00 |  403.87 KB |        1.00 |
| record   | NSJ Serialize   | 1000 |  1.180 ms |  0.98 |  302.34 KB |        0.75 |

## JsonStjBenchmarks

| Method   | Categories      | N    | Mean       | Ratio | Allocated | Alloc Ratio |
|--------- |---------------- |----- |-----------:|------:|----------:|------------:|
| additive | STJ Deserialize | 1000 | 8,197.6 μs |  1.00 | 389.42 KB |        1.00 |
| record   | STJ Deserialize | 1000 | 7,060.7 μs |  0.86 | 179.92 KB |        0.46 |
|          |                 |      |            |       |           |             |
| additive | STJ RoundTrip   | 1000 | 9,157.9 μs |  1.00 | 496.47 KB |        1.00 |
| record   | STJ RoundTrip   | 1000 | 7,520.4 μs |  0.82 | 287.02 KB |        0.58 |
|          |                 |      |            |       |           |             |
| additive | STJ Serialize   | 1000 |   592.1 μs |  1.00 | 107.05 KB |        1.00 |
| record   | STJ Serialize   | 1000 |   583.2 μs |  0.98 | 107.09 KB |        1.00 |

## ManagedConstructBenchmarks

| Method        | N    | Mean     | Ratio | Allocated | Alloc Ratio |
|-------------- |----- |---------:|------:|----------:|------------:|
| Additive      | 1000 | 593.8 μs |  1.00 |  31.57 KB |        1.00 |
| BoxedFields   | 1000 | 630.4 μs |  1.06 |  56.96 KB |        1.80 |
| BoxedTuple    | 1000 | 771.0 μs |  1.30 |  45.24 KB |        1.43 |
| Overlap       | 1000 | 596.0 μs |  1.00 |  31.57 KB |        1.00 |
| UnsafeOverlap | 1000 | 645.2 μs |  1.09 |  31.57 KB |        1.00 |
| Record        | 1000 | 636.1 μs |  1.07 |  43.29 KB |        1.37 |
| Class         | 1000 | 646.0 μs |  1.09 |  43.29 KB |        1.37 |

## ManagedCopyBenchmarks

| Method        | N    | Mean     | Ratio | Allocated | Alloc Ratio |
|-------------- |----- |---------:|------:|----------:|------------:|
| Additive      | 1000 | 138.1 μs |  1.00 |         - |          NA |
| BoxedFields   | 1000 | 148.9 μs |  1.08 |         - |          NA |
| BoxedTuple    | 1000 | 126.8 μs |  0.92 |         - |          NA |
| Overlap       | 1000 | 129.7 μs |  0.94 |         - |          NA |
| UnsafeOverlap | 1000 | 133.5 μs |  0.97 |         - |          NA |
| Record        | 1000 | 126.3 μs |  0.91 |         - |          NA |
| Class         | 1000 | 130.6 μs |  0.95 |         - |          NA |

## ManagedJsonNsjBenchmarks

| Method        | Categories      | N    | Mean        | Ratio | Allocated  | Alloc Ratio |
|-------------- |---------------- |----- |------------:|------:|-----------:|------------:|
| Additive      | NSJ Deserialize | 1000 | 11,852.1 μs |  1.00 | 1971.26 KB |        1.00 |
| BoxedFields   | NSJ Deserialize | 1000 | 12,902.7 μs |  1.09 | 2020.43 KB |        1.02 |
| BoxedTuple    | NSJ Deserialize | 1000 | 11,836.1 μs |  1.00 | 1937.37 KB |        0.98 |
| Overlap       | NSJ Deserialize | 1000 | 12,093.0 μs |  1.02 | 1971.26 KB |        1.00 |
| UnsafeOverlap | NSJ Deserialize | 1000 | 13,250.3 μs |  1.12 | 1971.26 KB |        1.00 |
| Record        | NSJ Deserialize | 1000 | 11,600.7 μs |  0.98 |  1888.2 KB |        0.96 |
| Class         | NSJ Deserialize | 1000 | 11,740.1 μs |  0.99 |  1888.2 KB |        0.96 |
|               |                 |      |             |       |            |             |
| Additive      | NSJ Serialize   | 1000 |    993.3 μs |  1.00 |  241.64 KB |        1.00 |
| BoxedFields   | NSJ Serialize   | 1000 |    992.6 μs |  1.00 |  249.45 KB |        1.03 |
| BoxedTuple    | NSJ Serialize   | 1000 |    987.8 μs |  0.99 |  226.02 KB |        0.94 |
| Overlap       | NSJ Serialize   | 1000 |    999.6 μs |  1.01 |  241.64 KB |        1.00 |
| UnsafeOverlap | NSJ Serialize   | 1000 |  1,020.1 μs |  1.03 |  241.64 KB |        1.00 |
| Record        | NSJ Serialize   | 1000 |  1,002.1 μs |  1.01 |  194.77 KB |        0.81 |
| Class         | NSJ Serialize   | 1000 |  1,057.0 μs |  1.06 |  194.77 KB |        0.81 |

## ManagedJsonStjBenchmarks

| Method        | Categories      | N    | Mean       | Ratio | Allocated | Alloc Ratio |
|-------------- |---------------- |----- |-----------:|------:|----------:|------------:|
| Additive      | STJ Deserialize | 1000 | 4,985.5 μs |  1.00 | 243.83 KB |        1.00 |
| BoxedFields   | STJ Deserialize | 1000 | 4,959.6 μs |  0.99 | 285.19 KB |        1.17 |
| BoxedTuple    | STJ Deserialize | 1000 | 4,985.5 μs |  1.00 | 225.56 KB |        0.93 |
| Overlap       | STJ Deserialize | 1000 | 5,013.7 μs |  1.01 | 243.83 KB |        1.00 |
| UnsafeOverlap | STJ Deserialize | 1000 | 4,910.8 μs |  0.99 | 243.83 KB |        1.00 |
| Record        | STJ Deserialize | 1000 | 3,967.4 μs |  0.80 | 207.64 KB |        0.85 |
| Class         | STJ Deserialize | 1000 | 4,222.8 μs |  0.85 | 207.64 KB |        0.85 |
|               |                 |      |            |       |           |             |
| Additive      | STJ Serialize   | 1000 |   467.1 μs |  1.00 |  81.17 KB |        1.00 |
| BoxedFields   | STJ Serialize   | 1000 |   454.7 μs |  0.97 |  81.17 KB |        1.00 |
| BoxedTuple    | STJ Serialize   | 1000 |   465.9 μs |  1.00 |  81.17 KB |        1.00 |
| Overlap       | STJ Serialize   | 1000 |   473.3 μs |  1.01 |  81.17 KB |        1.00 |
| UnsafeOverlap | STJ Serialize   | 1000 |   468.9 μs |  1.00 |  81.17 KB |        1.00 |
| Record        | STJ Serialize   | 1000 |   458.7 μs |  0.98 |  81.17 KB |        1.00 |
| Class         | STJ Serialize   | 1000 |   443.7 μs |  0.95 |  81.17 KB |        1.00 |

## ManagedPropertyBenchmarks

| Method        | Categories  | N    | Mean     | Ratio | Allocated | Alloc Ratio |
|-------------- |------------ |----- |---------:|------:|----------:|------------:|
| Additive      | Deconstruct | 1000 | 342.1 μs |  1.00 |    6000 B |        1.00 |
| BoxedFields   | Deconstruct | 1000 | 342.3 μs |  1.00 |         - |        0.00 |
| Overlap       | Deconstruct | 1000 | 336.7 μs |  0.98 |    6000 B |        1.00 |
| UnsafeOverlap | Deconstruct | 1000 | 401.3 μs |  1.17 |    6000 B |        1.00 |
| Record        | Deconstruct | 1000 | 362.9 μs |  1.06 |         - |        0.00 |
| Class         | Deconstruct | 1000 | 275.1 μs |  0.80 |         - |        0.00 |
|               |             |      |          |       |           |             |
| Additive      | Property    | 1000 | 263.1 μs |  1.00 |         - |          NA |
| BoxedFields   | Property    | 1000 | 302.2 μs |  1.15 |         - |          NA |
| BoxedTuple    | Property    | 1000 | 287.2 μs |  1.09 |         - |          NA |
| Overlap       | Property    | 1000 | 273.4 μs |  1.04 |         - |          NA |
| UnsafeOverlap | Property    | 1000 | 285.9 μs |  1.09 |         - |          NA |
| Record        | Property    | 1000 | 288.2 μs |  1.10 |         - |          NA |
| Class         | Property    | 1000 | 281.3 μs |  1.07 |         - |          NA |

## ManyConstructBenchmarks

| Method        | N    | Mean     | Ratio | Allocated | Alloc Ratio |
|-------------- |----- |---------:|------:|----------:|------------:|
| Additive      | 1000 | 742.2 μs |  1.00 |  23.76 KB |        1.00 |
| BoxedFields   | 1000 | 877.3 μs |  1.18 | 113.01 KB |        4.76 |
| BoxedTuple    | 1000 | 979.7 μs |  1.32 |  43.75 KB |        1.84 |
| Overlap       | 1000 | 769.3 μs |  1.04 |  23.76 KB |        1.00 |
| UnsafeOverlap | 1000 | 885.4 μs |  1.19 |  20.83 KB |        0.88 |
| Record        | 1000 | 861.4 μs |  1.16 |  37.41 KB |        1.57 |
| Class         | 1000 | 764.2 μs |  1.03 |  37.41 KB |        1.57 |

## ManyCopyBenchmarks

| Method        | N    | Mean     | Ratio | Allocated | Alloc Ratio |
|-------------- |----- |---------:|------:|----------:|------------:|
| Additive      | 1000 | 124.6 μs |  1.00 |         - |          NA |
| BoxedFields   | 1000 | 131.9 μs |  1.06 |         - |          NA |
| BoxedTuple    | 1000 | 126.2 μs |  1.01 |         - |          NA |
| Overlap       | 1000 | 128.4 μs |  1.03 |         - |          NA |
| UnsafeOverlap | 1000 | 126.4 μs |  1.01 |         - |          NA |
| Record        | 1000 | 126.1 μs |  1.01 |         - |          NA |
| Class         | 1000 | 133.8 μs |  1.07 |         - |          NA |

## ManyJsonNsjBenchmarks

| Method        | Categories      | N    | Mean      | Ratio | Allocated  | Alloc Ratio |
|-------------- |---------------- |----- |----------:|------:|-----------:|------------:|
| Additive      | NSJ Deserialize | 1000 | 16.413 ms |  1.00 | 2347.26 KB |        1.00 |
| BoxedFields   | NSJ Deserialize | 1000 | 15.724 ms |  0.96 | 2507.85 KB |        1.07 |
| BoxedTuple    | NSJ Deserialize | 1000 | 15.896 ms |  0.97 | 2343.47 KB |        1.00 |
| Overlap       | NSJ Deserialize | 1000 | 15.872 ms |  0.97 | 2347.26 KB |        1.00 |
| UnsafeOverlap | NSJ Deserialize | 1000 | 15.632 ms |  0.95 | 2338.34 KB |        1.00 |
| Record        | NSJ Deserialize | 1000 | 15.712 ms |  0.96 | 2289.91 KB |        0.98 |
| Class         | NSJ Deserialize | 1000 | 16.928 ms |  1.03 | 2289.91 KB |        0.98 |
|               |                 |      |           |       |            |             |
| Additive      | NSJ Serialize   | 1000 |  1.209 ms |  1.00 |  449.58 KB |        1.00 |
| BoxedFields   | NSJ Serialize   | 1000 |  1.261 ms |  1.04 |  473.02 KB |        1.05 |
| BoxedTuple    | NSJ Serialize   | 1000 |  1.251 ms |  1.03 |  441.77 KB |        0.98 |
| Overlap       | NSJ Serialize   | 1000 |  1.211 ms |  1.00 |  449.58 KB |        1.00 |
| UnsafeOverlap | NSJ Serialize   | 1000 |  1.211 ms |  1.00 |  449.58 KB |        1.00 |
| Record        | NSJ Serialize   | 1000 |  1.224 ms |  1.01 |  410.52 KB |        0.91 |
| Class         | NSJ Serialize   | 1000 |  1.234 ms |  1.02 |  410.52 KB |        0.91 |

## ManyJsonStjBenchmarks

| Method        | Categories      | N    | Mean       | Ratio | Allocated | Alloc Ratio |
|-------------- |---------------- |----- |-----------:|------:|----------:|------------:|
| Additive      | STJ Deserialize | 1000 | 7,373.1 μs |  1.00 | 173.17 KB |        1.00 |
| BoxedFields   | STJ Deserialize | 1000 | 7,440.3 μs |  1.01 | 310.33 KB |        1.79 |
| BoxedTuple    | STJ Deserialize | 1000 | 7,584.6 μs |  1.03 |  177.2 KB |        1.02 |
| Overlap       | STJ Deserialize | 1000 | 7,421.8 μs |  1.01 | 173.17 KB |        1.00 |
| UnsafeOverlap | STJ Deserialize | 1000 | 7,345.3 μs |  1.00 | 164.26 KB |        0.95 |
| Record        | STJ Deserialize | 1000 | 6,421.0 μs |  0.87 | 154.89 KB |        0.89 |
| Class         | STJ Deserialize | 1000 | 6,464.9 μs |  0.88 | 154.89 KB |        0.89 |
|               |                 |      |            |       |           |             |
| Additive      | STJ Serialize   | 1000 |   659.6 μs |  1.00 | 109.77 KB |        1.00 |
| BoxedFields   | STJ Serialize   | 1000 |   661.2 μs |  1.00 | 109.77 KB |        1.00 |
| BoxedTuple    | STJ Serialize   | 1000 |   687.7 μs |  1.04 | 109.77 KB |        1.00 |
| Overlap       | STJ Serialize   | 1000 |   662.8 μs |  1.00 | 109.77 KB |        1.00 |
| UnsafeOverlap | STJ Serialize   | 1000 |   681.3 μs |  1.03 | 109.77 KB |        1.00 |
| Record        | STJ Serialize   | 1000 |   675.2 μs |  1.02 | 109.77 KB |        1.00 |
| Class         | STJ Serialize   | 1000 |   669.3 μs |  1.01 | 109.77 KB |        1.00 |

## ManyPropertyBenchmarks

| Method        | Categories  | N    | Mean     | Ratio | Allocated | Alloc Ratio |
|-------------- |------------ |----- |---------:|------:|----------:|------------:|
| Additive      | Deconstruct | 1000 | 658.8 μs |  1.00 |   67392 B |        1.00 |
| BoxedFields   | Deconstruct | 1000 | 553.5 μs |  0.84 |         - |        0.00 |
| Overlap       | Deconstruct | 1000 | 792.4 μs |  1.20 |   67392 B |        1.00 |
| UnsafeOverlap | Deconstruct | 1000 | 823.0 μs |  1.25 |   67392 B |        1.00 |
| Record        | Deconstruct | 1000 | 738.8 μs |  1.12 |         - |        0.00 |
| Class         | Deconstruct | 1000 | 601.0 μs |  0.91 |         - |        0.00 |
|               |             |      |          |       |           |             |
| Additive      | Property    | 1000 | 416.6 μs |  1.00 |         - |          NA |
| BoxedFields   | Property    | 1000 | 417.3 μs |  1.00 |         - |          NA |
| BoxedTuple    | Property    | 1000 | 601.6 μs |  1.44 |         - |          NA |
| Overlap       | Property    | 1000 | 409.5 μs |  0.98 |         - |          NA |
| UnsafeOverlap | Property    | 1000 | 447.8 μs |  1.07 |         - |          NA |
| Record        | Property    | 1000 | 601.9 μs |  1.44 |         - |          NA |
| Class         | Property    | 1000 | 597.5 μs |  1.43 |         - |          NA |

## MatchBenchmarks

| Method        | Categories  | N    | Dist     | Mean     | Ratio | Allocated | Alloc Ratio |
|-------------- |------------ |----- |--------- |---------:|------:|----------:|------------:|
| **additive**      | **Deconstruct** | **1000** | **Uniform**  | **432.5 μs** |  **1.00** |   **18000 B** |        **1.00** |
| boxedFields   | Deconstruct | 1000 | Uniform  | 418.8 μs |  0.97 |         - |        0.00 |
| boxedTuple    | Deconstruct | 1000 | Uniform  | 374.7 μs |  0.87 |         - |        0.00 |
| overlap       | Deconstruct | 1000 | Uniform  | 423.9 μs |  0.98 |   18000 B |        1.00 |
| unsafeOverlap | Deconstruct | 1000 | Uniform  | 516.4 μs |  1.19 |   18000 B |        1.00 |
| record        | Deconstruct | 1000 | Uniform  | 517.6 μs |  1.20 |         - |        0.00 |
| class         | Deconstruct | 1000 | Uniform  | 398.4 μs |  0.92 |         - |        0.00 |
|               |             |      |          |          |       |           |             |
| **additive**      | **Deconstruct** | **1000** | **Skewed80** | **414.6 μs** |  **1.00** |    **4368 B** |        **1.00** |
| boxedFields   | Deconstruct | 1000 | Skewed80 | 459.6 μs |  1.11 |         - |        0.00 |
| boxedTuple    | Deconstruct | 1000 | Skewed80 | 299.4 μs |  0.72 |         - |        0.00 |
| overlap       | Deconstruct | 1000 | Skewed80 | 426.2 μs |  1.03 |    4368 B |        1.00 |
| unsafeOverlap | Deconstruct | 1000 | Skewed80 | 499.8 μs |  1.21 |    4368 B |        1.00 |
| record        | Deconstruct | 1000 | Skewed80 | 485.1 μs |  1.17 |         - |        0.00 |
| class         | Deconstruct | 1000 | Skewed80 | 398.2 μs |  0.96 |         - |        0.00 |
|               |             |      |          |          |       |           |             |
| **additive**      | **Property**    | **1000** | **Uniform**  | **381.2 μs** |  **1.00** |         **-** |          **NA** |
| boxedFields   | Property    | 1000 | Uniform  | 425.8 μs |  1.12 |         - |          NA |
| boxedTuple    | Property    | 1000 | Uniform  | 442.1 μs |  1.16 |         - |          NA |
| overlap       | Property    | 1000 | Uniform  | 380.7 μs |  1.00 |         - |          NA |
| unsafeOverlap | Property    | 1000 | Uniform  | 452.5 μs |  1.19 |         - |          NA |
| record        | Property    | 1000 | Uniform  | 389.6 μs |  1.02 |         - |          NA |
| class         | Property    | 1000 | Uniform  | 400.6 μs |  1.05 |         - |          NA |
|               |             |      |          |          |       |           |             |
| **additive**      | **Property**    | **1000** | **Skewed80** | **368.8 μs** |  **1.00** |         **-** |          **NA** |
| boxedFields   | Property    | 1000 | Skewed80 | 401.7 μs |  1.09 |         - |          NA |
| boxedTuple    | Property    | 1000 | Skewed80 | 435.8 μs |  1.18 |         - |          NA |
| overlap       | Property    | 1000 | Skewed80 | 367.3 μs |  1.00 |         - |          NA |
| unsafeOverlap | Property    | 1000 | Skewed80 | 430.5 μs |  1.17 |         - |          NA |
| record        | Property    | 1000 | Skewed80 | 401.3 μs |  1.09 |         - |          NA |
| class         | Property    | 1000 | Skewed80 | 380.1 μs |  1.03 |         - |          NA |

## MicroConstructBenchmarks

| Method        | Categories                | Mean     | Ratio | Allocated | Alloc Ratio |
|-------------- |-------------------------- |---------:|------:|----------:|------------:|
| additive      | Micro Construct 4-field   | 218.4 μs |  1.00 |         - |          NA |
| boxedFields   | Micro Construct 4-field   | 248.2 μs |  1.14 |      96 B |          NA |
| boxedTuple    | Micro Construct 4-field   | 303.6 μs |  1.39 |      32 B |          NA |
| overlap       | Micro Construct 4-field   | 201.1 μs |  0.92 |         - |          NA |
| unsafeOverlap | Micro Construct 4-field   | 255.7 μs |  1.17 |         - |          NA |
| record        | Micro Construct 4-field   | 231.2 μs |  1.06 |      32 B |          NA |
| class         | Micro Construct 4-field   | 213.1 μs |  0.98 |      32 B |          NA |
|               |                           |          |       |           |             |
| additive      | Micro Construct 5-mixed   | 227.1 μs |  1.00 |         - |          NA |
| boxedFields   | Micro Construct 5-mixed   | 250.4 μs |  1.10 |      72 B |          NA |
| boxedTuple    | Micro Construct 5-mixed   | 321.5 μs |  1.42 |      48 B |          NA |
| overlap       | Micro Construct 5-mixed   | 210.4 μs |  0.93 |         - |          NA |
| unsafeOverlap | Micro Construct 5-mixed   | 254.1 μs |  1.12 |         - |          NA |
| record        | Micro Construct 5-mixed   | 237.9 μs |  1.05 |      48 B |          NA |
| class         | Micro Construct 5-mixed   | 227.0 μs |  1.00 |      48 B |          NA |
|               |                           |          |       |           |             |
| additive      | Micro Construct Fieldless | 215.1 μs |  1.00 |         - |          NA |
| boxedFields   | Micro Construct Fieldless | 207.1 μs |  0.96 |         - |          NA |
| boxedTuple    | Micro Construct Fieldless | 202.9 μs |  0.94 |         - |          NA |
| overlap       | Micro Construct Fieldless | 185.1 μs |  0.86 |         - |          NA |
| unsafeOverlap | Micro Construct Fieldless | 193.4 μs |  0.90 |         - |          NA |
| record        | Micro Construct Fieldless | 220.9 μs |  1.03 |      24 B |          NA |
| class         | Micro Construct Fieldless | 201.2 μs |  0.94 |      24 B |          NA |

## MicroMatchBenchmarks

| Method        | Mean     | Ratio | Code Size | Allocated | Alloc Ratio |
|-------------- |---------:|------:|----------:|----------:|------------:|
| additive      | 219.8 μs |  1.00 |        NA |         - |          NA |
| boxedFields   | 248.0 μs |  1.13 |        NA |         - |          NA |
| boxedTuple    | 185.0 μs |  0.84 |     209 B |         - |          NA |
| overlap       | 225.3 μs |  1.02 |        NA |         - |          NA |
| unsafeOverlap | 260.2 μs |  1.18 |        NA |         - |          NA |
| record        | 223.3 μs |  1.02 |     377 B |         - |          NA |
| class         | 222.6 μs |  1.01 |     377 B |         - |          NA |

## PhysConstructBenchmarks

| Method        | N    | Mean     | Ratio | Allocated | Alloc Ratio |
|-------------- |----- |---------:|------:|----------:|------------:|
| Additive      | 1000 | 798.4 μs |  1.00 |  78.45 KB |        1.00 |
| BoxedFields   | 1000 | 925.0 μs |  1.16 | 185.87 KB |        2.37 |
| BoxedTuple    | 1000 | 997.0 μs |  1.25 |  40.36 KB |        0.51 |
| Overlap       | 1000 | 750.3 μs |  0.94 |  70.63 KB |        0.90 |
| UnsafeOverlap | 1000 | 815.2 μs |  1.02 |   67.7 KB |        0.86 |
| Record        | 1000 | 800.3 μs |  1.00 |  35.48 KB |        0.45 |
| Class         | 1000 | 729.2 μs |  0.91 |  35.48 KB |        0.45 |

## PhysCopyBenchmarks

| Method        | N    | Mean     | Ratio | Allocated | Alloc Ratio |
|-------------- |----- |---------:|------:|----------:|------------:|
| Additive      | 1000 | 127.9 μs |  1.00 |         - |          NA |
| BoxedFields   | 1000 | 159.6 μs |  1.25 |         - |          NA |
| BoxedTuple    | 1000 | 130.1 μs |  1.02 |         - |          NA |
| Overlap       | 1000 | 129.4 μs |  1.01 |         - |          NA |
| UnsafeOverlap | 1000 | 126.8 μs |  0.99 |         - |          NA |
| Record        | 1000 | 139.9 μs |  1.09 |         - |          NA |
| Class         | 1000 | 127.8 μs |  1.00 |         - |          NA |

## PhysJsonNsjBenchmarks

| Method        | Categories      | N    | Mean      | Ratio | Allocated  | Alloc Ratio |
|-------------- |---------------- |----- |----------:|------:|-----------:|------------:|
| Additive      | NSJ Deserialize | 1000 | 15.958 ms |  1.00 | 2247.09 KB |        1.00 |
| BoxedFields   | NSJ Deserialize | 1000 | 16.701 ms |  1.05 | 2520.98 KB |        1.12 |
| BoxedTuple    | NSJ Deserialize | 1000 | 15.921 ms |  1.00 | 2018.76 KB |        0.90 |
| Overlap       | NSJ Deserialize | 1000 | 16.042 ms |  1.01 |  2215.5 KB |        0.99 |
| UnsafeOverlap | NSJ Deserialize | 1000 | 16.104 ms |  1.01 | 2206.59 KB |        0.98 |
| Record        | NSJ Deserialize | 1000 | 15.795 ms |  0.99 | 1966.66 KB |        0.88 |
| Class         | NSJ Deserialize | 1000 | 15.480 ms |  0.97 | 1966.66 KB |        0.88 |
|               |                 |      |           |       |            |             |
| Additive      | NSJ Serialize   | 1000 |  1.130 ms |  1.00 |  457.25 KB |        1.00 |
| BoxedFields   | NSJ Serialize   | 1000 |  1.163 ms |  1.03 |  511.94 KB |        1.12 |
| BoxedTuple    | NSJ Serialize   | 1000 |  1.148 ms |  1.02 |  394.75 KB |        0.86 |
| Overlap       | NSJ Serialize   | 1000 |  1.130 ms |  1.00 |  449.44 KB |        0.98 |
| UnsafeOverlap | NSJ Serialize   | 1000 |  1.124 ms |  1.00 |  449.44 KB |        0.98 |
| Record        | NSJ Serialize   | 1000 |  1.143 ms |  1.01 |   363.5 KB |        0.79 |
| Class         | NSJ Serialize   | 1000 |  1.137 ms |  1.01 |   363.5 KB |        0.79 |

## PhysJsonStjBenchmarks

| Method        | Categories      | N    | Mean       | Ratio | Allocated | Alloc Ratio |
|-------------- |---------------- |----- |-----------:|------:|----------:|------------:|
| Additive      | STJ Deserialize | 1000 | 7,737.8 μs |  1.00 |  345.5 KB |        1.00 |
| BoxedFields   | STJ Deserialize | 1000 | 8,134.0 μs |  1.05 |  564.7 KB |        1.63 |
| BoxedTuple    | STJ Deserialize | 1000 | 7,903.0 μs |  1.02 | 179.66 KB |        0.52 |
| Overlap       | STJ Deserialize | 1000 | 7,750.6 μs |  1.00 | 321.72 KB |        0.93 |
| UnsafeOverlap | STJ Deserialize | 1000 | 7,664.6 μs |  0.99 |  312.8 KB |        0.91 |
| Record        | STJ Deserialize | 1000 | 6,965.3 μs |  0.90 | 158.81 KB |        0.46 |
| Class         | STJ Deserialize | 1000 | 6,857.6 μs |  0.89 | 158.81 KB |        0.46 |
|               |                 |      |            |       |           |             |
| Additive      | STJ Serialize   | 1000 |   608.9 μs |  1.00 | 108.95 KB |        1.00 |
| BoxedFields   | STJ Serialize   | 1000 |   631.6 μs |  1.04 | 108.95 KB |        1.00 |
| BoxedTuple    | STJ Serialize   | 1000 |   625.8 μs |  1.03 | 108.95 KB |        1.00 |
| Overlap       | STJ Serialize   | 1000 |   631.0 μs |  1.04 | 108.95 KB |        1.00 |
| UnsafeOverlap | STJ Serialize   | 1000 |   633.0 μs |  1.04 | 108.95 KB |        1.00 |
| Record        | STJ Serialize   | 1000 |   611.0 μs |  1.00 | 108.95 KB |        1.00 |
| Class         | STJ Serialize   | 1000 |   610.5 μs |  1.00 | 108.95 KB |        1.00 |

## PhysPropertyBenchmarks

| Method        | Categories  | N    | Mean     | Ratio | Allocated | Alloc Ratio |
|-------------- |------------ |----- |---------:|------:|----------:|------------:|
| Additive      | Deconstruct | 1000 | 440.3 μs |  1.00 |   18000 B |        1.00 |
| BoxedFields   | Deconstruct | 1000 | 431.0 μs |  0.98 |         - |        0.00 |
| Overlap       | Deconstruct | 1000 | 446.2 μs |  1.01 |   18000 B |        1.00 |
| UnsafeOverlap | Deconstruct | 1000 | 520.4 μs |  1.18 |   18000 B |        1.00 |
| Record        | Deconstruct | 1000 | 466.7 μs |  1.06 |         - |        0.00 |
| Class         | Deconstruct | 1000 | 396.0 μs |  0.90 |         - |        0.00 |
|               |             |      |          |       |           |             |
| Additive      | Property    | 1000 | 482.2 μs |  1.00 |         - |          NA |
| BoxedFields   | Property    | 1000 | 403.8 μs |  0.84 |         - |          NA |
| BoxedTuple    | Property    | 1000 | 434.8 μs |  0.90 |         - |          NA |
| Overlap       | Property    | 1000 | 367.2 μs |  0.76 |         - |          NA |
| UnsafeOverlap | Property    | 1000 | 447.3 μs |  0.93 |         - |          NA |
| Record        | Property    | 1000 | 395.2 μs |  0.82 |         - |          NA |
| Class         | Property    | 1000 | 420.1 μs |  0.87 |         - |          NA |

## SizeReportBenchmarks

| Method | Mean     | Allocated |
|------- |---------:|----------:|
| Dummy  | 129.7 μs |         - |

## UpdateLoopBenchmarks

| Method        | Categories  | N    | Dist     | Mean     | Ratio | Allocated | Alloc Ratio |
|-------------- |------------ |----- |--------- |---------:|------:|----------:|------------:|
| **additive**      | **Deconstruct** | **1000** | **Uniform**  | **385.9 μs** |  **1.00** |    **3000 B** |        **1.00** |
| boxedFields   | Deconstruct | 1000 | Uniform  | 390.1 μs |  1.01 |         - |        0.00 |
| boxedTuple    | Deconstruct | 1000 | Uniform  | 321.4 μs |  0.83 |         - |        0.00 |
| overlap       | Deconstruct | 1000 | Uniform  | 384.5 μs |  1.00 |    3000 B |        1.00 |
| unsafeOverlap | Deconstruct | 1000 | Uniform  | 448.5 μs |  1.16 |    3000 B |        1.00 |
| record        | Deconstruct | 1000 | Uniform  | 438.4 μs |  1.14 |         - |        0.00 |
| class         | Deconstruct | 1000 | Uniform  | 339.3 μs |  0.88 |         - |        0.00 |
|               |             |      |          |          |       |           |             |
| **additive**      | **Deconstruct** | **1000** | **Skewed80** | **393.8 μs** |  **1.00** |     **744 B** |        **1.00** |
| boxedFields   | Deconstruct | 1000 | Skewed80 | 387.6 μs |  0.98 |         - |        0.00 |
| boxedTuple    | Deconstruct | 1000 | Skewed80 | 325.4 μs |  0.83 |         - |        0.00 |
| overlap       | Deconstruct | 1000 | Skewed80 | 379.4 μs |  0.96 |     744 B |        1.00 |
| unsafeOverlap | Deconstruct | 1000 | Skewed80 | 462.8 μs |  1.18 |     744 B |        1.00 |
| record        | Deconstruct | 1000 | Skewed80 | 436.9 μs |  1.11 |         - |        0.00 |
| class         | Deconstruct | 1000 | Skewed80 | 355.4 μs |  0.90 |         - |        0.00 |
|               |             |      |          |          |       |           |             |
| **additive**      | **Property**    | **1000** | **Uniform**  | **333.1 μs** |  **1.00** |         **-** |          **NA** |
| boxedFields   | Property    | 1000 | Uniform  | 357.1 μs |  1.07 |         - |          NA |
| boxedTuple    | Property    | 1000 | Uniform  | 357.6 μs |  1.07 |         - |          NA |
| overlap       | Property    | 1000 | Uniform  | 351.5 μs |  1.06 |         - |          NA |
| unsafeOverlap | Property    | 1000 | Uniform  | 396.6 μs |  1.19 |         - |          NA |
| record        | Property    | 1000 | Uniform  | 341.1 μs |  1.02 |         - |          NA |
| class         | Property    | 1000 | Uniform  | 354.0 μs |  1.06 |         - |          NA |
|               |             |      |          |          |       |           |             |
| **additive**      | **Property**    | **1000** | **Skewed80** | **346.9 μs** |  **1.00** |         **-** |          **NA** |
| boxedFields   | Property    | 1000 | Skewed80 | 353.9 μs |  1.02 |         - |          NA |
| boxedTuple    | Property    | 1000 | Skewed80 | 371.6 μs |  1.07 |         - |          NA |
| overlap       | Property    | 1000 | Skewed80 | 338.5 μs |  0.98 |         - |          NA |
| unsafeOverlap | Property    | 1000 | Skewed80 | 386.3 μs |  1.11 |         - |          NA |
| record        | Property    | 1000 | Skewed80 | 357.4 μs |  1.03 |         - |          NA |
| class         | Property    | 1000 | Skewed80 | 343.8 μs |  0.99 |         - |          NA |

## WideConstructBenchmarks

| Method        | N    | Mean     | Ratio | Allocated | Alloc Ratio |
|-------------- |----- |---------:|------:|----------:|------------:|
| Additive      | 1000 | 702.2 μs |  1.00 |  55.01 KB |        1.00 |
| BoxedFields   | 1000 | 760.3 μs |  1.08 |  86.26 KB |        1.57 |
| BoxedTuple    | 1000 | 842.7 μs |  1.20 |   40.7 KB |        0.74 |
| Overlap       | 1000 | 723.4 μs |  1.03 |  31.57 KB |        0.57 |
| UnsafeOverlap | 1000 | 850.8 μs |  1.21 |  24.73 KB |        0.45 |
| Record        | 1000 | 731.0 μs |  1.04 |  36.77 KB |        0.67 |
| Class         | 1000 | 684.3 μs |  0.97 |  36.77 KB |        0.67 |

## WideCopyBenchmarks

| Method        | N    | Mean     | Ratio | Allocated | Alloc Ratio |
|-------------- |----- |---------:|------:|----------:|------------:|
| Additive      | 1000 | 140.9 μs |  1.00 |         - |          NA |
| BoxedFields   | 1000 | 129.2 μs |  0.92 |         - |          NA |
| BoxedTuple    | 1000 | 130.6 μs |  0.93 |         - |          NA |
| Overlap       | 1000 | 130.4 μs |  0.93 |         - |          NA |
| UnsafeOverlap | 1000 | 160.1 μs |  1.14 |         - |          NA |
| Record        | 1000 | 128.7 μs |  0.91 |         - |          NA |
| Class         | 1000 | 133.7 μs |  0.95 |         - |          NA |

