using AntiOllvm.Analyze.Impl;
using AntiOllvm.entity;
using AntiOllvm.Extension;
using AntiOllvm.Helper;
using AntiOllvm.Logging;

namespace AntiOllvm.Analyze;

public class Simulation
{
    private readonly List<Block> _blocks;
    private IAnalyze _analyzer;
    private readonly RegisterContext _regContext;
    private Instruction _lastCompareIns;
    private Block _initBlock;
    public RegisterContext RegContext => _regContext;
    public IAnalyze Analyzer => _analyzer;
    private string _outJsonPath;
    private IDACFG _idacfg;


    private List<Block> _childDispatcherBlocks = new List<Block>();

    private List<Block> _realBlocks = new List<Block>();


    public Simulation(string json, string outJsonPath)
    {
        _outJsonPath = outJsonPath;
        _idacfg = JsonHelper.Format<IDACFG>(json);
        _blocks = _idacfg.cfg;
        _regContext = new RegisterContext();
        Logger.InfoNewline("Simulation initialized with " + _blocks.Count + " blocks");
    }

    public void SetAnalyze(IAnalyze iAnalyze)
    {
        _analyzer = iAnalyze;
    }

    public void Run()
    {
        _analyzer.Init(_blocks, this);
        //get the main entry block
        foreach (var block in _blocks)
        {
            if (_analyzer.IsInitBlock(block, this))
            {
                _initBlock = block;
                Logger.RedNewline("Init block found  " + block.start_address);
                break;
            }
        }

        if (_initBlock == null)
        {
            throw new Exception("Init block not found you should implement IsInitBlock method and find it ");
        }

        ReBuildCFGBlocks();
    }

    private void ReBuildCFGBlocks()
    {
        var block = FindRealBlock(_initBlock);
        if (block == null)
        {
            Logger.RedNewline("Init block not found in real blocks");
            return;
        }

        Logger.RedNewline("=========================================================\n" +
                          "=========================================================");
        Logger.InfoNewline("Start Fix ReadBlock Count is  " + _realBlocks.Count);
        //Order by Address 
        _realBlocks = _realBlocks.OrderBy(x => x.start_address).ToList();
        
    
        foreach (var realBlock in _realBlocks)
        {
            if (realBlock.isFix)
            {
                continue;
            }

            realBlock.FixMachineCode(this);
          
        }
        
        List<Instruction> fixInstructions = new List<Instruction>();
        FixDispatcher(fixInstructions);
        Logger.InfoNewline("Child Dispatcher Count " + _childDispatcherBlocks.Count
                                                     + " RealBlock Fix Start " + fixInstructions.Count);

        foreach (var realBlock in _realBlocks)
        {
            foreach (var instruction in realBlock.instructions)
            {
                if (!string.IsNullOrEmpty(instruction.fixmachine_code))
                {
                    if (!fixInstructions.Contains(instruction))
                    {
                        fixInstructions.Add(instruction);
                    }
                }

                if (!string.IsNullOrEmpty(instruction.fixmachine_byteCode))
                {
                    if (!fixInstructions.Contains(instruction))
                    {
                        fixInstructions.Add(instruction);
                    }
                }
            }
        }

        File.WriteAllText(_outJsonPath, JsonHelper.ToString(fixInstructions));

        OutLogger.InfoNewline("All Instruction is Fix Done Count is " + fixInstructions.Count);
        OutLogger.InfoNewline("FixJson OutPath is " + _outJsonPath);
    }

    private void FixDispatcher(List<Instruction> fixInstructions)
    {
        foreach (var block in _childDispatcherBlocks)
        {
            foreach (var instruction in block.instructions)
            {
               
                if (string.IsNullOrEmpty(instruction.fixmachine_code))
                {
                    if (instruction.InstructionSize==8)
                    {
                        instruction.SetFixMachineCode("NOP");
                        var nop = Instruction.CreateNOP($"0x{instruction.GetAddress() + 4:X}");
                        fixInstructions.Add(instruction);
                        fixInstructions.Add(nop);
                    }
                    else
                    {
                        instruction.SetFixMachineCode("NOP");
                        fixInstructions.Add(instruction);
                    }
                   
                }
                else
                {
                    fixInstructions.Add(instruction);
                }
            }
        }
    }

