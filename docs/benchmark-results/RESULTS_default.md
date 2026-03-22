# Benchmark Results

Generated: 2026-03-21 22:13:06

## BigConstructBenchmarks

| Method        | N    | Mean      | Ratio | Gen0    | Gen1    | Gen2    | Allocated | Alloc Ratio |
|-------------- |----- |----------:|------:|--------:|--------:|--------:|----------:|------------:|
| Additive      | 1000 |  7.321 μs |  1.00 |  2.6398 |       - |       - |  43.29 KB |        1.00 |
| BoxedFields   | 1000 | 38.190 μs |  5.22 | 27.7710 | 27.7710 | 27.7710 |  164.5 KB |        3.80 |
| BoxedTuple    | 1000 |  6.795 μs |  0.93 |  2.4109 |  0.2975 |       - |  39.41 KB |        0.91 |
| Overlap       | 1000 |  7.454 μs |  1.02 |  2.6398 |       - |       - |  43.29 KB |        1.00 |
| UnsafeOverlap | 1000 |  9.301 μs |  1.27 |  2.4567 |       - |       - |  40.36 KB |        0.93 |
| Record        | 1000 |  6.825 μs |  0.93 |  2.4109 |  0.3204 |       - |   39.4 KB |        0.91 |
| Class         | 1000 |  6.883 μs |  0.94 |  2.4109 |  0.3204 |       - |   39.4 KB |        0.91 |

## BigCopyBenchmarks

| Method        | N    | Mean      | Ratio | Allocated | Alloc Ratio |
|-------------- |----- |----------:|------:|----------:|------------:|
| Additive      | 1000 | 354.07 ns |  1.00 |         - |          NA |
| BoxedFields   | 1000 | 878.38 ns |  2.48 |         - |          NA |
| BoxedTuple    | 1000 | 124.94 ns |  0.35 |         - |          NA |
| Overlap       | 1000 | 354.74 ns |  1.00 |         - |          NA |
| UnsafeOverlap | 1000 | 327.93 ns |  0.93 |         - |          NA |
| Record        | 1000 |  62.99 ns |  0.18 |         - |          NA |
| Class         | 1000 |  62.99 ns |  0.18 |         - |          NA |

## BigJsonNsjBenchmarks

| Method        | Categories      | N    | Mean       | Ratio | Gen0     | Gen1    | Gen2    | Allocated  | Alloc Ratio |
|-------------- |---------------- |----- |-----------:|------:|---------:|--------:|--------:|-----------:|------------:|
| Additive      | NSJ Deserialize | 1000 |   854.3 μs |  1.00 | 173.8281 | 32.2266 |       - | 2840.27 KB |        1.00 |
| BoxedFields   | NSJ Deserialize | 1000 | 1,200.0 μs |  1.40 | 230.4688 | 82.0313 | 52.7344 | 3088.41 KB |        1.09 |
| BoxedTuple    | NSJ Deserialize | 1000 |   797.0 μs |  0.93 | 167.9688 | 27.3438 |       - | 2749.24 KB |        0.97 |
| Overlap       | NSJ Deserialize | 1000 |   802.3 μs |  0.94 | 173.8281 | 32.2266 |       - | 2840.27 KB |        1.00 |
| UnsafeOverlap | NSJ Deserialize | 1000 |   829.5 μs |  0.97 | 172.8516 | 31.2500 |       - | 2831.35 KB |        1.00 |
| Record        | NSJ Deserialize | 1000 |   816.3 μs |  0.96 | 165.0391 | 25.3906 |       - | 2702.02 KB |        0.95 |
| Class         | NSJ Deserialize | 1000 |   786.9 μs |  0.92 | 165.0391 | 25.3906 |       - | 2702.02 KB |        0.95 |
|               |                 |      |            |       |          |         |         |            |             |
| Additive      | NSJ Serialize   | 1000 |   310.5 μs |  1.00 |  41.9922 | 41.0156 | 41.0156 |  558.36 KB |        1.00 |
| BoxedFields   | NSJ Serialize   | 1000 |   324.6 μs |  1.05 |  42.4805 | 41.0156 | 41.0156 |  597.42 KB |        1.07 |
| BoxedTuple    | NSJ Serialize   | 1000 |   317.5 μs |  1.02 |  41.9922 | 41.0156 | 41.0156 |  527.11 KB |        0.94 |
| Overlap       | NSJ Serialize   | 1000 |   313.1 μs |  1.01 |  41.9922 | 41.0156 | 41.0156 |  558.36 KB |        1.00 |
| UnsafeOverlap | NSJ Serialize   | 1000 |   305.7 μs |  0.98 |  41.9922 | 41.0156 | 41.0156 |  558.36 KB |        1.00 |
| Record        | NSJ Serialize   | 1000 |   304.1 μs |  0.98 |  42.4805 | 41.0156 | 41.0156 |  495.86 KB |        0.89 |
| Class         | NSJ Serialize   | 1000 |   309.0 μs |  1.00 |  42.4805 | 41.0156 | 41.0156 |  495.86 KB |        0.89 |

## BigJsonStjBenchmarks

| Method        | Categories      | N    | Mean     | Ratio | Gen0    | Gen1    | Gen2    | Allocated | Alloc Ratio |
|-------------- |---------------- |----- |---------:|------:|--------:|--------:|--------:|----------:|------------:|
| Additive      | STJ Deserialize | 1000 | 604.8 μs |  1.00 | 13.6719 |  0.9766 |       - | 232.63 KB |        1.00 |
| BoxedFields   | STJ Deserialize | 1000 | 654.3 μs |  1.08 | 67.3828 | 65.4297 | 51.7578 | 441.94 KB |        1.90 |
| BoxedTuple    | STJ Deserialize | 1000 | 595.4 μs |  0.98 |  9.7656 |  0.9766 |       - | 172.85 KB |        0.74 |
| Overlap       | STJ Deserialize | 1000 | 601.6 μs |  0.99 | 13.6719 |  0.9766 |       - | 232.63 KB |        1.00 |
| UnsafeOverlap | STJ Deserialize | 1000 | 600.0 μs |  0.99 | 13.6719 |  1.9531 |       - | 223.71 KB |        0.96 |
| Record        | STJ Deserialize | 1000 | 593.9 μs |  0.98 |  8.7891 |       - |       - | 156.88 KB |        0.67 |
| Class         | STJ Deserialize | 1000 | 598.1 μs |  0.99 |  8.7891 |       - |       - | 156.88 KB |        0.67 |
|               |                 |      |          |       |         |         |         |           |             |
| Additive      | STJ Serialize   | 1000 | 235.1 μs |  1.00 | 39.5508 | 39.5508 | 39.5508 | 128.16 KB |        1.00 |
| BoxedFields   | STJ Serialize   | 1000 | 235.9 μs |  1.00 | 39.0625 | 39.0625 | 39.0625 | 128.16 KB |        1.00 |
| BoxedTuple    | STJ Serialize   | 1000 | 242.0 μs |  1.03 | 39.0625 | 39.0625 | 39.0625 | 128.16 KB |        1.00 |
| Overlap       | STJ Serialize   | 1000 | 228.4 μs |  0.97 | 39.0625 | 39.0625 | 39.0625 | 128.16 KB |        1.00 |
| UnsafeOverlap | STJ Serialize   | 1000 | 233.2 μs |  0.99 | 39.0625 | 39.0625 | 39.0625 | 128.16 KB |        1.00 |
| Record        | STJ Serialize   | 1000 | 236.3 μs |  1.01 | 39.0625 | 39.0625 | 39.0625 | 128.16 KB |        1.00 |
| Class         | STJ Serialize   | 1000 | 228.0 μs |  0.97 | 39.0625 | 39.0625 | 39.0625 | 128.16 KB |        1.00 |

