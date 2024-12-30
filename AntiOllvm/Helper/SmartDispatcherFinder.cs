using AntiOllvm.Analyze;
using AntiOllvm.entity;
using AntiOllvm.Extension;
using AntiOllvm.Logging;

namespace AntiOllvm.Helper;

public class OpCodeAnalye
{
    public long address;
    public int moveCount;
    public int movkCount;
}

public class SmartDispatcherFinder
{
    private List<Block> _allBlocks;
    private Simulation _simulation;
    private Block _initBlock;
    public List<string> _leftCompareRegs = new();
    public List<string> _rightCompareRegs = new();
    private List<Block> _multiDispatcher = new();
    private List<Block> _childDispatcher = new();
    private List<Block> _initDispatcher = new();

    public bool IsCompareRegister(string reg)
    {
        if (_leftCompareRegs.Contains(reg) || _rightCompareRegs.Contains(reg))
        {
            return true;
        }

        return false;
    }
    private bool CheckDispatcherFeatureWithTwoInstruction(Block block, string regName)
    {
        if (block.instructions.Count == 2)
        {
            bool hasCmp = false;
            bool hasBCond = false;
            foreach (var instruction in block.instructions)
            {
                switch (instruction.Opcode())
                {
                    case OpCode.CMP:
                        var left = instruction.Operands()[0];
                        if (left.registerName == regName)
                        {
                            hasCmp = true;
                        }

                        break;
                    case OpCode.B_NE:
                    case OpCode.B_EQ:
                    case OpCode.B_GT:
                    case OpCode.B_LE:
                        hasBCond = true;
                        break;
                }
            }

            if (hasCmp && hasBCond)
            {
                return true;
            }
        }

        return false;
    }

    private bool CheckHasDispatcherFeature(Block block, string compareReg)
    {
        if (block.instructions.Count == 2)
        {
            return CheckDispatcherFeatureWithTwoInstruction(block, compareReg);
        }

        if (block.instructions.Count == 3)
        {
            return CheckDispatcherFeatureWith3Instruction(block, compareReg);
        }

        return false;
    }


// MOVK            W27, #0x778E,LSL#16
// CSEL            W8, W12, W19, EQ
// MOVK            W9, #0x94FC,LSL#16
// STR             W8, [SP,#0x330+var_2AC]
    /**
     * loc_1820A0
MOV             W8, W9
CMP             W9, W22
B.LE            loc_18211C
     */
    //Dispatcher maybe like this case
    private bool CheckDispatcherFeatureWith3Instruction(Block block, string compareReg)
    {
        bool hasMov = false;
        bool hasCmp = false;
        bool hasBCond = false;
        foreach (var instruction in block.instructions)
        {
            switch (instruction.Opcode())
            {
                case OpCode.MOV:

                    hasMov = true;

                    break;
                case OpCode.CMP:
                    var left = instruction.Operands()[0];
                    if (left.registerName == compareReg)
                    {
                        hasCmp = true;
                    }

                    break;
                case OpCode.B_NE:
                case OpCode.B_EQ:
                case OpCode.B_GT:
                case OpCode.B_LE:
                    hasBCond = true;
                    break;
            }
        }

        if (hasMov && hasCmp && hasBCond)
        {
            return true;
        }

        return false;
    }