    private void AssignRegisterByInstruction(Instruction instruction)
    {
        switch (instruction.mnemonic)
        {
            case "MOV":
            {
                //Assign register
                var left = instruction.Operands()[0];
                var right = instruction.Operands()[1];
                if (left.kind == Arm64OperandKind.Register && right.kind == Arm64OperandKind.Immediate)
                {
                    //Assign immediate value to register
                    var register = _regContext.GetRegister(left.registerName);
                    var imm = right.immediateValue;
                    register.SetLongValue(imm);
                    Logger.RedNewline($"AssignRegisterByInstruction MOV {left.registerName} = {imm} ({imm:X})");
                }
            }
                break;
            case "MOVK":
            {
                var dest = instruction.Operands()[0];
                var imm = instruction.Operands()[1].immediateValue;
                var shift = instruction.Operands()[2].shiftType;
                var reg = _regContext.GetRegister(dest.registerName);
                var v = MathHelper.CalculateMOVK(reg.GetLongValue(), imm, shift, instruction.Operands()[2].shiftValue);
                reg.SetLongValue(v);
                Logger.InfoNewline($"AssignRegisterByInstruction MOVK {dest.registerName} = {imm} ({imm:X})");
                break;
            }
        }
    }

    private Block RunDispatcherBlock(Block block)
    {
        foreach (var instruction in block.instructions)
        {
            switch (instruction.Opcode())
            {
                case OpCode.MOV:
                {
                    //Runnning MOV instruction in Dispatch we need sync this
                    AssignRegisterByInstruction(instruction);
                    Logger.InfoNewline("MOV " + instruction.Operands()[0].registerName + " = " +
                                       instruction.Operands()[1].immediateValue + " in DispatchBlock ============");
                    break;
                }
                case OpCode.B:
                {
                    var addr = instruction.GetRelativeAddress();
                    var nextBlock = FindBlockByAddress(addr);
                    if (block.IsChildBlock(nextBlock))
                    {
                        return nextBlock;
                    }

                    throw new Exception(" B " + instruction + " is not in " + block);
                }
                case OpCode.CMP:
                {
                    var left = instruction.Operands()[0];
                    var right = instruction.Operands()[1];
                    _regContext.Compare(left.registerName, right.registerName);
                    _lastCompareIns = instruction;
                    break;
                }
                case OpCode.B_NE:
                case OpCode.B_EQ:
                case OpCode.B_GT:
                case OpCode.B_LE:
                {
                    var needJump = ConditionJumpHelper.Condition(instruction.Opcode(), _lastCompareIns, _regContext);
                    Block jumpBlock;
                    //next block is current Address +4 ;

                    jumpBlock = !needJump
                        ? FindBlockByAddress(instruction.GetAddress() + 4)
                        : FindBlockByAddress(instruction.GetRelativeAddress());
                    Logger.VerboseNewline("\n block  " + block + "\n is Jump ? " + needJump + " next block is " +
                                          jumpBlock.start_address);

                    if (block.IsChildBlock(jumpBlock))
                    {
                        return jumpBlock;
                    }

                    throw new Exception(
                        $" Analyze Error :  {jumpBlock.start_address} is not in {block.start_address} Child ");
                    // break;
                }
                default:
                {
                    throw new Exception(" not support opcode " + instruction.Opcode());
                }
            }
        }

        if (block.instructions.Count == 1)
        {
            //Fix only MOV instruction Block
            var nextBlock = block.GetLinkedBlocks(this)[0];
            return nextBlock;
        }

        return null;
    }

    private Block FindRealBlock(Block block)
    {
        if (_analyzer.IsDispatcherBlock(block, this))
        {
            Logger.InfoNewline("is Dispatcher block " + block.start_address);
            var next = RunDispatcherBlock(block);
            if (!_childDispatcherBlocks.Contains(block))
            {
                _childDispatcherBlocks.Add(block);
            }

            return FindRealBlock(next);
        }

        if (_analyzer.IsRealBlock(block, this))
        {
            block.RealChilds = GetAllChildBlocks(block);
            if (!_realBlocks.Contains(block))
            {
                _realBlocks.Add(block);
            }

            return block;
        }

        throw new Exception("is unknown block \n" + block);
    }