## BigPropertyBenchmarks

| Method        | Categories  | N    | Mean       | Ratio | Gen0   | Allocated | Alloc Ratio |
|-------------- |------------ |----- |-----------:|------:|-------:|----------:|------------:|
| Additive      | Deconstruct | 1000 | 2,157.8 ns |  1.00 | 0.9537 |   15984 B |        1.00 |
| BoxedFields   | Deconstruct | 1000 | 2,336.8 ns |  1.08 |      - |         - |        0.00 |
| Overlap       | Deconstruct | 1000 | 2,179.2 ns |  1.01 | 0.9537 |   15984 B |        1.00 |
| UnsafeOverlap | Deconstruct | 1000 | 2,174.1 ns |  1.01 | 0.9537 |   15984 B |        1.00 |
| Record        | Deconstruct | 1000 | 1,481.3 ns |  0.69 |      - |         - |        0.00 |
| Class         | Deconstruct | 1000 | 1,494.5 ns |  0.69 |      - |         - |        0.00 |
|               |             |      |            |       |        |           |             |
| Additive      | Property    | 1000 |   956.7 ns |  1.00 |      - |         - |          NA |
| BoxedFields   | Property    | 1000 | 2,330.0 ns |  2.44 |      - |         - |          NA |
| BoxedTuple    | Property    | 1000 | 1,427.0 ns |  1.49 |      - |         - |          NA |
| Overlap       | Property    | 1000 |   957.8 ns |  1.00 |      - |         - |          NA |
| UnsafeOverlap | Property    | 1000 |   956.2 ns |  1.00 |      - |         - |          NA |
| Record        | Property    | 1000 | 1,445.6 ns |  1.51 |      - |         - |          NA |
| Class         | Property    | 1000 | 1,475.7 ns |  1.54 |      - |         - |          NA |

## ConstructBenchmarks

| Method        | Categories        | N    | Mean      | Ratio | Gen0    | Gen1    | Gen2    | Allocated | Alloc Ratio |
|-------------- |------------------ |----- |----------:|------:|--------:|--------:|--------:|----------:|------------:|
| additive      | Event Construct   | 1000 | 26.834 μs |  1.00 | 27.7710 | 27.7710 | 27.7710 |  86.27 KB |        1.00 |
| boxedFields   | Event Construct   | 1000 | 37.224 μs |  1.39 | 41.6260 | 41.6260 | 41.6260 |  168.3 KB |        1.95 |
| boxedTuple    | Event Construct   | 1000 | 11.629 μs |  0.43 |  2.1667 |  0.2594 |       - |  35.48 KB |        0.41 |
| overlap       | Event Construct   | 1000 | 27.448 μs |  1.02 | 27.7710 | 27.7710 | 27.7710 |  86.27 KB |        1.00 |
| unsafeOverlap | Event Construct   | 1000 | 29.264 μs |  1.09 | 27.7710 | 27.7710 | 27.7710 |  86.27 KB |        1.00 |
| record        | Event Construct   | 1000 |  7.471 μs |  0.28 |  2.2278 |  0.2747 |       - |  36.45 KB |        0.42 |
| class         | Event Construct   | 1000 |  7.929 μs |  0.30 |  2.2278 |  0.2747 |       - |  36.45 KB |        0.42 |
|               |                   |      |           |       |         |         |         |           |             |
| additive      | Physics Construct | 1000 | 11.962 μs |  1.00 |  4.7760 |  1.1902 |       - |  78.45 KB |        1.00 |
| boxedFields   | Physics Construct | 1000 | 39.793 μs |  3.33 | 41.6260 | 41.6260 | 41.6260 | 185.88 KB |        2.37 |
| boxedTuple    | Physics Construct | 1000 |  9.706 μs |  0.81 |  2.4567 |  0.3357 |       - |  40.36 KB |        0.51 |
| overlap       | Physics Construct | 1000 | 11.643 μs |  0.97 |  4.3030 |       - |       - |  70.63 KB |        0.90 |
| unsafeOverlap | Physics Construct | 1000 | 15.471 μs |  1.29 |  4.1199 |       - |       - |   67.7 KB |        0.86 |
| record        | Physics Construct | 1000 |  7.501 μs |  0.63 |  2.1667 |  0.2670 |       - |  35.48 KB |        0.45 |
| class         | Physics Construct | 1000 |  7.098 μs |  0.59 |  2.1667 |  0.2670 |       - |  35.48 KB |        0.45 |

## CopyBenchmarks

| Method        | N    | Mean        | Ratio | Allocated | Alloc Ratio |
|-------------- |----- |------------:|------:|----------:|------------:|
| additive      | 1000 |   866.46 ns |  1.00 |         - |          NA |
| boxedFields   | 1000 | 1,353.83 ns |  1.56 |         - |          NA |
| boxedTuple    | 1000 |   123.99 ns |  0.14 |         - |          NA |
| overlap       | 1000 |   870.23 ns |  1.00 |         - |          NA |
| unsafeOverlap | 1000 |   881.53 ns |  1.02 |         - |          NA |
| record        | 1000 |    63.06 ns |  0.07 |         - |          NA |
| class         | 1000 |    63.10 ns |  0.07 |         - |          NA |

## JsonNsjBenchmarks

| Method   | Categories      | N    | Mean     | Ratio | Gen0     | Gen1    | Gen2    | Allocated  | Alloc Ratio |
|--------- |---------------- |----- |---------:|------:|---------:|--------:|--------:|-----------:|------------:|
| additive | NSJ Deserialize | 1000 | 662.1 μs |  1.00 | 165.0391 | 54.6875 | 54.6875 | 2166.33 KB |        1.00 |
| record   | NSJ Deserialize | 1000 | 556.6 μs |  0.84 | 113.2813 | 21.4844 |       - | 1855.13 KB |        0.86 |
|          |                 |      |          |       |          |         |         |            |             |
| additive | NSJ RoundTrip   | 1000 | 873.4 μs |  1.00 | 181.6406 | 90.8203 | 90.8203 | 2570.22 KB |        1.00 |
| record   | NSJ RoundTrip   | 1000 | 761.1 μs |  0.87 | 137.6953 | 34.1797 | 34.1797 | 2157.49 KB |        0.84 |
|          |                 |      |          |       |          |         |         |            |             |
| additive | NSJ Serialize   | 1000 | 195.8 μs |  1.00 |  34.4238 | 34.4238 | 34.4238 |  403.89 KB |        1.00 |
| record   | NSJ Serialize   | 1000 | 184.9 μs |  0.94 |  34.4238 | 34.4238 | 34.4238 |  302.37 KB |        0.75 |

## JsonStjBenchmarks

