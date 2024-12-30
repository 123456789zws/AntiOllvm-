using System.Text;
using AntiOllvm.Analyze;
using AntiOllvm.entity;
using AntiOllvm.Helper;
using AntiOllvm.Logging;

namespace AntiOllvm.Extension;

public static class BlockExtension
{
    public static List<Block> GetLinkedBlocks(this Block block, Simulation simulation)
    {
        var linkedBlocks = new List<Block>();
        foreach (var address in block.LinkBlocks)
        {
            linkedBlocks.Add(simulation.FindBlockByAddress(address));
        }

        return linkedBlocks;
    }

    public static string GetMainDispatchOperandRegisterName(this Block block)
    {
        foreach (var instruction in block.instructions)
        {
            switch (instruction.Opcode())
            {
                case OpCode.CMP:
                {
                    var operand = instruction.Operands()[0];
                    return operand.registerName;
                }
            }
        }

        return "";
    }

    /**
     *  find block is end to mainDispatch
     */
    public static bool IsEndJumpToMainDispatch(this Block block, Block mainDispath)
    {
        var ins = block.instructions[^1];
        switch (ins.Opcode())
        {
            case OpCode.B:
            {
                if (ins.Operands()[0].pcRelativeValue == mainDispath.GetStartAddress())
                {
                    return true;
                }

                return false;
            }
        }

        return false;
    }

