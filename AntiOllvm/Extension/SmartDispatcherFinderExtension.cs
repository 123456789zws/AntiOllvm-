using AntiOllvm.Analyze;
using AntiOllvm.entity;
using AntiOllvm.Helper;
using AntiOllvm.Logging;

namespace AntiOllvm.Extension;

public static class SmartDispatcherFinderExtension
{
    public static bool CheckChildDispatcherFeatureWith3Ins4CMPMOVBCond(this SmartDispatcherFinder finder,
        Block block, Simulation simulation)
    {
        bool hasCmp = false;
        bool hasMov = false;
        bool hasBCond = false;
        foreach (var instruction in block.instructions)
        {
            switch (instruction.Opcode())
            {
                case OpCode.CMP:
                    var left = instruction.Operands()[0];
                    var right = instruction.Operands()[1];
                    if (finder._leftCompareRegs.Contains(left.registerName)
                        && finder._rightCompareRegs.Contains(right.registerName))
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
                    var leftMov = instruction.Operands()[0];
                    var rightMov = instruction.Operands()[1];
                    if (finder._rightCompareRegs.Contains(leftMov.registerName))
                    {
                        hasMov = true;
                    }
                    break;
                case OpCode.B_NE:
                case OpCode.B_EQ:
                case OpCode.B_GT:
                case OpCode.B_LE:
                    var nextBlock=block.GetLinkedBlocks(simulation)[0];
                    if (simulation.IsDispatcherBlock(nextBlock))
                    {
                        hasBCond = true;
                    }
                    break;
            }
        }

        if (hasCmp && hasMov && hasBCond)
        {
            return true;
        }

        return false;
    }
    public static bool CheckChildDispatcherFeatureWith3Ins4LDRCmpB(this SmartDispatcherFinder finder,
        Block block, Simulation simulation)
    {
        bool isLDRCompare = false;
        bool hasBCond = false;
        bool hasCmp = false;
        string ldrReg = "";
        foreach (var instruction in block.instructions)
        {
            switch (instruction.Opcode())
            {
                case OpCode.LDR:
                {
                    var compareReg = instruction.Operands()[0];
                    //it must be a dispatcher register 
                    if (finder._rightCompareRegs.Contains(compareReg.registerName))
                    {
                        var operand = instruction.Operands()[1];
                        if (operand.kind == Arm64OperandKind.Memory)
                        {
                           var add=  operand.memoryOperand.addend;
                           var sp=  simulation.RegContext.GetSpRegister();
                           var imm = sp.Get(add);
                           if (imm!=0)
                           {
                                 ldrReg = compareReg.registerName;
                                 isLDRCompare = true;
                           }
                         
                        }
                    }
                    break;
                }
                case OpCode.CMP:
                    var left = instruction.Operands()[0];
                    var right = instruction.Operands()[1];
                    if (finder._leftCompareRegs.Contains(left.registerName)
                        && ldrReg!=right.registerName && finder._rightCompareRegs.Contains(right.registerName))
                    {
                     
                        hasCmp = true;
                    }
                    break;
                case OpCode.B_NE:
                case OpCode.B_EQ:
                case OpCode.B_GT:
                case OpCode.B_LE:

                    var nextBlock = block.GetLinkedBlocks(simulation)[0];
                    if (simulation.IsDispatcherBlock(nextBlock))
                    {
                        foreach (var ins in nextBlock.instructions.Where(ins => ins.Opcode()==OpCode.CMP))
                        {   
                            var leftCompare = ins.Operands()[0];
                            if (leftCompare.registerName == ldrReg)
                            {
                                hasBCond = true;
                            }
                        }
                    }
                    break;
            }
        }
       
        if (hasBCond && hasCmp&& isLDRCompare)
        {
            return true;
        }

        return false;
    }

    public static bool CheckChildDispatcherFeatureWith3Ins4MoveCmpB(this SmartDispatcherFinder finder, Block block,
        Simulation simulation)
    {
        bool hasMov = false;
        bool hasCmp = false;
        bool hasBCond = false;
        string curCmpName = "";
        foreach (var instruction in block.instructions)
        {
            switch (instruction.Opcode())
            {
                case OpCode.MOV:
                {
                    var left = instruction.Operands()[0];
                    var right = instruction.Operands()[1];
                    if (right.kind == Arm64OperandKind.Immediate && right.immediateValue != 0)
                    {
                        curCmpName = left.registerName;
                        hasMov = true;
                    }
                }

                    break;
                case OpCode.CMP:
                    var leftCmp = instruction.Operands()[0];
                    var rightCmp = instruction.Operands()[1];
                    if (finder._leftCompareRegs.Contains(leftCmp.registerName))
                    {
                        //Get is immediate value
                        var leftValue = simulation.RegContext.GetRegister(leftCmp.registerName).GetLongValue();
                        if (leftValue != 0 && rightCmp.registerName == curCmpName)
                        {
                            hasCmp = true;
                        }
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

        if (hasBCond && hasCmp && hasMov)
        {
            return true;
        }

        return false;
    }

    public static bool CheckChildDispatcherFeatureWith2Ins4MovAndB(this SmartDispatcherFinder finder, Block block,
        Simulation simulation)
    {
        bool hasMov = false;
        bool hasB = false;
        foreach (var instruction in block.instructions)
        {
            switch (instruction.Opcode())
            {
                case OpCode.MOV:
                    var left = instruction.Operands()[0];
                    var right = instruction.Operands()[1];
                    if (finder._leftCompareRegs.Contains(left.registerName) && right.kind == Arm64OperandKind.Immediate
                                                                             && right.immediateValue != 0)
                    {
                        hasMov = true;
                    }

                    break;
                case OpCode.B:
                    // B               loc_17F59C
                    var nextBlock=block.GetLinkedBlocks(simulation)[0];
                    if (simulation.IsDispatcherBlock(nextBlock))
                    {
                        hasB = true;
                    }
                    break;
            }
        }

        if (hasMov && hasB)
        {
            return true;
        }

        return false;
    }

    public static bool CheckChildDispatcherFeatureWith2Ins4Cmp(this SmartDispatcherFinder finder, Block block,
        Simulation simulation)
    {
        bool hasCmp = false;
        bool hasBCond = false;
        foreach (var instruction in block.instructions)
        {
            switch (instruction.Opcode())
            {
                case OpCode.CMP:
                    var left = instruction.Operands()[0];
                    var right = instruction.Operands()[1];
                    if (finder._leftCompareRegs.Contains(left.registerName)
                        &&finder._rightCompareRegs.Contains(right.registerName))
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
}