| Method   | Categories      | N    | Mean     | Ratio | Gen0    | Gen1    | Gen2    | Allocated | Alloc Ratio |
|--------- |---------------- |----- |---------:|------:|--------:|--------:|--------:|----------:|------------:|
| additive | STJ Deserialize | 1000 | 509.9 μs |  1.00 | 54.6875 | 54.6875 | 54.6875 | 389.48 KB |        1.00 |
| record   | STJ Deserialize | 1000 | 457.3 μs |  0.90 | 10.7422 |  1.9531 |       - | 179.92 KB |        0.46 |
|          |                 |      |          |       |         |         |         |           |             |
| additive | STJ RoundTrip   | 1000 | 647.8 μs |  1.00 | 90.8203 | 90.8203 | 90.8203 | 496.56 KB |        1.00 |
| record   | STJ RoundTrip   | 1000 | 610.4 μs |  0.94 | 34.1797 | 34.1797 | 34.1797 | 287.05 KB |        0.58 |
|          |                 |      |          |       |         |         |         |           |             |
| additive | STJ Serialize   | 1000 | 141.6 μs |  1.00 | 34.4238 | 34.4238 | 34.4238 | 107.08 KB |        1.00 |
| record   | STJ Serialize   | 1000 | 145.2 μs |  1.03 | 34.4238 | 34.4238 | 34.4238 | 107.13 KB |        1.00 |

## ManagedConstructBenchmarks

| Method        | N    | Mean     | Ratio | Gen0   | Gen1   | Allocated | Alloc Ratio |
|-------------- |----- |---------:|------:|-------:|-------:|----------:|------------:|
| Additive      | 1000 | 1.860 μs |  1.00 | 1.9264 |      - |  31.57 KB |        1.00 |
| BoxedFields   | 1000 | 4.245 μs |  2.28 | 3.4714 | 0.6866 |  56.96 KB |        1.80 |
| BoxedTuple    | 1000 | 4.213 μs |  2.26 | 2.7695 | 0.4578 |  45.24 KB |        1.43 |
| Overlap       | 1000 | 5.731 μs |  3.08 | 1.9226 |      - |  31.57 KB |        1.00 |
| UnsafeOverlap | 1000 | 1.876 μs |  1.01 | 1.9264 |      - |  31.57 KB |        1.00 |
| Record        | 1000 | 7.847 μs |  4.22 | 2.6398 | 0.3815 |  43.29 KB |        1.37 |
| Class         | 1000 | 7.895 μs |  4.24 | 2.6398 | 0.3815 |  43.29 KB |        1.37 |

## ManagedCopyBenchmarks

| Method        | N    | Mean      | Ratio | Allocated | Alloc Ratio |
|-------------- |----- |----------:|------:|----------:|------------:|
| Additive      | 1000 | 318.93 ns |  1.00 |         - |          NA |
| BoxedFields   | 1000 | 404.81 ns |  1.27 |         - |          NA |
| BoxedTuple    | 1000 | 122.61 ns |  0.38 |         - |          NA |
| Overlap       | 1000 | 315.48 ns |  0.99 |         - |          NA |
| UnsafeOverlap | 1000 | 314.17 ns |  0.99 |         - |          NA |
| Record        | 1000 |  73.05 ns |  0.23 |         - |          NA |
| Class         | 1000 |  73.31 ns |  0.23 |         - |          NA |

## ManagedJsonNsjBenchmarks

| Method        | Categories      | N    | Mean     | Ratio | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|-------------- |---------------- |----- |---------:|------:|---------:|--------:|-----------:|------------:|
| Additive      | NSJ Deserialize | 1000 | 477.5 μs |  1.00 | 120.6055 | 25.3906 | 1971.26 KB |        1.00 |
| BoxedFields   | NSJ Deserialize | 1000 | 501.6 μs |  1.05 | 123.0469 | 28.3203 | 2020.43 KB |        1.02 |
| BoxedTuple    | NSJ Deserialize | 1000 | 459.0 μs |  0.96 | 118.1641 | 25.3906 | 1937.37 KB |        0.98 |
| Overlap       | NSJ Deserialize | 1000 | 475.0 μs |  0.99 | 120.6055 | 25.3906 | 1971.26 KB |        1.00 |
| UnsafeOverlap | NSJ Deserialize | 1000 | 468.1 μs |  0.98 | 120.6055 | 25.3906 | 1971.26 KB |        1.00 |
| Record        | NSJ Deserialize | 1000 | 463.8 μs |  0.97 | 115.2344 | 24.4141 |  1888.2 KB |        0.96 |
| Class         | NSJ Deserialize | 1000 | 468.8 μs |  0.98 | 115.2344 | 24.4141 |  1888.2 KB |        0.96 |
|               |                 |      |          |       |          |         |            |             |
| Additive      | NSJ Serialize   | 1000 | 106.1 μs |  1.00 |  14.6484 |  2.8076 |  241.64 KB |        1.00 |
| BoxedFields   | NSJ Serialize   | 1000 | 112.1 μs |  1.06 |  15.1367 |  3.2959 |  249.45 KB |        1.03 |
| BoxedTuple    | NSJ Serialize   | 1000 | 104.9 μs |  0.99 |  13.6719 |  3.2959 |  226.02 KB |        0.94 |
| Overlap       | NSJ Serialize   | 1000 | 107.3 μs |  1.01 |  14.6484 |  2.8076 |  241.64 KB |        1.00 |
| UnsafeOverlap | NSJ Serialize   | 1000 | 106.9 μs |  1.01 |  14.6484 |  2.8076 |  241.64 KB |        1.00 |
| Record        | NSJ Serialize   | 1000 | 104.9 μs |  0.99 |  11.8408 |  2.3193 |  194.77 KB |        0.81 |
| Class         | NSJ Serialize   | 1000 | 104.5 μs |  0.99 |  11.8408 |  2.3193 |  194.77 KB |        0.81 |

## ManagedJsonStjBenchmarks

| Method        | Categories      | N    | Mean      | Ratio | Gen0    | Gen1   | Allocated | Alloc Ratio |
|-------------- |---------------- |----- |----------:|------:|--------:|-------:|----------:|------------:|
| Additive      | STJ Deserialize | 1000 | 420.82 μs |  1.00 | 14.6484 | 2.4414 | 243.83 KB |        1.00 |
| BoxedFields   | STJ Deserialize | 1000 | 460.09 μs |  1.09 | 17.0898 | 3.4180 | 285.19 KB |        1.17 |
| BoxedTuple    | STJ Deserialize | 1000 | 421.01 μs |  1.00 | 13.6719 | 2.4414 | 225.56 KB |        0.93 |
| Overlap       | STJ Deserialize | 1000 | 419.90 μs |  1.00 | 14.6484 | 2.4414 | 243.83 KB |        1.00 |
| UnsafeOverlap | STJ Deserialize | 1000 | 425.13 μs |  1.01 | 14.6484 | 2.4414 | 243.83 KB |        1.00 |
| Record        | STJ Deserialize | 1000 | 419.23 μs |  1.00 | 12.6953 | 1.9531 | 207.64 KB |        0.85 |
| Class         | STJ Deserialize | 1000 | 418.24 μs |  0.99 | 12.6953 | 1.9531 | 207.64 KB |        0.85 |
|               |                 |      |           |       |         |        |           |             |
| Additive      | STJ Serialize   | 1000 |  83.72 μs |  1.00 |  4.8828 |      - |  81.17 KB |        1.00 |
| BoxedFields   | STJ Serialize   | 1000 |  82.79 μs |  0.99 |  4.8828 |      - |  81.17 KB |        1.00 |
| BoxedTuple    | STJ Serialize   | 1000 |  83.04 μs |  0.99 |  4.8828 |      - |  81.17 KB |        1.00 |
| Overlap       | STJ Serialize   | 1000 |  87.12 μs |  1.04 |  4.8828 |      - |  81.17 KB |        1.00 |
| UnsafeOverlap | STJ Serialize   | 1000 |  82.72 μs |  0.99 |  4.8828 |      - |  81.17 KB |        1.00 |
| Record        | STJ Serialize   | 1000 |  83.21 μs |  0.99 |  4.8828 |      - |  81.17 KB |        1.00 |
| Class         | STJ Serialize   | 1000 |  83.67 μs |  1.00 |  4.8828 |      - |  81.17 KB |        1.00 |

