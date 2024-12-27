using AntiOllvm.entity;

namespace AntiOllvm.Analyze.Impl;

public interface IAnalyze
{   
    /**
     * When the simulation is initialized
     */
    void Init(List<Block> blocks, Simulation simulation);
    
    /**
     *  should let framework know init block ,framework will be work start from this block
     */
    bool IsInitBlock(Block block,Simulation simulation);
    
    
    List<string> GetDispatcherOperandRegisterNames();
    bool IsDispatcherBlock(Block link, Simulation simulation);
    bool IsRealBlock(Block block, Simulation simulation);
    bool IsRealBlockWithDispatchNextBlock(Block block, Simulation simulation);
    bool IsCSELOperandDispatchRegister(Instruction instruction, Simulation simulation);
    
    Config GetConfig();
}