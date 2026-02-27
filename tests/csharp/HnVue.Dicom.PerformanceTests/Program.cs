using BenchmarkDotNet.Running;
using HnVue.Dicom.PerformanceTests;

/// <summary>
/// Performance benchmark entry point for HnVue DICOM services.
/// SPEC-DICOM-001: Performance validation for C-STORE, C-FIND, and N-CREATE operations.
/// </summary>
/// <remarks>
/// Usage:
///   dotnet run -c Release                    -- Run all benchmarks
///   dotnet run -c Release -- --filter Cstore -- Run only C-STORE benchmarks
///   dotnet run -c Release -- --filter Worklist -- Run only Worklist benchmarks
///   dotnet run -c Release -- --filter Mpps -- Run only MPPS benchmarks
///
/// Requirements:
///   - Docker must be running (for Orthanc Testcontainers)
///   - Ports 11112, 11114, 11116 must be available
///   - Run in Release configuration for accurate results
/// </remarks>
class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("HnVue DICOM Performance Benchmarks");
        Console.WriteLine("===================================");
        Console.WriteLine();

        if (args.Length == 0)
        {
            // Run all benchmarks
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        }
        else
        {
            // Run with filter
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        }
    }
}