## ManagedPropertyBenchmarks

| Method        | Categories  | N    | Mean       | Ratio | Gen0   | Allocated | Alloc Ratio |
|-------------- |------------ |----- |-----------:|------:|-------:|----------:|------------:|
| Additive      | Deconstruct | 1000 | 1,699.5 ns |  1.00 | 0.3586 |    6000 B |        1.00 |
| BoxedFields   | Deconstruct | 1000 | 1,605.2 ns |  0.94 |      - |         - |        0.00 |
| Overlap       | Deconstruct | 1000 | 1,672.0 ns |  0.98 | 0.3586 |    6000 B |        1.00 |
| UnsafeOverlap | Deconstruct | 1000 | 1,693.7 ns |  1.00 | 0.3586 |    6000 B |        1.00 |
| Record        | Deconstruct | 1000 |   540.4 ns |  0.32 |      - |         - |        0.00 |
| Class         | Deconstruct | 1000 |   613.0 ns |  0.36 |      - |         - |        0.00 |
|               |             |      |            |       |        |           |             |
| Additive      | Property    | 1000 |   364.4 ns |  1.00 |      - |         - |          NA |
| BoxedFields   | Property    | 1000 | 1,587.2 ns |  4.36 |      - |         - |          NA |
| BoxedTuple    | Property    | 1000 | 1,606.2 ns |  4.41 |      - |         - |          NA |
| Overlap       | Property    | 1000 |   360.1 ns |  0.99 |      - |         - |          NA |
| UnsafeOverlap | Property    | 1000 |   362.3 ns |  0.99 |      - |         - |          NA |
| Record        | Property    | 1000 |   623.9 ns |  1.71 |      - |         - |          NA |
| Class         | Property    | 1000 |   534.8 ns |  1.47 |      - |         - |          NA |

## ManyConstructBenchmarks

| Method        | N    | Mean      | Ratio | Gen0   | Gen1   | Allocated | Alloc Ratio |
|-------------- |----- |----------:|------:|-------:|-------:|----------:|------------:|
| Additive      | 1000 |  5.497 μs |  1.00 | 1.4496 | 0.1297 |  23.76 KB |        1.00 |
| BoxedFields   | 1000 | 14.577 μs |  2.65 | 6.8817 | 2.2888 | 113.01 KB |        4.76 |
| BoxedTuple    | 1000 |  7.144 μs |  1.30 | 2.6779 | 0.3967 |  43.75 KB |        1.84 |
| Overlap       | 1000 |  5.557 μs |  1.01 | 1.4496 | 0.1297 |  23.76 KB |        1.00 |
| UnsafeOverlap | 1000 |  5.450 μs |  0.99 | 1.2665 | 0.0916 |  20.83 KB |        0.88 |
| Record        | 1000 |  8.569 μs |  1.56 | 2.2888 | 0.2747 |  37.41 KB |        1.57 |
| Class         | 1000 |  9.095 μs |  1.65 | 2.2888 | 0.2747 |  37.41 KB |        1.57 |

## ManyCopyBenchmarks

| Method        | N    | Mean      | Ratio | Allocated | Alloc Ratio |
|-------------- |----- |----------:|------:|----------:|------------:|
| Additive      | 1000 | 114.06 ns |  1.00 |         - |          NA |
| BoxedFields   | 1000 | 477.63 ns |  4.19 |         - |          NA |
| BoxedTuple    | 1000 | 122.79 ns |  1.08 |         - |          NA |
| Overlap       | 1000 | 108.66 ns |  0.95 |         - |          NA |
| UnsafeOverlap | 1000 | 120.52 ns |  1.06 |         - |          NA |
| Record        | 1000 |  63.49 ns |  0.56 |         - |          NA |
| Class         | 1000 |  73.87 ns |  0.65 |         - |          NA |

## ManyJsonNsjBenchmarks

| Method        | Categories      | N    | Mean     | Ratio | Gen0     | Gen1    | Gen2    | Allocated  | Alloc Ratio |
|-------------- |---------------- |----- |---------:|------:|---------:|--------:|--------:|-----------:|------------:|
| Additive      | NSJ Deserialize | 1000 | 692.6 μs |  1.00 | 143.5547 | 14.6484 |       - | 2347.26 KB |        1.00 |
| BoxedFields   | NSJ Deserialize | 1000 | 742.3 μs |  1.07 | 153.3203 | 37.1094 |       - | 2507.85 KB |        1.07 |
| BoxedTuple    | NSJ Deserialize | 1000 | 697.3 μs |  1.01 | 142.5781 | 19.5313 |       - | 2343.47 KB |        1.00 |
| Overlap       | NSJ Deserialize | 1000 | 705.2 μs |  1.02 | 143.5547 | 14.6484 |       - | 2347.26 KB |        1.00 |
| UnsafeOverlap | NSJ Deserialize | 1000 | 731.8 μs |  1.06 | 142.5781 | 12.6953 |       - | 2338.34 KB |        1.00 |
| Record        | NSJ Deserialize | 1000 | 726.3 μs |  1.05 | 139.6484 | 18.5547 |       - | 2289.91 KB |        0.98 |
| Class         | NSJ Deserialize | 1000 | 723.5 μs |  1.04 | 139.6484 | 18.5547 |       - | 2289.91 KB |        0.98 |
|               |                 |      |          |       |          |         |         |            |             |
| Additive      | NSJ Serialize   | 1000 | 278.1 μs |  1.00 |  35.6445 | 35.6445 | 35.6445 |  449.59 KB |        1.00 |
| BoxedFields   | NSJ Serialize   | 1000 | 305.6 μs |  1.10 |  35.6445 | 35.6445 | 35.6445 |  473.03 KB |        1.05 |
| BoxedTuple    | NSJ Serialize   | 1000 | 287.5 μs |  1.03 |  35.6445 | 35.6445 | 35.6445 |  441.78 KB |        0.98 |
| Overlap       | NSJ Serialize   | 1000 | 275.4 μs |  0.99 |  35.6445 | 35.6445 | 35.6445 |  449.59 KB |        1.00 |
| UnsafeOverlap | NSJ Serialize   | 1000 | 287.4 μs |  1.03 |  35.6445 | 35.6445 | 35.6445 |  449.59 KB |        1.00 |
| Record        | NSJ Serialize   | 1000 | 284.2 μs |  1.02 |  35.6445 | 35.6445 | 35.6445 |  410.53 KB |        0.91 |
| Class         | NSJ Serialize   | 1000 | 279.7 μs |  1.01 |  35.6445 | 35.6445 | 35.6445 |  410.53 KB |        0.91 |

