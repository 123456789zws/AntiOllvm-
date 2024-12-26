using AntiOllvm.Analyze;
using AntiOllvm.Analyze.Impl;
using AntiOllvm.Logging;

namespace AntiOllvm;

public class App
{
    public static void Init(Config config)
    {
        if (config == null)
        {
            Logger.RedNewline("config is null");
            return;
        }

    
        var readAllText = File.ReadAllText(config.ida_cfg_path);
        
      
        Simulation simulation = new(readAllText, config.fix_outpath);
        simulation.SetAnalyze(new SmartCffAnalayer(config));
        simulation.Run();
    }
}