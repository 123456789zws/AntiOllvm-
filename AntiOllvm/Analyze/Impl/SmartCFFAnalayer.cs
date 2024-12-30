using AntiOllvm.entity;
using AntiOllvm.Helper;

namespace AntiOllvm.Analyze.Impl;

public class SmartCffAnalayer : IAnalyze
{
    private Config _config;

    private SmartDispatcherFinder _dispatcherFinder;

    public SmartCffAnalayer(Config config)
    {
        _config = config;
        _dispatcherFinder=new SmartDispatcherFinder();
    }
    public void Init(List<Block> blocks, Simulation simulation)
    {
        _dispatcherFinder.Init(blocks, simulation);
    }
    
    public bool IsInitBlock(Block block, Simulation simulation)
    {
        return _dispatcherFinder.IsInitBlock(block, simulation);
    }

    public List<string> GetLeftDispatcherOperandRegisterNames()
    {
        return _dispatcherFinder.GetDispatcherOperandRegisterNames();
    }

    public List<string> GetRightDispatcherOperandRegisterNames()
    {
        return _dispatcherFinder.GetRightCompareRegs();
    }


    public bool IsDispatcherBlock(Block link, Simulation simulation)
    {   
        return _dispatcherFinder.IsDispatcherBlock(link,simulation);
    }

    public bool IsRealBlock(Block block, Simulation simulation)
    {   
        return true;
    }

    public bool IsRealBlockWithDispatchNextBlock(Block block, Simulation simulation)
    {
        return _dispatcherFinder.IsRealBlockWithDispatchNextBlock(block, simulation);
    }

    public bool IsCSELOperandDispatchRegister(Instruction instruction, Simulation simulation)
    {
        return _dispatcherFinder.IsCselOperandDispatchRegister(instruction, simulation);
    }

    public Config GetConfig()
    {
        return _config;
    }
}