## ManyJsonStjBenchmarks

| Method        | Categories      | N    | Mean     | Ratio | Gen0    | Gen1    | Gen2    | Allocated | Alloc Ratio |
|-------------- |---------------- |----- |---------:|------:|--------:|--------:|--------:|----------:|------------:|
| Additive      | STJ Deserialize | 1000 | 526.0 μs |  1.00 |  9.7656 |       - |       - | 173.17 KB |        1.00 |
| BoxedFields   | STJ Deserialize | 1000 | 547.9 μs |  1.04 | 18.5547 |  4.8828 |       - | 310.33 KB |        1.79 |
| BoxedTuple    | STJ Deserialize | 1000 | 531.0 μs |  1.01 | 10.7422 |  0.9766 |       - |  177.2 KB |        1.02 |
| Overlap       | STJ Deserialize | 1000 | 541.3 μs |  1.03 |  9.7656 |       - |       - | 173.17 KB |        1.00 |
| UnsafeOverlap | STJ Deserialize | 1000 | 526.9 μs |  1.00 |  9.7656 |       - |       - | 164.26 KB |        0.95 |
| Record        | STJ Deserialize | 1000 | 517.3 μs |  0.98 |  8.7891 |  0.9766 |       - | 154.89 KB |        0.89 |
| Class         | STJ Deserialize | 1000 | 524.8 μs |  1.00 |  8.7891 |  0.9766 |       - | 154.89 KB |        0.89 |
|               |                 |      |          |       |         |         |         |           |             |
| Additive      | STJ Serialize   | 1000 | 190.4 μs |  1.00 | 35.6445 | 35.6445 | 35.6445 |  109.8 KB |        1.00 |
| BoxedFields   | STJ Serialize   | 1000 | 192.7 μs |  1.01 | 35.1563 | 35.1563 | 35.1563 |  109.8 KB |        1.00 |
| BoxedTuple    | STJ Serialize   | 1000 | 195.5 μs |  1.03 | 35.1563 | 35.1563 | 35.1563 |  109.8 KB |        1.00 |
| Overlap       | STJ Serialize   | 1000 | 190.2 μs |  1.00 | 35.6445 | 35.6445 | 35.6445 |  109.8 KB |        1.00 |
| UnsafeOverlap | STJ Serialize   | 1000 | 191.9 μs |  1.01 | 35.1563 | 35.1563 | 35.1563 |  109.8 KB |        1.00 |
| Record        | STJ Serialize   | 1000 | 190.8 μs |  1.00 | 35.6445 | 35.6445 | 35.6445 |  109.8 KB |        1.00 |
| Class         | STJ Serialize   | 1000 | 193.5 μs |  1.02 | 35.1563 | 35.1563 | 35.1563 |  109.8 KB |        1.00 |

## ManyPropertyBenchmarks

| Method        | Categories  | N    | Mean       | Ratio | Gen0   | Allocated | Alloc Ratio |
|-------------- |------------ |----- |-----------:|------:|-------:|----------:|------------:|
| Additive      | Deconstruct | 1000 | 8,185.6 ns |  1.00 | 4.0283 |   67392 B |        1.00 |
| BoxedFields   | Deconstruct | 1000 | 4,254.3 ns |  0.52 |      - |         - |        0.00 |
| Overlap       | Deconstruct | 1000 | 7,447.8 ns |  0.91 | 4.0283 |   67392 B |        1.00 |
| UnsafeOverlap | Deconstruct | 1000 | 7,916.8 ns |  0.97 | 4.0283 |   67392 B |        1.00 |
| Record        | Deconstruct | 1000 | 1,866.6 ns |  0.23 |      - |         - |        0.00 |
| Class         | Deconstruct | 1000 | 1,896.3 ns |  0.23 |      - |         - |        0.00 |
|               |             |      |            |       |        |           |             |
| Additive      | Property    | 1000 |   651.6 ns |  1.00 |      - |         - |          NA |
| BoxedFields   | Property    | 1000 | 2,015.6 ns |  3.10 |      - |         - |          NA |
| BoxedTuple    | Property    | 1000 | 2,312.0 ns |  3.56 |      - |         - |          NA |
| Overlap       | Property    | 1000 |   656.5 ns |  1.01 |      - |         - |          NA |
| UnsafeOverlap | Property    | 1000 |   568.7 ns |  0.87 |      - |         - |          NA |
| Record        | Property    | 1000 | 1,896.9 ns |  2.92 |      - |         - |          NA |
| Class         | Property    | 1000 | 1,896.4 ns |  2.92 |      - |         - |          NA |

## MatchBenchmarks

| Method        | Categories  | N    | Dist     | Mean       | Ratio | Gen0   | Allocated | Alloc Ratio |
|-------------- |------------ |----- |--------- |-----------:|------:|-------:|----------:|------------:|
| **additive**      | **Deconstruct** | **1000** | **Uniform**  | **2,940.4 ns** |  **1.00** | **1.0757** |   **18000 B** |        **1.00** |
| boxedFields   | Deconstruct | 1000 | Uniform  | 2,219.2 ns |  0.75 |      - |         - |        0.00 |
| boxedTuple    | Deconstruct | 1000 | Uniform  | 1,852.5 ns |  0.63 |      - |         - |        0.00 |
| overlap       | Deconstruct | 1000 | Uniform  | 2,859.9 ns |  0.97 | 1.0757 |   18000 B |        1.00 |
| unsafeOverlap | Deconstruct | 1000 | Uniform  | 2,941.7 ns |  1.00 | 1.0757 |   18000 B |        1.00 |
| record        | Deconstruct | 1000 | Uniform  | 1,289.2 ns |  0.44 |      - |         - |        0.00 |
| class         | Deconstruct | 1000 | Uniform  | 1,296.8 ns |  0.44 |      - |         - |        0.00 |
|               |             |      |          |            |       |        |           |             |
| **additive**      | **Deconstruct** | **1000** | **Skewed80** | **1,905.7 ns** |  **1.00** | **0.2594** |    **4368 B** |        **1.00** |
| boxedFields   | Deconstruct | 1000 | Skewed80 | 1,690.0 ns |  0.89 |      - |         - |        0.00 |
| boxedTuple    | Deconstruct | 1000 | Skewed80 |   449.1 ns |  0.24 |      - |         - |        0.00 |
| overlap       | Deconstruct | 1000 | Skewed80 | 1,861.2 ns |  0.98 | 0.2594 |    4368 B |        1.00 |
| unsafeOverlap | Deconstruct | 1000 | Skewed80 | 1,911.1 ns |  1.00 | 0.2594 |    4368 B |        1.00 |
| record        | Deconstruct | 1000 | Skewed80 | 1,889.2 ns |  0.99 |      - |         - |        0.00 |
| class         | Deconstruct | 1000 | Skewed80 | 1,844.5 ns |  0.97 |      - |         - |        0.00 |
|               |             |      |          |            |       |        |           |             |
| **additive**      | **Property**    | **1000** | **Uniform**  | **1,414.0 ns** |  **1.00** |      **-** |         **-** |          **NA** |
| boxedFields   | Property    | 1000 | Uniform  | 2,234.0 ns |  1.58 |      - |         - |          NA |
| boxedTuple    | Property    | 1000 | Uniform  | 1,862.4 ns |  1.32 |      - |         - |          NA |
| overlap       | Property    | 1000 | Uniform  | 1,401.8 ns |  0.99 |      - |         - |          NA |
| unsafeOverlap | Property    | 1000 | Uniform  | 1,402.4 ns |  0.99 |      - |         - |          NA |
| record        | Property    | 1000 | Uniform  | 1,030.2 ns |  0.73 |      - |         - |          NA |
| class         | Property    | 1000 | Uniform  | 1,014.9 ns |  0.72 |      - |         - |          NA |
|               |             |      |          |            |       |        |           |             |
| **additive**      | **Property**    | **1000** | **Skewed80** | **1,363.1 ns** |  **1.00** |      **-** |         **-** |          **NA** |
| boxedFields   | Property    | 1000 | Skewed80 | 1,743.5 ns |  1.28 |      - |         - |          NA |
| boxedTuple    | Property    | 1000 | Skewed80 |   507.2 ns |  0.37 |      - |         - |          NA |
| overlap       | Property    | 1000 | Skewed80 | 1,356.7 ns |  1.00 |      - |         - |          NA |
| unsafeOverlap | Property    | 1000 | Skewed80 | 1,384.8 ns |  1.02 |      - |         - |          NA |
| record        | Property    | 1000 | Skewed80 | 1,370.4 ns |  1.01 |      - |         - |          NA |
| class         | Property    | 1000 | Skewed80 | 1,345.5 ns |  0.99 |      - |         - |          NA |