    private void BuildDispatcher()
    {
        _multiDispatcher = new List<Block>();

        Dictionary<long, OpCodeAnalye> movkCount = new Dictionary<long, OpCodeAnalye>();
        foreach (var item in _allBlocks)
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
        //Find Mov >=MOVK and MOVK>1 mov >1 

        var findDispatchers = movkCount.Where(x => x.Value.moveCount >= x.Value.movkCount
                                                   && x.Value is { movkCount: > 1, moveCount: > 1 })
            .ToDictionary(x => x.Key, x => x.Value);
        foreach (var VARIABLE in findDispatchers)
        {
            var initDispatcher = _simulation.FindBlockByAddress(VARIABLE.Key);
            //Get the last ins is B instruction? 
            var lastIns = initDispatcher.instructions[^1];
            if (lastIns.Opcode() == OpCode.B)
            {
                var dispatcher = _simulation.FindBlockByAddress(lastIns.GetRelativeAddress());
                //Find the last MOVK instruction
                var lastMovk = initDispatcher.instructions.FindLast(x => x.Opcode() == OpCode.MOVK);
                var compareReg = lastMovk?.Operands()[0].registerName;
                if (CheckHasDispatcherFeature(dispatcher, compareReg))
                {
                    if (!_multiDispatcher.Contains(dispatcher))
                    {
                        Logger.WarnNewline("Find multi Main Dispatcher " + dispatcher.start_address
                                                                         + " and init Register is " +
                                                                         initDispatcher.start_address +
                                                                         " compareRegister is  " + compareReg);
                        _multiDispatcher.Add(dispatcher);
                    }

                    if (!_leftCompareRegs.Contains(compareReg))
                    {
                        _leftCompareRegs.Add(compareReg);
                    }

                    if (!_initDispatcher.Contains(initDispatcher))
                    {
                        _initDispatcher.Add(initDispatcher);
                    }
                   
                }
            }
            else
            {
                //Get the Link block
                var link = initDispatcher.GetLinkedBlocks(_simulation);
                if (link.Count == 1)
                {
                    //init Dispatcher only have one link block
                    var dispatcherBlock = link[0];
                    var lastMovk = initDispatcher.instructions.FindLast(x => x.Opcode() == OpCode.MOVK);
                    var compareReg = lastMovk?.Operands()[0].registerName;
                    if (CheckHasDispatcherFeature(dispatcherBlock, compareReg))
                    {
                        if (!_multiDispatcher.Contains(dispatcherBlock))
                        {
                            Logger.WarnNewline("Find multi Main Dispatcher " + dispatcherBlock.start_address
                                                                             + " and init Register is " +
                                                                             initDispatcher.start_address
                                                                             + " compareRegister is  " + compareReg);
                            _multiDispatcher.Add(dispatcherBlock);
                            //Init Right CompareRegister
                            
                        }

                        if (!_leftCompareRegs.Contains(compareReg))
                        {
                            _leftCompareRegs.Add(compareReg);
                        }
                        if (!_initDispatcher.Contains(initDispatcher))
                        {
                            _initDispatcher.Add(initDispatcher);
                        }
                    }
                }
                // Logger.WarnNewline("Block " + dispatcherBlock.start_address + " maybe is a init dispatcher  but not end with B instruction "+dispatcherBlock);
            }
        }

        //Order by start address
        _multiDispatcher = _multiDispatcher.OrderBy(x => x.GetStartAddress()).ToList();

        //findDispatchers order by start address
        var findDispatchersOrder =
            findDispatchers.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value).ToList();
        // var initBlock = ;

        _initBlock = _simulation.FindBlockByAddress(findDispatchersOrder[0].Key);
        //init rightCompareRegs
        var movRegs = new List<string>();
        var movkRegs = new List<string>();
        foreach (var block in _initDispatcher)
        {
            foreach (var ins in block.instructions)
            {
                if (ins.Opcode() == OpCode.MOV)
                {
                    var left = ins.Operands()[0];
                    var right = ins.Operands()[1];
                    if (right.kind == Arm64OperandKind.Immediate && right.immediateValue != 0
                        && left.registerName.StartsWith("W"))
                    {
                        if (!movRegs.Contains(left.registerName))
                        {
                            movRegs.Add(left.registerName);
                        }
                        
                    }
                }

                if (ins.Opcode() == OpCode.MOVK)
                {
                    var left = ins.Operands()[0];
                    var right = ins.Operands()[1];
                    if (right.kind == Arm64OperandKind.Immediate && right.immediateValue != 0
                        && left.registerName.StartsWith("W"))
                    {
                        if (!movkRegs.Contains(left.registerName))
                        {
                            movkRegs.Add(left.registerName);
                        }
                    }
                }
            }
        }
        
