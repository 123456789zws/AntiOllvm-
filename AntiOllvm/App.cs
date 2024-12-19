using AntiOllvm.Analyze;
using AntiOllvm.Analyze.Type;

namespace AntiOllvm;

public class App
{

    public static void Init()
    {
        var readAllText = File.ReadAllText(@"E:\RiderDemo\AntiOllvm\AntiOllvm\cfg_output_0x181c6c.json");
        Simulation simulation = new(readAllText, @"E:\RiderDemo\AntiOllvm\AntiOllvm\fix.json");
        simulation.SetAnalyze(new CFFAnalyer());
        simulation.Run();
        
    }
}