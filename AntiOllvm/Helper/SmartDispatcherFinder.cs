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
    public List<string> _multiCompareRegs = new();
    private Block mainDispatcher;
    private List<Block> _multiDispatcher = new();
    private List<Block> _childDispatcher = new();
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
                    Logger.WarnNewline("Find multi Main Dispatcher " + dispatcher.start_address
                                                                     + " and init Register is " +
                                                                     initDispatcher.start_address + " compareRegister is  " +compareReg);
                    if (!_multiDispatcher.Contains(dispatcher))
                    {
                        _multiDispatcher.Add(dispatcher);
                    }

                    if (!_multiCompareRegs.Contains(compareReg))
                    {
                        _multiCompareRegs.Add(compareReg);
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
                        Logger.WarnNewline("Find multi Main Dispatcher " + dispatcherBlock.start_address
                                                                         + " and init Register is " +
                                                                         initDispatcher.start_address
                                                                         +" compareRegister is  "+compareReg);
                        if (!_multiDispatcher.Contains(dispatcherBlock))
                        {
                            _multiDispatcher.Add(dispatcherBlock);
                        }

                        if (!_multiCompareRegs.Contains(compareReg))
                        {
                            _multiCompareRegs.Add(compareReg);
                        }
                    }
                }
                // Logger.WarnNewline("Block " + dispatcherBlock.start_address + " maybe is a init dispatcher  but not end with B instruction "+dispatcherBlock);
            }
        }
        //Order by start address
        _multiDispatcher = _multiDispatcher.OrderBy(x => x.GetStartAddress()).ToList();
        mainDispatcher = _multiDispatcher[0];
    }

    public void Init(List<Block> allBlocks, Simulation simulation)
    {
        _allBlocks = allBlocks;
        _simulation = simulation;
        BuildDispatcher();
    }

    public bool IsMainDispatcher(Block block, RegisterContext context, List<Block> allBlocks, Simulation simulation)
    {
        
        return mainDispatcher.Equals(block);
    }

    public bool IsChildDispatcher(Block curBlock, RegisterContext registerContext)
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
        if (CheckChildDispatcherFeature(curBlock, registerContext))
        {
            if (!_childDispatcher.Contains(curBlock))
            {
                _childDispatcher.Add(curBlock);
            }
            return true;
        }
        return false;
    }

    private bool CheckChildDispatcherFeature(Block block, RegisterContext context)
    {
        if (block.instructions.Count==1)
        {   
            return CheckChildDispatcherFeatureWith1Ins(block, context);
        }
        if (block.instructions.Count == 2)
        {
            return CheckChildDispatcherFeatureWith2Ins(block, context);
        }

        if (block.instructions.Count==3)
        {
            return CheckChildDispatcherFeatureWith3Ins(block, context);
        }

        return false;
    }

    private bool CheckChildDispatcherFeatureWith1Ins(Block block, RegisterContext context)
    {
        // [Block] 0x17f848
        // 0x17f848   MOV W8,#0x67DE569C
        //Fix this case
        var ins=block.instructions[0];
        if (ins.Opcode()==OpCode.MOV)
        {
            var left = ins.Operands()[0];
            var right = ins.Operands()[1];
            if (_multiCompareRegs.Contains(left.registerName) && right.kind == Arm64OperandKind.Immediate
                && right.immediateValue != 0)
            {   
                //Got the next is Child?
                var link=block.GetLinkedBlocks(_simulation)[0];
                if (IsChildDispatcher(link, context))
                {
                    return true;
                }
                return true;
            }
        }

        return false;
    }

    private bool CheckChildDispatcherFeatureWith3Ins(Block block, RegisterContext context)
    {
        // MOV             W11, #0xEFF1B6F8
        // CMP             W10, W11
        // B.NE            loc_17F59C
        if (this.CheckChildDispatcherFeatureWith3Ins4MoveCmpB( block, context))
        {
            return true;
        }
        return false;
    }

  


    private bool CheckChildDispatcherFeatureWith2Ins(Block block, RegisterContext context)
    {
        //* Find Child when this block only 2 instruction
        // * CMP W8,W9
        // * B.EQ 0X100
        if (this.CheckChildDispatcherFeatureWith2Ins4Cmp( block, context))
        {
            return true;
        }
        //  MOV             W10, #0x8614E721
        //  B               loc_17F59C
        //Fix this case
        if (this.CheckChildDispatcherFeatureWith2Ins4MovAndB( block, context))
        {
            return true;
        }
       
        return false;
    }
    
    public bool IsRealBlockWithDispatchNextBlock(Block block, RegisterContext regContext, Simulation simulation)
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
                if (IsChildDispatcher(nextBlock, regContext))
                {
                    return true;
                }
            }
        }
    
        if (!hasBInstruction)
        {
            bool isDispatcher = false;
            //if dont have B instruction but Movk and Mov register
            foreach (var instruction in block.instructions)
            {
                switch (instruction.Opcode())
                {
                    case OpCode.MOVK:
                    case OpCode.MOV:
                    {   
                        var left = instruction.Operands()[0];
                        var right = instruction.Operands()[1];
                        if (_multiCompareRegs.Contains(left.registerName)
                            && right.kind==Arm64OperandKind.Immediate && right.immediateValue != 0)
                        {
                            isDispatcher = true;
                         
                        }
                        break;
                    }
                }
            }

            if (isDispatcher)
            {
                return true;
            }
        }
        return false;
    }

    public bool IsCselOperandDispatchRegister(Instruction instruction, Block mainDispatcher, RegisterContext regContext)
    {
        var first = instruction.Operands()[0].registerName;
        var second = instruction.Operands()[1].registerName;
        var third = instruction.Operands()[2].registerName;

        if (_multiCompareRegs.Contains(first))
        {
            var secondReg = regContext.GetRegister(second);
            var thirdReg = regContext.GetRegister(third);
            if (secondReg.GetLongValue() != 0 && thirdReg.GetLongValue() != 0)
            {
                return true;
            }
        }

        return false;
    }

    public List<string> GetDispatcherOperandRegisterNames()
    {
        return _multiCompareRegs;
    }
}