## MicroConstructBenchmarks

| Method        | Categories                | Mean      | Ratio  | Gen0   | Code Size | Allocated | Alloc Ratio |
|-------------- |-------------------------- |----------:|-------:|-------:|----------:|----------:|------------:|
| additive      | Micro Construct 4-field   | 0.4423 ns |   1.00 |      - |        NA |         - |          NA |
| boxedFields   | Micro Construct 4-field   | 9.2796 ns |  20.98 | 0.0057 |        NA |      96 B |          NA |
| boxedTuple    | Micro Construct 4-field   | 2.2948 ns |   5.19 | 0.0019 |      54 B |      32 B |          NA |
| overlap       | Micro Construct 4-field   | 0.2717 ns |   0.61 |      - |      70 B |         - |          NA |
| unsafeOverlap | Micro Construct 4-field   | 2.6600 ns |   6.01 |      - |      96 B |         - |          NA |
| record        | Micro Construct 4-field   | 2.9394 ns |   6.65 | 0.0019 |      49 B |      32 B |          NA |
| class         | Micro Construct 4-field   | 2.7666 ns |   6.26 | 0.0019 |      49 B |      32 B |          NA |
|               |                           |           |        |        |           |           |             |
| additive      | Micro Construct 5-mixed   | 0.2674 ns |   1.00 |      - |        NA |         - |          NA |
| boxedFields   | Micro Construct 5-mixed   | 7.0967 ns |  26.54 | 0.0043 |        NA |      72 B |          NA |
| boxedTuple    | Micro Construct 5-mixed   | 2.5708 ns |   9.61 | 0.0029 |      79 B |      48 B |          NA |
| overlap       | Micro Construct 5-mixed   | 3.5423 ns |  13.25 |      - |     139 B |         - |          NA |
| unsafeOverlap | Micro Construct 5-mixed   | 3.2463 ns |  12.14 |      - |     121 B |         - |          NA |
| record        | Micro Construct 5-mixed   | 3.0311 ns |  11.33 | 0.0029 |      74 B |      48 B |          NA |
| class         | Micro Construct 5-mixed   | 3.3912 ns |  12.68 | 0.0029 |      74 B |      48 B |          NA |
|               |                           |           |        |        |           |           |             |
| additive      | Micro Construct Fieldless | 0.1762 ns |  1.000 |      - |        NA |         - |          NA |
| boxedFields   | Micro Construct Fieldless | 0.7061 ns |  4.009 |      - |        NA |         - |          NA |
| boxedTuple    | Micro Construct Fieldless | 0.0000 ns |  0.000 |      - |       5 B |         - |          NA |
| overlap       | Micro Construct Fieldless | 0.0067 ns |  0.038 |      - |      42 B |         - |          NA |
| unsafeOverlap | Micro Construct Fieldless | 3.1293 ns | 17.766 |      - |      68 B |         - |          NA |
| record        | Micro Construct Fieldless | 2.7479 ns | 15.600 | 0.0014 |      22 B |      24 B |          NA |
| class         | Micro Construct Fieldless | 2.6704 ns | 15.160 | 0.0014 |      22 B |      24 B |          NA |

## MicroMatchBenchmarks

| Method        | Mean      | Ratio | Code Size | Allocated | Alloc Ratio |
|-------------- |----------:|------:|----------:|----------:|------------:|
| additive      | 0.0000 ns |     ? |      33 B |         - |           ? |
| boxedFields   | 1.2627 ns |     ? |     305 B |         - |           ? |
| boxedTuple    | 0.1894 ns |     ? |     203 B |         - |           ? |
| overlap       | 0.0030 ns |     ? |      44 B |         - |           ? |
| unsafeOverlap | 0.0055 ns |     ? |      44 B |         - |           ? |
| record        | 0.0959 ns |     ? |     279 B |         - |           ? |
| class         | 0.0883 ns |     ? |     279 B |         - |           ? |

## PhysConstructBenchmarks

| Method        | N    | Mean      | Ratio | Gen0    | Gen1    | Gen2    | Allocated | Alloc Ratio |
|-------------- |----- |----------:|------:|--------:|--------:|--------:|----------:|------------:|
| Additive      | 1000 |  7.203 μs |  1.00 |  4.7836 |  1.1902 |       - |  78.45 KB |        1.00 |
| BoxedFields   | 1000 | 33.746 μs |  4.69 | 41.6260 | 41.6260 | 41.6260 | 185.88 KB |        2.37 |
| BoxedTuple    | 1000 |  5.601 μs |  0.78 |  2.4643 |  0.3510 |       - |  40.36 KB |        0.51 |
| Overlap       | 1000 |  8.117 μs |  1.13 |  4.3030 |       - |       - |  70.63 KB |        0.90 |
| UnsafeOverlap | 1000 |  4.640 μs |  0.64 |  4.1275 |       - |       - |   67.7 KB |        0.86 |
| Record        | 1000 |  7.069 μs |  0.98 |  2.1667 |  0.2670 |       - |  35.48 KB |        0.45 |
| Class         | 1000 |  7.199 μs |  1.00 |  2.1667 |  0.2670 |       - |  35.48 KB |        0.45 |

