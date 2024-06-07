using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;
using Abm.Pyro.Domain.Benchmark.Extensions;

// var config = DefaultConfig.Instance
//     .AddJob(Job
//         .MediumRun
//         .WithLaunchCount(1)
//         .WithToolchain(InProcessEmitToolchain.Instance));

var config = DefaultConfig.Instance
    .AddJob(Job
        .MediumRun
        .WithLaunchCount(1)
        .WithToolchain(InProcessNoEmitToolchain.Instance));

//var summary = BenchmarkRunner.Run<StringSupportBenchmark>();
//var summary = BenchmarkRunner.Run<FhirUriFactoryBenchmark>(config);
var summary = BenchmarkRunner.Run<ResourceExtensionsBenchmark>(config);



