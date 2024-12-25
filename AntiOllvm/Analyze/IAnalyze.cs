using AntiOllvm.Analyze;
using AntiOllvm.entity;

namespace AntiOllvm.analyze;

public interface IAnalyze
{   
    public void InitAfterRegisterAssign(RegisterContext context,Block mainDispatcher,List<Block> allBlocks,Simulation simulation);
    public bool IsMainDispatcher(Block block, RegisterContext context, List<Block> allBlocks, Simulation simulation);
    public bool IsChildDispatcher(Block curBlock, Block mainBlock, RegisterContext registerContext);
    public bool IsRealBlock(Block block, Block mainBlock, RegisterContext context);
    /**
     * we need the right time to sync the block
     */
    bool IsRealBlockWithDispatchNextBlock(Block block, Block mainDispatcher, RegisterContext regContext, Simulation simulation);
    bool IsCSELOperandDispatchRegister(Instruction instruction, RegisterContext regContext);
    /**
     * When framework init, this method will be called
     */
    void Init(List<Block> blocks,Simulation simulation);
    
    /**
     *  if function is have multiple dispatcher, we need to know the operand register name
     */
    List<string> GetDispatcherOperandRegisterNames();
    
    /**
 * When Main Dispatcher is found, we need to know if the init register block has CSEL
 */
    Instruction IsInitRegisterBlockHasCSEL(Block initBlock,Block mainDispatcher, Simulation simulation, RegisterContext regContext);

}