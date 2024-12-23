using AntiOllvm.Analyze;
using AntiOllvm.entity;
using AntiOllvm.Extension;
using AntiOllvm.Logging;

namespace AntiOllvm.Helper;

/**
 *
 *  guess the main dispatcher
 */
public static class MainDispatchFinder
{
    private class OpCodeAnalye
    {
        public long address;
        public int moveCount;
        public int movkCount;
    }


  

    /**
     *     return true if this block has dispatcher flag
     */
    private static bool IsHaveDispatcherFlag(Block block, Block main)
    {
        bool isCMP = false;
        bool isConditionJump = false;
        foreach (var instruction in block.instructions)
        {
            if (instruction.Opcode() == OpCode.CMP)
            {
                var operandRegName = instruction.Operands()[0].registerName;
                var mainOperandRegName = main.GetMainDispatchOperandRegisterName();
                if (operandRegName != mainOperandRegName)
                {
                    isCMP = true;
                }
            }

            if (instruction.HasConditionJump())
            {
                isConditionJump = true;
            }
        }

        if (isConditionJump && isCMP)
        {
            return true;
        }

        return false;
    }

    /**
     * return Child Main Dispatcher if exist
     */
    public static List<Block> FindMultiMainDispatcher(RegisterContext context, Block main, List<Block> allBlocks,
        Simulation simulation)
    {
        var multiMain = new List<Block>();
        Dictionary<long, OpCodeAnalye> movkCount = new Dictionary<long, OpCodeAnalye>();
        foreach (var item in allBlocks)
        {
            foreach (var instruction in item.instructions)
            {
                if (instruction.Opcode() == OpCode.MOVK)
                {
                    if (movkCount.ContainsKey(item.GetStartAddress()))
                    {
                        movkCount[item.GetStartAddress()].movkCount++;
                    }
                    else
                    {
                        movkCount.Add(item.GetStartAddress(),
                            new OpCodeAnalye() { address = item.GetStartAddress(), movkCount = 1 });
                    }
                }

                if (instruction.Opcode() == OpCode.MOV)
                {
                    if (movkCount.ContainsKey(item.GetStartAddress()))
                    {
                        movkCount[item.GetStartAddress()].moveCount++;
                    }
                    else
                    {
                        movkCount.Add(item.GetStartAddress(),
                            new OpCodeAnalye() { address = item.GetStartAddress(), moveCount = 1 });
                    }
                }
            }
        }

        //Find Mov >=MOVK and MOVK!=0
        var findDispatchers = movkCount.Where(x => x.Value.moveCount >= x.Value.movkCount && x.Value.movkCount != 0)
            .ToDictionary(x => x.Key, x => x.Value);

        foreach (var VARIABLE in findDispatchers)
        {
            var dispatcherBlock = simulation.FindBlockByAddress(VARIABLE.Key);
            var links = dispatcherBlock.GetLinkedBlocks(simulation);
            if (links.Count == 0 || links.Count == 2)
            {
                continue;
            }

            var link = links[0];
            if (link.Equals(main))
            {
                continue;
            }

            // Logger.InfoNewline(" init Dispatcher is "+dispatcherBlock.start_address +" Get Dispatcher ?  "+link );
            if (IsHaveDispatcherFlag(link, main))
            {
                multiMain.Add(link);
            }
        }

        return multiMain;
    }

    /**
     *  Get MOVK instruction Block
     */
    public static Block SmartFindMainDispatcher(Block block, RegisterContext context, List<Block> allBlocks,
        Simulation simulation)
    {
        // MOVK            W13, #0xE76F,LSL#16
        // MOVK            W28, #0xE76F,LSL#16
        // MOVK            W29, #0x8614,LSL#16
        // MOVK            W26, #0x91FA,LSL#16
        // MOVK            W20, #0xCE6E,LSL#16
        // MOVK            W21, #0x5938,LSL#16
        // MOVK            W14, #0x5938,LSL#16
        // MOVK            W24, #0x43E7,LSL#16
        // MOVK            W22, #0x4F55,LSL#16
        // MOVK            W15, #0xBCC5,LSL#16
        // MOVK            W16, #0xD34D,LSL#16
        // MOVK            W17, #0xF23F,LSL#16
        // MOVK            W1, #0x67DE,LSL#16
        // MOVK            W2, #0x90E,LSL#16
        // MOVK            W3, #0x2C22,LSL#16
        // MOVK            W19, #0x5F54,LSL#16
        // MOVK            W4, #0x5714,LSL#16
        // MOVK            W5, #0x4F55,LSL#16
        // MOVK            W23, #0x43E7,LSL#16
        // MOVK            W25, #0x5382,LSL#16
        // MOVK            W27, #0x5604,LSL#16
        // MOVK            W10, #0xE76F,LSL#16
        Dictionary<long, int> movkCount = new Dictionary<long, int>();
        foreach (var item in allBlocks)
        {
            foreach (var instruction in item.instructions)
            {
                if (instruction.Opcode() == OpCode.MOVK)
                {
                    if (movkCount.ContainsKey(item.GetStartAddress()))
                    {
                        movkCount[item.GetStartAddress()]++;
                    }
                    else
                    {
                        movkCount.Add(item.GetStartAddress(), 1);
                    }
                }
            }
        }


        //Get the max Count Block 
        var maxBlock = movkCount.OrderByDescending(x => x.Value).FirstOrDefault();
        //We Find the initDispatcher block 
        var initDispatcherBlock = simulation.FindBlockByAddress(maxBlock.Key);
        //the next is the main dispatcher block
        var link = initDispatcherBlock.GetLinkedBlocks(simulation);
        if (link.Count != 1)
        {
            Logger.ErrorNewline(
                " SmartFindMainDispatcher error : can't find main dispatcher you must find in other way");
            return null;
        }

        var lastIns = initDispatcherBlock.instructions[^1];
        var nextBlock = simulation.FindBlockByAddress(lastIns.GetAddress() + 4);
        var linkBlock = link[0];
        if (nextBlock.GetStartAddress() == linkBlock.GetStartAddress())
        {
            var findMain = FindMainDispatcher(linkBlock, context, allBlocks);
            if (findMain.GetStartAddress() != linkBlock.GetStartAddress())
            {
                Logger.WarnNewline("Warning : this Funcation maybe has multi main dispatcher");
            }

            return nextBlock;
        }

        return null;
    }


    /**
     * smart guess the main dispatcher block
     */
    public static Block FindMainDispatcher(Block block, RegisterContext context, List<Block> allBlocks)
    {
        //Find Cmp instruction
        List<Instruction> cmpInstructions =
            allBlocks.SelectMany(x => x.instructions).Where(x => x.Opcode() == OpCode.CMP).ToList();

        Dictionary<string, int> registerCount = new Dictionary<string, int>();
        foreach (var instruction in cmpInstructions)
        {
            var regName = instruction.Operands()[0].registerName;
            if (!registerCount.TryAdd(regName, 1))
            {
                registerCount[regName]++;
            }
        }

        var maxRegister = registerCount.OrderByDescending(x => x.Value).FirstOrDefault();
        //Find the first block show the max register
        foreach (var item in allBlocks)
        {
            foreach (var instruction in item.instructions)
            {
                if (instruction.Opcode() == OpCode.CMP)
                {
                    if (instruction.Operands()[0].registerName == maxRegister.Key)
                    {
                        return item;
                    }
                }
            }
        }

        return null;
    }


  
}