    /**
     *  Get Control Flow Flattening CESL EXP
     */
    public static Instruction GetCFF_CSEL_Expression(this Block block, Block mainDispatch, RegisterContext regContext)
    {
        foreach (var instruction in block.instructions)
        {
            switch (instruction.Opcode())
            {
                case OpCode.CSEL:
                {
                    var mainCompareName = mainDispatch.GetMainDispatchOperandRegisterName();
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
                            return instruction;
                        }
                    }

                    break;
                }
            }
        }

        return null!;
    }

    public static bool HasCSELOpCode(this Block block)
    {
        foreach (var instruction in block.instructions)
        {
            switch (instruction.Opcode())
            {
                case OpCode.CSEL:
                {
                    return true;
                }
            }
        }

        return false;
    }


    private static bool CheckRealBlockUseNextBlockJumpByOneIns(this Block block, Simulation simulation)
    {
        if (block.instructions.Count == 1)
        {
            //loc_17F750
            // STR             XZR, [SP,#0xF0+var_C8] 
            //Current Block is only one Ins but not B Ins it's use next Block to Jump
            var ins = block.instructions[0];
            if (ins.Opcode() != OpCode.B)
            {
                var link = block.GetLinkedBlocks(simulation)[0];
                if (simulation.IsDispatcherBlock(link))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsJumpToDispatcherButNotWithBIns(this Block block, Simulation simulation)
    {
        var ins = block.instructions[^1];
        if (ins.Opcode() != OpCode.B)
        {
            return true;
        }

        return false;
    }

    private static bool IsJumpToDispatcher(this Block block, Simulation simulation)
    {
        var links = block.GetLinkedBlocks(simulation);
        if (links.Count != 1)
        {
            return false;
        }

        if (simulation.IsDispatcherBlock(links[0]))
        {
            return true;
        }

        return false;
    }


    private static bool CanFixByChangeLocation(this Block block, Simulation simulation)
    {
        var lastIns = block.instructions[^1];
        if (lastIns.Opcode() != OpCode.MOV && lastIns.Opcode() != OpCode.MOVK)
        {
            var preIns = block.instructions[^2];
            if (preIns.Opcode() == OpCode.MOV || preIns.Opcode() == OpCode.MOVK)
            {
                return true;
            }
        }

        return false;
    }

    private static void FixJumpToDispatchButNotBIns(this Block block, Simulation simulation)
    {
        //there have many case
        //Check next is Dispatcher? 
        var lastDispatcherIns = block.IsEnoughFixBySelf(simulation);
        var insIndex = block.instructions.IndexOf(lastDispatcherIns);
        if (lastDispatcherIns != null)
        {
            if (insIndex + 1 == block.instructions.Count)
            {
                if (lastDispatcherIns.InstructionSize == 8)
                {
                    var nop = Instruction.CreateNOP($"0x{lastDispatcherIns.GetAddress() + 4:X}");
                    //Add to after fixIns 
                    block.instructions.Insert(insIndex + 1, nop);
                }

                lastDispatcherIns.SetFixMachineCode(AssemBuildHelper.BuildJump(lastDispatcherIns.FormatOpcode(OpCode.B),
                    block.RealChilds[0].GetStartAddress()));
                Logger.WarnNewline(" FixJumpToDispatchButNotBIns  when mov or movk is last  " + block);
                return;
            }

            Logger.RedNewline(" FixJumpToDispatchButNotBIns  in change ins location !!!   " + block);
            var ins = block.instructions[insIndex];
            for (int i = 0; i < block.instructions.Count; i++)
            {
                var item = block.instructions[i];

                if (i > insIndex)
                {
                    item.address = $"0x{(item.GetAddress() - ins.InstructionSize).ToString("X")}";
                    item.fixmachine_byteCode = item.machine_code;
                }
            }

            //Remove dispatch instruction
            block.instructions.RemoveAt(insIndex);
            var lastIns = block.instructions[^1];

            //Add B instruction to Last instruction
            ins.SetFixMachineCode(AssemBuildHelper.BuildJump(ins.FormatOpcode(OpCode.B),
                block.RealChilds[0].GetStartAddress()));
            ins.operands_str = $"0x{block.RealChilds[0].GetStartAddress().ToString("X")}";
            ins.mnemonic = ins.FormatOpcode(OpCode.B);
            ins.address = $"0x{(lastIns.GetAddress() + 4).ToString("X")}";
            block.instructions.Add(ins);
            //Fix Address
            if (ins.InstructionSize == 8)
            {
                block.instructions.Add(Instruction.CreateNOP($"0x{ins.GetAddress() + 4:X}"));
            }

            return;
        }

        if (CanFixByChangeLocation(block, simulation))
        {
            Logger.WarnNewline("FixJumpToDispatchButNotBIns  CanFixByChangeLocation  " + block);
            var preIns = block.instructions[^2];
            var lastIns = block.instructions[^1];
            block.instructions.Remove(preIns);
            var pre_addr = preIns.address;
            var last_addr=lastIns.address;
            lastIns.address = pre_addr;
            preIns.address = last_addr;
            preIns.fixmachine_code=AssemBuildHelper.BuildJump(preIns.FormatOpcode(OpCode.B),
                block.RealChilds[0].GetStartAddress());
            block.instructions.Insert(block.instructions.Count,preIns);
            Logger.WarnNewline("FixJumpToDispatchButNotBIns   FixByChangeLocation "+block);
            return;
        }

        var nextBlock = block.GetLinkedBlocks(simulation)[0];
        Logger.WarnNewline("use next Dispatcher to Jump " + block.start_address);
        //Fix
        var firstIns = nextBlock.instructions[0];
        if (!string.IsNullOrEmpty(firstIns.fixmachine_code))
        {
            throw new Exception(" this ins is fixed can't fix again !!!");
        }

        if (firstIns.InstructionSize == 8)
        {
            var nop = Instruction.CreateNOP($"0x{firstIns.GetAddress() + 4:X}");
            //Add to after first Ins
            nextBlock.instructions.Insert(1, nop);
        }

        firstIns.SetFixMachineCode(AssemBuildHelper.BuildJump(firstIns.FormatOpcode(OpCode.B),
            block.RealChilds[0].GetStartAddress()));
        //NOP Other 
        foreach (var instruction in nextBlock.instructions)
        {
            if (string.IsNullOrEmpty(instruction.fixmachine_code))
            {
                instruction.SetFixMachineCode("NOP");
            }
        }
        return;

        throw new Exception(" FixJumpToDispatchButNotBIns  Next Block is not Dispatcher  can't fix !!! " + block);
    }

    private static void FixMachineCodeByCFF_CSELBlock(this Block block, Simulation simulation)
    {
        var index = block.instructions.IndexOf(block.CFF_CSEL);
        var lastIns = block.instructions[^1];
        if (index + 1 == block.instructions.Count - 1 && lastIns.IsJumpToDispatcher(simulation))
        {
            //CSEL            W8, W10, W8, EQ
            // B               loc_15E510
            //End like this case
            FixCSEL(block, block.CFF_CSEL);
            return;
        }

        if (index + 2 == block.instructions.Count - 1 && lastIns.IsJumpToDispatcher(simulation))
        {
            // CSEL            W8, W22, W21, LT
            // MOVK            W10, #0x186A,LSL#16
            // B               loc_15E510
            var movIns = block.instructions[index + 1];
            if (movIns.Opcode() == OpCode.MOVK)
            {
                FixCSEL(block, block.CFF_CSEL);
                //NOP End Ins
                lastIns.SetFixMachineCode("NOP");
                Logger.WarnNewline("FixMachineCodeByCFF_CSELBlock  with MOVK Dispatcher \n" + block);
                return;
            }
        }

        if (index + 2 == block.instructions.Count - 1)
        {
            //CSEL            W8, W12, W19, EQ
            // MOVK            W9, #0x94FC,LSL#16
            // STR             W8, [SP,#0x330+var_2AC]
            var movIns = block.instructions[index + 1];
            if (movIns.Opcode() == OpCode.MOVK)
            {
                //the last is not B ins we need change this location first
                var lastInsIndex = block.instructions.Count - 1;
                int offset = (lastInsIndex - index) * 4;
                lastIns.address = $"0x{(lastIns.GetAddress() - offset).ToString("X")}";

                block.instructions.RemoveAt(lastInsIndex);
                block.instructions.Insert(index, lastIns);
                block.CFF_CSEL.address = $"0x{(block.CFF_CSEL.GetAddress() + 4).ToString("X")}";
                movIns.address = $"0x{(movIns.GetAddress() + 4).ToString("X")}";
                // Logger.WarnNewline("FixMachineCodeByCFF_CSELBlock  with MOVK Dispatcher \n" + block);
                FixCSEL(block, block.CFF_CSEL);

                return;
            }
        }

        Logger.RedNewline(" it's unKnow FixMachineCodeByCFF_CSELBlock  \n" + block.start_address
                                                                           + "CSEL Index " + index + " Count " +
                                                                           block.instructions.Count);
        throw new Exception(" Fix CSEL Not Impl in  " + block.start_address);
    }

    private static void FixMachineCodeByDispatcherNextBlock(this Block block, Simulation simulation)
    {
        //Check is only one Ins but not B Ins it's use next Block to Jump 
        if (CheckRealBlockUseNextBlockJumpByOneIns(block, simulation))
        {
            //Get Next Block
            var nextBlock = block.GetLinkedBlocks(simulation)[0];
            var firstIns = nextBlock.instructions[0];
            if (firstIns.InstructionSize == 8)
            {
                var nop = Instruction.CreateNOP($"0x{firstIns.GetAddress() + 4:X}");
                //Add to after first Ins
                nextBlock.instructions.Insert(1, nop);
            }

            firstIns.SetFixMachineCode(AssemBuildHelper.BuildJump(firstIns.FormatOpcode(OpCode.B),
                block.RealChilds[0].GetStartAddress()));
            Logger.WarnNewline("Fix RealBlockUseNextBlockJumpByOneIns  \n" + block);
            return;
        }

        if (IsJumpToDispatcherButNotWithBIns(block, simulation))
        {
            //loc_15E604
            // LDR             X9, [SP,#0x2D0+var_2B0]
            // ADRP            X8, #qword_7289B8@PAGE
            // LDR             X8, [X8,#qword_7289B8@PAGEOFF]
            // STR             X9, [SP,#0x2D0+var_260]
            // LDR             X9, [SP,#0x2D0+var_2A8]
            // STR             X8, [SP,#0x2D0+var_238]
            // MOV             W8, #0x561D9EF8
            // STP             X19, X9, [SP,#0x2D0+var_270]
            // loc_15E628
            // CMP             W8, W23
            // B.GT            loc_15E6C0
            Logger.WarnNewline("IsJumpToDispatcherButNotWithBIns  FixMachineCode  " + block.start_address);
            FixJumpToDispatchButNotBIns(block, simulation);
            return;
        }

        //real block is has B ins and jump to dispatcher
        var lastIns = block.instructions[^1];
        if (lastIns.IsJumpToDispatcher(simulation))
        {
            lastIns.SetFixMachineCode(AssemBuildHelper.BuildJump(lastIns.FormatOpcode(OpCode.B),
                block.RealChilds[0].GetStartAddress()));
            return;
        }

        throw new Exception(" not Impl " + block.start_address);
    }

    public static void FixMachineCode(this Block block, Simulation simulation)
    {
        block.isFix = true;
        Logger.InfoNewline("Start Fix RealBlock " + block);
        if (block.HasCFF_CSEL())
        {
            FixMachineCodeByCFF_CSELBlock(block, simulation);
            return;
        }

        var isDispatcher = IsJumpToDispatcher(block, simulation);
        Logger.RedNewline(" FixMachineCode  Dont have CFF_CSEL  IsJumpToDispatcher " +
                          isDispatcher + "  block is " + block.start_address);
        if (isDispatcher)
        {
            FixMachineCodeByDispatcherNextBlock(block, simulation);
            return;
        }

        var links = block.GetLinkedBlocks(simulation);
        if (links.Count == 0 || links.Count == 2)
        {
            Logger.WarnNewline(" it's don't need fix block !!" + block);
            return;
        }

        var link = links[0];
        if (!simulation.IsDispatcherBlock(link))
        {
            Logger.WarnNewline(" this block link next is RealBlock it don't need fix ! " + block);
            return;
        }

        Logger.RedNewline(" Unknow FixMachineCode  \n" + block);
        throw new Exception("not Impl " + block.start_address);
    }


    private static void FixCSEL(Block block, Instruction csel)
    {
        Logger.WarnNewline("Fix CSEL  \n" + block);
        var cselIndex = block.FindIndex(csel);
        var opcode = csel.GetCSELOpCodeFix();
        var matchBlockAddress = block.RealChilds[0].GetStartAddress();
        var JumpIns = AssemBuildHelper.BuildJump(csel.FormatOpcode(opcode), matchBlockAddress);
        csel.SetFixMachineCode(JumpIns);
        var nextIns = block.instructions[cselIndex + 1];
        var notMatchBlockAddress = block.RealChilds[1].GetStartAddress();
        var notMatchJumpIns = AssemBuildHelper.BuildJump(nextIns.FormatOpcode(OpCode.B), notMatchBlockAddress);
        nextIns.SetFixMachineCode(notMatchJumpIns);
    }

    public static int FindIndex(this Block block, Instruction instruction)
    {
        for (int i = 0; i < block.instructions.Count; i++)
        {
            var item = block.instructions[i];
            if (instruction.GetAddress() == item.GetAddress())
            {
                return i;
            }
        }

        return -1;
    }

    public static bool IsChangeDispatchRegisterInRealBlock(this Block block, Block mainDispatcher)
    {
        var mainRegisterName = mainDispatcher.GetMainDispatchOperandRegisterName();
        foreach (var instruction in block.instructions)
        {
            if (instruction.Opcode() == OpCode.MOVK)
            {
                var second = instruction.Operands()[1];

                if (instruction.Operands()[0].registerName == mainRegisterName &&
                    second.kind == Arm64OperandKind.Immediate)
                {
                    return true;
                }
            }

            if (instruction.Opcode() == OpCode.MOV)
            {
                var second = instruction.Operands()[1];

                if (instruction.Operands()[0].registerName == mainRegisterName &&
                    second.kind == Arm64OperandKind.Immediate)
                {
                    return true;
                }
            }
        }

        return false;
    }


    private static Instruction IsEnoughFixBySelf(this Block block, Simulation simulation)
    {
        if (block.instructions.Count > 1)
        {
            var lastIns = block.instructions[^1];
            if (lastIns.Opcode() == OpCode.MOV || lastIns.Opcode() == OpCode.MOVK)
            {
                var operand = lastIns.Operands()[0];
                if (simulation.Analyzer.GetDispatcherOperandRegisterNames().Contains(operand.registerName))
                {
                    return lastIns;
                }

                return null;
            }

            //Check the lastIns -1;
            var preIns = block.instructions[^2];
            if (preIns.Opcode() == OpCode.MOV || preIns.Opcode() == OpCode.MOVK)
            {
                var operand = preIns.Operands()[0];
                if (simulation.Analyzer.GetDispatcherOperandRegisterNames().Contains(operand.registerName))
                {
                    return preIns;
                }

                return null;
            }
        }

        return null;
    }
}