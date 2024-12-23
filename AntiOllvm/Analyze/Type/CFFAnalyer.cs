using AntiOllvm.analyze;
using AntiOllvm.entity;
using AntiOllvm.Extension;
using AntiOllvm.Helper;
using AntiOllvm.Logging;

namespace AntiOllvm.Analyze.Type;

/**
 * Control Flow Flattening Analyer
 */
public class CFFAnalyer : IAnalyze
{
    private Block _findMain = null;

    private List<Block> _multiChildMainBlocks;
    private List<string> _childOperandRegisterNames = new();

    private Config _config;

    public CFFAnalyer(Config config)
    {
        _config = config;
    }

    private bool IsLinkToChildMain(Block block)
    {
        foreach (var VARIABLE in _multiChildMainBlocks)
        {
            if (block.Equals(VARIABLE))
            {
                return true;
            }
        }

        return false;
    }

    public void InitAfterRegisterAssign(RegisterContext context, Block main, List<Block> allBlocks,
        Simulation simulation)
    {
        if (_config.force_no_child_main)
        {
            return;
        }

        _multiChildMainBlocks = MainDispatchFinder.FindMultiMainDispatcher(context, main, allBlocks, simulation);
        if (_multiChildMainBlocks.Count > 0)
        {
            foreach (var block in _multiChildMainBlocks)
            {
                OutLogger.WarnNewline(" Find Child Main Dispatcher ! " + block.start_address +
                                      " if this not right you should Run with -force_no_child_main");
                foreach (var instruction in block.instructions)
                {
                    if (instruction.Opcode() != OpCode.CMP) continue;
                    var regName = instruction.Operands()[0].registerName;
                    if (!_childOperandRegisterNames.Contains(regName))
                    {
                        _childOperandRegisterNames.Add(regName);
                    }
                }
            }
        }
    }

    public bool IsMainDispatcher(Block block, RegisterContext context, List<Block> allBlocks, Simulation simulation)
    {
        if (_findMain == null)
        {
            _findMain = MainDispatchFinder.SmartFindMainDispatcher(block, context, allBlocks, simulation);
            if (_findMain == null)
            {
                throw new Exception(" can't find main dispatcher you should find in other way");
            }

            Logger.InfoNewline(" Find Main Dispatcher : " + _findMain.start_address);
        }

        return block.start_address == _findMain.start_address;
    }


    /**
     *
     */
    public bool IsChildDispatcher(Block curBlock, Block mainBlock, RegisterContext registerContext)
    {
        var isChild1 = ChildDispatchFinder.IsChildDispatch1(curBlock, mainBlock, registerContext);
        if (isChild1)
        {
            return true;
        }

        var isChild2 = ChildDispatchFinder.IsChildDispatch2(curBlock, mainBlock, registerContext);
        if (isChild2)
        {
            return true;
        }

        if (_config.force_no_child_main)
        {
            return false;
        }

        if (_multiChildMainBlocks.Count == 0)
        {
            return false;
        }

        // Child main dispatcher is also a child dispatcher
        if (_multiChildMainBlocks.Contains(curBlock))
        {
            // Logger.InfoNewline(" Find Child Main Dispatcher \n" + curBlock);
            return true;
        }

        //Child also have child dispatcher ... 
        var b = ChildDispatchFinder.IsChildMainChildDispatch(curBlock, registerContext, _childOperandRegisterNames,
            _multiChildMainBlocks);
        if (b)
        {
            return true;
        }

        return false;
    }

    public bool IsRealBlock(Block block, Block mainBlock, RegisterContext context)
    {
        return true;
    }

    /**
     * Return real block has child block
     */
    public bool IsRealBlockWithDispatchNextBlock(Block block, Block mainDispatcher, RegisterContext regContext,
        Simulation simulation)
    {
        var mainRegisterName = mainDispatcher.GetMainDispatchOperandRegisterName();
        foreach (var instruction in block.instructions)
        {
            if (instruction.Opcode() == OpCode.B &&
                instruction.GetRelativeAddress() == mainDispatcher.GetStartAddress())
            {
                return true;
            }
        }

        // loc_15E604
        // LDR             X9, [SP,#0x2D0+var_2B0]
        // ADRP            X8, #qword_7289B8@PAGE
        // LDR             X8, [X8,#qword_7289B8@PAGEOFF]
        // STR             X9, [SP,#0x2D0+var_260]
        // LDR             X9, [SP,#0x2D0+var_2A8]
        // STR             X8, [SP,#0x2D0+var_238]
        // MOV             W8, #0x561D9EF8
        // STP             X19, X9, [SP,#0x2D0+var_270]
        foreach (var instruction in block.instructions)
        {
            if (instruction.Opcode() == OpCode.MOV || instruction.Opcode() == OpCode.MOVK)
            {
                var second = instruction.Operands()[1];

                if (instruction.Operands()[0].registerName == mainRegisterName &&
                    second.kind == Arm64OperandKind.Immediate)
                {
                    return true;
                }
            }
        }

        if (_config.force_no_child_main)
        {
            return false;
        }
        // if this function have two main dispatcher  we should  fix
        //it's like this case 
        //Main
        //MOVK            W19, #0x5F54,LSL#16
        // MOVK            W4, #0x5714,LSL#16
        // MOVK            W5, #0x4F55,LSL#16
        // MOVK            W23, #0x43E7,LSL#16
        // MOVK            W25, #0x5382,LSL#16
        // MOVK            W27, #0x5604,LSL#16
        // MOVK            W10, #0xE76F,LSL#16
        // loc_17F59C
        // CMP             W10, W28
        // B.LE            loc_17F6B4
        //Child Main

        //MOVK            W5, #0x4F55,LSL#16
        // MOVK            W4, #0x5714,LSL#16
        // MOVK            W3, #0x2C22,LSL#16
        // MOVK            W2, #0x90E,LSL#16
        // MOVK            W1, #0x67DE,LSL#16
        // MOVK            W17, #0xF23F,LSL#16
        // MOVK            W16, #0xD34D,LSL#16
        // MOVK            W15, #0xBCC5,LSL#16
        // MOVK            W14, #0x5938,LSL#16
        // MOVK            W13, #0xE76F,LSL#16
        // MOVK            W8, #0x4189,LSL#16
        //loc_17F5E0
        // CMP             W8, W24
        // B.GT            loc_17F61C

        // this case has two main dispatcher  we need let child main dispatcher sync MOV or MOVK ins  so we need return true
        if (_multiChildMainBlocks.Count == 0)
        {
            return false;
        }

        var link = block.GetLinkedBlocks(simulation);
        if (link.Count == 0 || link.Count == 2)
        {
            return false;
        }

        if (IsLinkToChildMain(link[0]))
        {
            return true;
        }

        return false;
    }

    public bool IsOperandDispatchRegister(Instruction instruction, Block mainDispatcher, RegisterContext regContext)
    {
        var mainCompareName = mainDispatcher.GetMainDispatchOperandRegisterName();
        var first = instruction.Operands()[0].registerName;
        var second = instruction.Operands()[1].registerName;
        var third = instruction.Operands()[2].registerName;
        if (mainCompareName == first)
        {
            var secondReg = regContext.GetRegister(second);
            var thirdReg = regContext.GetRegister(third);
            if (secondReg.GetLongValue() != 0 && thirdReg.GetLongValue() != 0)
            {
                // Logger.InfoNewline("Find CFF_CSEL_Expression " + instruction);
                return true;
            }
        }

        return false;
    }
}