## PhysCopyBenchmarks

| Method        | N    | Mean        | Ratio | Allocated | Alloc Ratio |
|-------------- |----- |------------:|------:|----------:|------------:|
| Additive      | 1000 |   631.87 ns |  1.00 |         - |          NA |
| BoxedFields   | 1000 | 1,363.13 ns |  2.16 |         - |          NA |
| BoxedTuple    | 1000 |   123.66 ns |  0.20 |         - |          NA |
| Overlap       | 1000 |   569.99 ns |  0.90 |         - |          NA |
| UnsafeOverlap | 1000 |   547.71 ns |  0.87 |         - |          NA |
| Record        | 1000 |    63.55 ns |  0.10 |         - |          NA |
| Class         | 1000 |    72.42 ns |  0.11 |         - |          NA |

## PhysJsonNsjBenchmarks

| Method        | Categories      | N    | Mean     | Ratio | Gen0     | Gen1    | Gen2    | Allocated  | Alloc Ratio |
|-------------- |---------------- |----- |---------:|------:|---------:|--------:|--------:|-----------:|------------:|
| Additive      | NSJ Deserialize | 1000 | 634.6 μs |  1.00 | 136.7188 | 37.1094 |       - | 2247.09 KB |        1.00 |
| BoxedFields   | NSJ Deserialize | 1000 | 948.4 μs |  1.49 | 220.7031 | 86.9141 | 83.0078 | 2521.18 KB |        1.12 |
| BoxedTuple    | NSJ Deserialize | 1000 | 628.4 μs |  0.99 | 123.0469 | 18.5547 |       - | 2018.76 KB |        0.90 |
| Overlap       | NSJ Deserialize | 1000 | 625.5 μs |  0.99 | 134.7656 | 33.2031 |       - |  2215.5 KB |        0.99 |
| UnsafeOverlap | NSJ Deserialize | 1000 | 630.5 μs |  0.99 | 134.7656 | 32.2266 |       - | 2206.59 KB |        0.98 |
| Record        | NSJ Deserialize | 1000 | 629.9 μs |  0.99 | 120.1172 | 16.6016 |       - | 1966.66 KB |        0.88 |
| Class         | NSJ Deserialize | 1000 | 610.1 μs |  0.96 | 120.1172 | 16.6016 |       - | 1966.66 KB |        0.88 |
|               |                 |      |          |       |          |         |         |            |             |
| Additive      | NSJ Serialize   | 1000 | 244.2 μs |  1.00 |  34.1797 | 34.1797 | 34.1797 |  457.27 KB |        1.00 |
| BoxedFields   | NSJ Serialize   | 1000 | 247.4 μs |  1.01 |  34.1797 | 34.1797 | 34.1797 |  511.96 KB |        1.12 |
| BoxedTuple    | NSJ Serialize   | 1000 | 243.6 μs |  1.00 |  34.4238 | 34.4238 | 34.4238 |  394.77 KB |        0.86 |
| Overlap       | NSJ Serialize   | 1000 | 234.5 μs |  0.96 |  34.4238 | 34.4238 | 34.4238 |  449.46 KB |        0.98 |
| UnsafeOverlap | NSJ Serialize   | 1000 | 242.7 μs |  0.99 |  34.4238 | 34.4238 | 34.4238 |  449.46 KB |        0.98 |
| Record        | NSJ Serialize   | 1000 | 237.4 μs |  0.97 |  34.4238 | 34.4238 | 34.4238 |  363.52 KB |        0.79 |
| Class         | NSJ Serialize   | 1000 | 230.1 μs |  0.94 |  34.4238 | 34.4238 | 34.4238 |  363.52 KB |        0.79 |

## PhysJsonStjBenchmarks

| Method        | Categories      | N    | Mean     | Ratio | Gen0    | Gen1    | Gen2    | Allocated | Alloc Ratio |
|-------------- |---------------- |----- |---------:|------:|--------:|--------:|--------:|----------:|------------:|
| Additive      | STJ Deserialize | 1000 | 478.1 μs |  1.00 | 20.9961 |  3.4180 |       - |  345.5 KB |        1.00 |
| BoxedFields   | STJ Deserialize | 1000 | 548.6 μs |  1.15 | 99.6094 | 93.7500 | 82.0313 | 565.11 KB |        1.64 |
| BoxedTuple    | STJ Deserialize | 1000 | 480.7 μs |  1.01 | 10.7422 |  1.4648 |       - | 179.66 KB |        0.52 |
| Overlap       | STJ Deserialize | 1000 | 468.2 μs |  0.98 | 19.5313 |       - |       - | 321.72 KB |        0.93 |
| UnsafeOverlap | STJ Deserialize | 1000 | 474.9 μs |  0.99 | 19.0430 |  2.9297 |       - |  312.8 KB |        0.91 |
| Record        | STJ Deserialize | 1000 | 467.6 μs |  0.98 |  9.2773 |  0.9766 |       - | 158.81 KB |        0.46 |
| Class         | STJ Deserialize | 1000 | 468.0 μs |  0.98 |  9.2773 |  0.9766 |       - | 158.81 KB |        0.46 |
|               |                 |      |          |       |         |         |         |           |             |
| Additive      | STJ Serialize   | 1000 | 177.6 μs |  1.00 | 34.4238 | 34.4238 | 34.4238 | 108.98 KB |        1.00 |
| BoxedFields   | STJ Serialize   | 1000 | 165.9 μs |  0.93 | 34.4238 | 34.4238 | 34.4238 | 108.98 KB |        1.00 |
| BoxedTuple    | STJ Serialize   | 1000 | 166.0 μs |  0.94 | 34.4238 | 34.4238 | 34.4238 | 108.98 KB |        1.00 |
| Overlap       | STJ Serialize   | 1000 | 166.8 μs |  0.94 | 34.4238 | 34.4238 | 34.4238 | 108.98 KB |        1.00 |
| UnsafeOverlap | STJ Serialize   | 1000 | 167.9 μs |  0.95 | 34.4238 | 34.4238 | 34.4238 | 108.98 KB |        1.00 |
| Record        | STJ Serialize   | 1000 | 165.9 μs |  0.93 | 34.4238 | 34.4238 | 34.4238 | 108.98 KB |        1.00 |
| Class         | STJ Serialize   | 1000 | 164.5 μs |  0.93 | 34.4238 | 34.4238 | 34.4238 | 108.98 KB |        1.00 |

## PhysPropertyBenchmarks

