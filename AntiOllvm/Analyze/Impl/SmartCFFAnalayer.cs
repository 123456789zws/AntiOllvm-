using AntiOllvm.analyze;
using AntiOllvm.entity;
using AntiOllvm.Helper;

namespace AntiOllvm.Analyze.Type;

public class SmartCffAnalayer : IAnalyze
{
    private Config _config;
    private SmartDispatcherFinder _dispatcherFinder;
    
    public SmartCffAnalayer(Config config)
    {
        _config = config;
        _dispatcherFinder=new SmartDispatcherFinder();
    }
    
    public void Init(List<Block> blocks,Simulation simulation)
    {
        _dispatcherFinder.Init(blocks,simulation);
    }

    public List<string> GetDispatcherOperandRegisterNames()
    {
        return _dispatcherFinder.GetDispatcherOperandRegisterNames();
    }

    public Instruction IsInitRegisterBlockHasCSEL(Block initBlock, Block mainDispatcher, Simulation simulation,
        RegisterContext regContext)
    {
        return _dispatcherFinder.IsInitRegisterBlockHasCSEL(initBlock,mainDispatcher,simulation,regContext);
    }

    public void InitAfterRegisterAssign(RegisterContext context, Block mainDispatcher, List<Block> allBlocks, Simulation simulation)
    {
        
    }

    public bool IsMainDispatcher(Block block, RegisterContext context, List<Block> allBlocks, Simulation simulation)
    {
        return _dispatcherFinder.IsMainDispatcher(block,context,allBlocks,simulation);
    }

    public bool IsChildDispatcher(Block curBlock, Block mainBlock, RegisterContext registerContext)
    {
        return _dispatcherFinder.IsChildDispatcher(curBlock,registerContext);
    }

    public bool IsRealBlock(Block block, Block mainBlock, RegisterContext context)
    {

        return true;
    }

    public bool IsRealBlockWithDispatchNextBlock(Block block, Block mainDispatcher, RegisterContext regContext,
        Simulation simulation)
    {
       return _dispatcherFinder.IsRealBlockWithDispatchNextBlock(block,regContext,simulation);
    }

    public bool IsCSELOperandDispatchRegister(Instruction instruction, RegisterContext regContext)
    {
        return _dispatcherFinder.IsCselOperandDispatchRegister(instruction,regContext);
    }

  
}