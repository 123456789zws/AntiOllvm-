using AntiOllvm.Analyze;
using AntiOllvm.entity;

namespace AntiOllvm.analyze;

public interface IAnalyze
{   
    public void InitAfterRegisterAssign(RegisterContext context,Block mainDispatcher,List<Block> allBlocks,Simulation simulation);
    public bool IsMainDispatcher(Block block, RegisterContext context, List<Block> allBlocks, Simulation simulation);
    public bool IsChildDispatcher(Block curBlock, Block mainBlock, RegisterContext registerContext);
    public bool IsRealBlock(Block block, Block mainBlock, RegisterContext context);

    bool IsRealBlockWithDispatchNextBlock(Block block, Block mainDispatcher, RegisterContext regContext, Simulation simulation);
    bool IsOperandDispatchRegister(Instruction instruction, Block mainDispatcher, RegisterContext regContext);
}