| Method        | Categories  | N    | Mean     | Ratio | Gen0   | Allocated | Alloc Ratio |
|-------------- |------------ |----- |---------:|------:|-------:|----------:|------------:|
| Additive      | Deconstruct | 1000 | 3.010 μs |  1.00 | 1.0757 |   18000 B |        1.00 |
| BoxedFields   | Deconstruct | 1000 | 2.449 μs |  0.81 |      - |         - |        0.00 |
| Overlap       | Deconstruct | 1000 | 2.908 μs |  0.97 | 1.0757 |   18000 B |        1.00 |
| UnsafeOverlap | Deconstruct | 1000 | 2.959 μs |  0.98 | 1.0757 |   18000 B |        1.00 |
| Record        | Deconstruct | 1000 | 1.088 μs |  0.36 |      - |         - |        0.00 |
| Class         | Deconstruct | 1000 | 1.092 μs |  0.36 |      - |         - |        0.00 |
|               |             |      |          |       |        |           |             |
| Additive      | Property    | 1000 | 1.402 μs |  1.00 |      - |         - |          NA |
| BoxedFields   | Property    | 1000 | 2.419 μs |  1.73 |      - |         - |          NA |
| BoxedTuple    | Property    | 1000 | 1.867 μs |  1.33 |      - |         - |          NA |
| Overlap       | Property    | 1000 | 1.387 μs |  0.99 |      - |         - |          NA |
| UnsafeOverlap | Property    | 1000 | 1.382 μs |  0.99 |      - |         - |          NA |
| Record        | Property    | 1000 | 1.084 μs |  0.77 |      - |         - |          NA |
| Class         | Property    | 1000 | 1.089 μs |  0.78 |      - |         - |          NA |

## SizeReportBenchmarks

| Method | Mean      | Allocated |
|------- |----------:|----------:|
| Dummy  | 0.0009 ns |         - |

## UpdateLoopBenchmarks

| Method        | Categories  | N    | Dist     | Mean       | Ratio | Gen0   | Allocated | Alloc Ratio |
|-------------- |------------ |----- |--------- |-----------:|------:|-------:|----------:|------------:|
| **additive**      | **Deconstruct** | **1000** | **Uniform**  | **1,524.3 ns** |  **1.00** | **0.1793** |    **3000 B** |        **1.00** |
| boxedFields   | Deconstruct | 1000 | Uniform  | 1,974.2 ns |  1.30 |      - |         - |        0.00 |
| boxedTuple    | Deconstruct | 1000 | Uniform  | 1,327.8 ns |  0.87 |      - |         - |        0.00 |
| overlap       | Deconstruct | 1000 | Uniform  | 1,279.5 ns |  0.84 | 0.1793 |    3000 B |        1.00 |
| unsafeOverlap | Deconstruct | 1000 | Uniform  | 1,462.3 ns |  0.96 | 0.1793 |    3000 B |        1.00 |
| record        | Deconstruct | 1000 | Uniform  | 1,133.1 ns |  0.74 |      - |         - |        0.00 |
| class         | Deconstruct | 1000 | Uniform  | 1,110.1 ns |  0.73 |      - |         - |        0.00 |
|               |             |      |          |            |       |        |           |             |
| **additive**      | **Deconstruct** | **1000** | **Skewed80** | **1,195.8 ns** |  **1.00** | **0.0439** |     **744 B** |        **1.00** |
| boxedFields   | Deconstruct | 1000 | Skewed80 | 1,590.0 ns |  1.33 |      - |         - |        0.00 |
| boxedTuple    | Deconstruct | 1000 | Skewed80 |   426.2 ns |  0.36 |      - |         - |        0.00 |
| overlap       | Deconstruct | 1000 | Skewed80 |   991.8 ns |  0.83 | 0.0439 |     744 B |        1.00 |
| unsafeOverlap | Deconstruct | 1000 | Skewed80 | 1,161.2 ns |  0.97 | 0.0439 |     744 B |        1.00 |
| record        | Deconstruct | 1000 | Skewed80 | 1,433.3 ns |  1.20 |      - |         - |        0.00 |
| class         | Deconstruct | 1000 | Skewed80 | 1,368.2 ns |  1.14 |      - |         - |        0.00 |
|               |             |      |          |            |       |        |           |             |
| **additive**      | **Property**    | **1000** | **Uniform**  |   **931.8 ns** |  **1.00** |      **-** |         **-** |          **NA** |
| boxedFields   | Property    | 1000 | Uniform  | 1,557.9 ns |  1.67 |      - |         - |          NA |
| boxedTuple    | Property    | 1000 | Uniform  | 1,339.7 ns |  1.44 |      - |         - |          NA |
| overlap       | Property    | 1000 | Uniform  |   874.3 ns |  0.94 |      - |         - |          NA |
| unsafeOverlap | Property    | 1000 | Uniform  |   870.2 ns |  0.93 |      - |         - |          NA |
| record        | Property    | 1000 | Uniform  |   842.1 ns |  0.90 |      - |         - |          NA |
| class         | Property    | 1000 | Uniform  |   848.3 ns |  0.91 |      - |         - |          NA |
|               |             |      |          |            |       |        |           |             |
| **additive**      | **Property**    | **1000** | **Skewed80** |   **877.2 ns** |  **1.00** |      **-** |         **-** |          **NA** |
| boxedFields   | Property    | 1000 | Skewed80 | 1,196.1 ns |  1.36 |      - |         - |          NA |
| boxedTuple    | Property    | 1000 | Skewed80 |   419.7 ns |  0.48 |      - |         - |          NA |
| overlap       | Property    | 1000 | Skewed80 |   810.0 ns |  0.92 |      - |         - |          NA |
| unsafeOverlap | Property    | 1000 | Skewed80 |   809.1 ns |  0.92 |      - |         - |          NA |
| record        | Property    | 1000 | Skewed80 | 1,054.6 ns |  1.20 |      - |         - |          NA |
| class         | Property    | 1000 | Skewed80 | 1,056.7 ns |  1.20 |      - |         - |          NA |

## WideConstructBenchmarks

| Method        | N    | Mean      | Ratio | Gen0   | Gen1   | Allocated | Alloc Ratio |
|-------------- |----- |----------:|------:|-------:|-------:|----------:|------------:|
| Additive      | 1000 |  3.557 μs |  1.00 | 3.3531 |      - |  55.01 KB |        1.00 |
| BoxedFields   | 1000 | 10.360 μs |  2.91 | 5.2643 | 1.3123 |  86.26 KB |        1.57 |
| BoxedTuple    | 1000 |  5.060 μs |  1.42 | 2.4872 | 0.3510 |   40.7 KB |        0.74 |
| Overlap       | 1000 |  5.373 μs |  1.51 | 1.9226 |      - |  31.57 KB |        0.57 |
| UnsafeOverlap | 1000 |  4.942 μs |  1.39 | 1.5106 | 0.1297 |  24.73 KB |        0.45 |
| Record        | 1000 |  7.400 μs |  2.08 | 2.2507 | 0.2747 |  36.77 KB |        0.67 |
| Class         | 1000 |  7.354 μs |  2.07 | 2.2507 | 0.2747 |  36.77 KB |        0.67 |

## WideCopyBenchmarks

| Method        | N    | Mean      | Ratio | Allocated | Alloc Ratio |
|-------------- |----- |----------:|------:|----------:|------------:|
| Additive      | 1000 | 449.04 ns |  1.00 |         - |          NA |
| BoxedFields   | 1000 | 407.24 ns |  0.91 |         - |          NA |
| BoxedTuple    | 1000 | 122.77 ns |  0.27 |         - |          NA |
| Overlap       | 1000 | 250.62 ns |  0.56 |         - |          NA |
| UnsafeOverlap | 1000 | 105.45 ns |  0.23 |         - |          NA |
| Record        | 1000 |  71.63 ns |  0.16 |         - |          NA |
| Class         | 1000 |  63.28 ns |  0.14 |         - |          NA |