        foreach (var VARIABLE in movkRegs)
        {
            if (movRegs.Contains(VARIABLE))
            {
                if (!_rightCompareRegs.Contains(VARIABLE))
                {
                    _rightCompareRegs.Add(VARIABLE);
                }
            }
        }
        Logger.InfoNewline("Left Compare Register is " + string.Join(",", _leftCompareRegs));
        Logger.InfoNewline("Right Compare Register is " + string.Join(",", _rightCompareRegs));
    }

    public void Init(List<Block> allBlocks, Simulation simulation)
    {
        _allBlocks = allBlocks;
        _simulation = simulation;
        BuildDispatcher();
    }


    private bool CheckChildDispatcherFeature(Block block, Simulation context)
    {
        if (block.instructions.Count == 1)
        {
            return CheckChildDispatcherFeatureWith1Ins(block, context);
        }

        if (block.instructions.Count == 2)
        {
            return CheckChildDispatcherFeatureWith2Ins(block, context);
        }

        if (block.instructions.Count == 3)
        {
            return CheckChildDispatcherFeatureWith3Ins(block, context);
        }

        if (block.instructions.Count == 4)
        {
            return CheckChildDispatcherFeatureWith4Ins(block, context);
        }

        if (block.instructions.Count == 5)
        {
            return CheckChildDispatcherFeatureWith5Ins(block, context);
        }

        return false;
    }

    private bool CheckChildDispatcherFeatureWith5Ins(Block block, Simulation simulation)
    {
        // MOV             W9, #0xF97CBD0C
        // MOV             W10, #0xDFC5
        // CMP             W8, W9
        // MOVK            W10, #0x7A2B,LSL#16
        // B.NE            loc_182B74

        bool hasCmp = false;
        bool hasBCond = false;
        List<string> movRegs = new();
        foreach (var instruction in block.instructions)
        {
            switch (instruction.Opcode())
            {
                case OpCode.CMP:
                    var left = instruction.Operands()[0];
                    var right = instruction.Operands()[1];
                    if (_leftCompareRegs.Contains(left.registerName)
                        && _rightCompareRegs.Contains(right.registerName)
                        && movRegs.Contains(right.registerName))
                    {
                        //Get is immediate value
                        var leftValue = simulation.RegContext.GetRegister(left.registerName).GetLongValue();
                        var rightValue = simulation.RegContext.GetRegister(right.registerName).GetLongValue();
                        if (leftValue != 0 && rightValue != 0)
                        {
                            hasCmp = true;
                        }
                    }

                    break;
                case OpCode.MOV:
                    var first = instruction.Operands()[0];
                    var sec = instruction.Operands()[1];
                    if (_rightCompareRegs.Contains(first.registerName) && sec.kind == Arm64OperandKind.Immediate
                                                                       && sec.immediateValue != 0)
                    {
                        movRegs.Add(first.registerName);
                    }

                    break;
                case OpCode.B_NE:
                case OpCode.B_EQ:
                case OpCode.B_GT:
                case OpCode.B_LE:
                    hasBCond = true;
                    break;
            }
        }

        if (hasCmp && hasBCond)
        {
            return true;
        }


        return false;
    }

    private bool CheckChildDispatcherFeatureWith4Ins(Block block, Simulation simulation)
    {
        //MOV             W9, #0xD58FFDA8
        // CMP             W8, W9
        // MOV             W9, W8
        // B.NE            loc_1820A0
        if (this.CheckChildDispatcherFeatureWith3Ins4MoveCmpB(block, simulation))
        {
            return true;
        }

        // loc_183468
        // MOV             W9, #0xF2AF
        // CMP             W8, W19
        // MOVK            W9, #0x510C,LSL#16
        // B.EQ            loc_18330C
        if (this.CheckChildDispatcherFeatureWith4Ins4MovCmpMovkB(block, simulation))
        {
            return true;
        }

        return false;
    }

    private bool CheckChildDispatcherFeatureWith4Ins4MovCmpMovkB(Block block, Simulation simulation)
    {
        bool hasMov = false;
        bool hasCmp = false;
        bool hasBCond = false;
        string movRegName = "";
        bool isMOVKSameReg = false;
        foreach (var instruction in block.instructions)
        {
            switch (instruction.Opcode())
            {
                case OpCode.MOV:
                {
                    var left = instruction.Operands()[0];
                    var right = instruction.Operands()[1];
                    if (_leftCompareRegs.Contains(left.registerName) && right.kind == Arm64OperandKind.Immediate
                                                                     && right.immediateValue != 0)
                    {
                        hasMov = true;
                        movRegName = left.registerName;
                    }

                    break;
                }
                case OpCode.MOVK:
                {
                    var left = instruction.Operands()[0];
                    var right = instruction.Operands()[1];
                    if (hasMov && movRegName == left.registerName && right.kind == Arm64OperandKind.Immediate
                        && right.immediateValue != 0)
                    {
                        isMOVKSameReg = true;
                    }

                    break;
                }
                case OpCode.CMP:
                {
                    var left = instruction.Operands()[0];
                    if (_leftCompareRegs.Contains(left.registerName))
                    {
                        hasCmp = true;
                    }

                    break;
                }
                case OpCode.B_NE:
                case OpCode.B_EQ:
                case OpCode.B_GT:
                case OpCode.B_LE:
                    //Got Link
                    var links = block.GetLinkedBlocks(simulation);
                    if (IsDispatcherBlock(links[0], simulation) && IsDispatcherBlock(links[1], simulation))
                    {
                        hasBCond = true;
                    }

                    break;
            }
        }

        if (hasBCond && hasCmp && isMOVKSameReg)
        {
            return true;
        }

        return false;
    }

    private bool CheckChildDispatcherFeatureWith1Ins(Block block, Simulation context)
    {
        // [Block] 0x17f848
        // 0x17f848   MOV W8,#0x67DE569C
        //Fix this case
        var ins = block.instructions[0];
        if (ins.Opcode() == OpCode.MOV)
        {
            var left = ins.Operands()[0];
            var right = ins.Operands()[1];
            if (right.kind == Arm64OperandKind.Immediate && right.immediateValue != 0)
            {
                if (_leftCompareRegs.Contains(left.registerName) || _rightCompareRegs.Contains(left.registerName))
                {
                    //Got the next is Child?
                    var link = block.GetLinkedBlocks(_simulation)[0];
                    if (IsDispatcherBlock(link, context))
                    {
                        return true;
                    }
                }

                return true;
            }
        }

        return false;
    }


    private bool CheckChildDispatcherFeatureWith3Ins(Block block, Simulation context)
    {
        // MOV             W11, #0xEFF1B6F8
        // CMP             W10, W11
        // B.NE            loc_17F59C
        if (this.CheckChildDispatcherFeatureWith3Ins4MoveCmpB(block, context))
        {
            return true;
        }

        // LDR             W9, [SP,#0x330+var_2AC]
        // CMP             W8, W24
        // B.EQ            loc_1820A0
        if (this.CheckChildDispatcherFeatureWith3Ins4LDRCmpB(block, context))
        {
            return true;
        }

        if (this.CheckChildDispatcherFeatureWith3Ins4CMPMOVBCond(block, context))
        {
            return true;
        }

        return false;
    }


    private bool CheckChildDispatcherFeatureWith2Ins(Block block, Simulation simulation)
    {
        //* Find Child when this block only 2 instruction
        // * CMP W8,W9
        // * B.EQ 0X100
        if (this.CheckChildDispatcherFeatureWith2Ins4Cmp(block, simulation))
        {
            return true;
        }

        //  MOV             W10, #0x8614E721
        //  B               loc_17F59C
        //Fix this case
        if (this.CheckChildDispatcherFeatureWith2Ins4MovAndB(block, simulation))
        {
            return true;
        }

        return false;
    }

    private bool IsWriteRightCompareRegister(Block block)
    {
        bool isDispatcher = false;
        //if dont have B instruction but Movk and Mov register
        var movRegs = new List<string>();
        var movkRegs = new List<string>();

        foreach (var ins in block.instructions)
        {
            if (ins.Opcode() == OpCode.MOV)
            {
                var left = ins.Operands()[0];
                var right = ins.Operands()[1];
                if (right.kind == Arm64OperandKind.Immediate && right.immediateValue != 0)
                {
                    if (!movRegs.Contains(left.registerName))
                    {
                        movRegs.Add(left.registerName);
                    }

                    if (ins.InstructionSize == 8)
                    {
                        //ida is merge MOVK and MOV
                        movkRegs.Add(left.registerName);
                    }
                }
            }

            if (ins.Opcode() == OpCode.MOVK)
            {
                var left = ins.Operands()[0];
                var right = ins.Operands()[1];
                if (right.kind == Arm64OperandKind.Immediate && right.immediateValue != 0)
                {
                    if (!movkRegs.Contains(left.registerName))
                    {
                        movkRegs.Add(left.registerName);
                    }
                }
            }
        }

        foreach (var VARIABLE in movkRegs)
        {
            if (movRegs.Contains(VARIABLE))
            {
                if (_rightCompareRegs.Contains(VARIABLE) || _leftCompareRegs.Contains(VARIABLE))
                {
                    isDispatcher = true;
                }
            }
        }

        if (isDispatcher)
        {
            return true;
        }

        return false;
    }

    public bool IsRealBlockWithDispatchNextBlock(Block block, Simulation simulation)
    {
        bool hasBInstruction = false;
        foreach (var instruction in block.instructions)
        {
            if (instruction.Opcode() == OpCode.B)
            {
                hasBInstruction = true;
                var nextBlock = simulation.FindBlockByAddress(instruction.GetRelativeAddress());

                if (_multiDispatcher.Contains(nextBlock))
                {
                    return true;
                }

                if (IsDispatcherBlock(nextBlock, simulation))
                {
                    return true;
                }

                if (IsWriteRightCompareRegister(block))
                {
                    return true;
                }
            }
        }

        if (!hasBInstruction)
        {
            if (IsWriteRightCompareRegister(block))
            {
                return true;
            }
        }

        return false;
    }

    public bool IsCselOperandDispatchRegister(Instruction instruction, Simulation simulation)
    {
        var first = instruction.Operands()[0].registerName;
        var second = instruction.Operands()[1].registerName;
        var third = instruction.Operands()[2].registerName;

        if (_leftCompareRegs.Contains(first))
        {
            var secondReg = simulation.RegContext.GetRegister(second);
            var thirdReg = simulation.RegContext.GetRegister(third);
            if (secondReg.GetLongValue() != 0 && thirdReg.GetLongValue() != 0)
            {
                return true;
            }
        }

        return false;
    }

    public List<string> GetDispatcherOperandRegisterNames()
    {
        return _leftCompareRegs;
    }


    public bool IsInitBlock(Block block, Simulation simulation)
    {
        return block.Equals(_initBlock);
    }

    public bool IsDispatcherBlock(Block curBlock, Simulation context)
    {
        //Use cache
        if (_multiDispatcher.Contains(curBlock))
        {
            return true;
        }

        if (_childDispatcher.Contains(curBlock))
        {
            return true;
        }

        if (CheckChildDispatcherFeature(curBlock, context))
        {
            if (!_childDispatcher.Contains(curBlock))
            {
                _childDispatcher.Add(curBlock);
            }

            return true;
        }

        return false;
    }

    public List<string> GetRightCompareRegs()
    {
        return _rightCompareRegs;
    }
}