    private void SyncLogicInstruction(Instruction instruction)
    {
        switch (instruction.Opcode())
        {
            case OpCode.MOV:
            {
                //Assign register
                var left = instruction.Operands()[0];
                var right = instruction.Operands()[1];
                if (left.kind == Arm64OperandKind.Register && right.kind == Arm64OperandKind.Immediate)
                {
                    //Assign immediate value to register
                    var register = _regContext.GetRegister(left.registerName);
                    var imm = right.immediateValue;
                    register.SetLongValue(imm);
                    Logger.RedNewline($"Update  MOV {left.registerName} = {imm} ({imm:X})");
                }

                break;
            }
            case OpCode.MOVK:
            {
                var dest = instruction.Operands()[0];
                var imm = instruction.Operands()[1].immediateValue;
                var shift = instruction.Operands()[2].shiftType;
                var reg = _regContext.GetRegister(dest.registerName);
                var v = MathHelper.CalculateMOVK(reg.GetLongValue(), imm, shift, instruction.Operands()[2].shiftValue);
                reg.SetLongValue(v);
                Logger.InfoNewline($"Update MOVK {dest.registerName} = {imm} ({imm:X})");
                break;
            }
        }
    }

    private void SyncLogicBlock(Block block)
    {
        foreach (var instruction in block.instructions)
        {
            SyncLogicInstruction(instruction);
        }
    }

    private Instruction IsRealBlockHasCSELDispatcher(Block block)
    {
        foreach (var instruction in block.instructions)
        {
            switch (instruction.Opcode())
            {
                case OpCode.CSEL:
                {
                    if (_analyzer.IsCSELOperandDispatchRegister(instruction, this))
                    {
                        return instruction;
                    }

                    break;
                }
            }
        }

        return null;
    }

    private List<Block> GetAllChildBlocks(Block block)
    {
        if (block.isFind)
        {
            Logger.WarnNewline("block is Finding  " + block.start_address);
            return block.RealChilds;
        }

        block.isFind = true;
        var list = new List<Block>();
        var isRealBlockDispatcherNext =
            _analyzer.IsRealBlockWithDispatchNextBlock(block, this);

        if (isRealBlockDispatcherNext)
        {
            SyncLogicBlock(block);
        }
    
        var cselInstruction = IsRealBlockHasCSELDispatcher(block);
        if (cselInstruction != null)
        {
            //Mark this when we fixMachineCode 
            block.CFF_CSEL = cselInstruction;

            Logger.WarnNewline("block has CSEL Dispatcher " + block);
            _regContext.SnapshotRegisters(block.start_address);
            var needOperandRegister = cselInstruction.Operands()[0].registerName;
            var operandLeft = cselInstruction.Operands()[1].registerName;
            var left = _regContext.GetRegister(operandLeft);
            _regContext.SetRegister(needOperandRegister, left.value);
            var nextBlock = block.GetLinkedBlocks(this)[0];
            var leftBlock = FindRealBlock(nextBlock);
            Logger.WarnNewline("Block " + block.start_address + " Left  is Link To " + leftBlock.start_address);
            list.Add(leftBlock);
            _regContext.RestoreRegisters(block.start_address);
            var operandRight = cselInstruction.Operands()[2].registerName;
            var right = _regContext.GetRegister(operandRight);
            _regContext.SetRegister(needOperandRegister, right.value);
            var rightBlock = FindRealBlock(nextBlock);
            Logger.WarnNewline("Block " + block.start_address + " Right  is Link To " + rightBlock.start_address);
            list.Add(rightBlock);
            return list;
        }

        if (isRealBlockDispatcherNext)
        {
            Logger.RedNewline("Real Block Dispatcher Next " + block);
            var linkedBlocks = block.GetLinkedBlocks(this);
            if (linkedBlocks.Count != 1)
            {
                throw new Exception("Real Block Dispatcher Next block count is not 1");
            }

            var nextBlock = FindRealBlock(linkedBlocks[0]);
            list.Add(nextBlock);
            return list;
        }

        Logger.WarnNewline("Real Block  and not dispatcher next" + block);
        //But we need Check next is dispatcher block?
        var links = block.GetLinkedBlocks(this);
        if (links.Count == 0)
        {
            return list;
        }

        Logger.WarnNewline("Real Block  and not dispatcher next " + block);
        if (links.Count == 2)
        {
            var next = FindRealBlock(links[0]);
            list.Add(next);
            next = FindRealBlock(links[1]);
            list.Add(next);
            return list;
        }

        list.Add(FindRealBlock(links[0]));
        return list;
    }

    public Block FindBlockByAddress(long address)
    {
        foreach (var block in _blocks)
        {
            if (block.GetStartAddress() == address)
            {
                return block;
            }
        }

        return null;
    }

    public bool IsDispatcherBlock(Block link)
    {
        if (_childDispatcherBlocks.Contains(link))
        {
            return true;
        }

        return _analyzer.IsDispatcherBlock(link, this);
    }
}