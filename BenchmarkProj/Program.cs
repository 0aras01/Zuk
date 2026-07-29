using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using System.IO;
using System.Threading.Tasks;

public class PaletteBenchmark
{
    private string _testFile = "benchmark_palettes.json";

    [GlobalSetup]
    public void Setup()
    {
        string json = @"[{""Name"":""Test"",""IsBuiltIn"":true,""Stops"":[{""Position"":0.0,""R"":0,""G"":0,""B"":0}]}]";
        File.WriteAllText(_testFile, json);
    }

    [Benchmark(Baseline = true)]
    public void ReadSync_ThreadBlock()
    {
        var text = File.ReadAllText(_testFile);
    }

    [Benchmark]
    public async Task ReadAsync_NonBlocking()
    {
        var text = await File.ReadAllTextAsync(_testFile);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (File.Exists(_testFile)) File.Delete(_testFile);
    }
}

public class Program
{
    public static void Main(string[] args)
    {
        var summary = BenchmarkRunner.Run<PaletteBenchmark>();
    }
}
