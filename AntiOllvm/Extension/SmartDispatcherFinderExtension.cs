using AntiOllvm.entity;
using AntiOllvm.Helper;
using AntiOllvm.Logging;

namespace AntiOllvm.Extension;

public static class SmartDispatcherFinderExtension
{
    public static bool CheckChildDispatcherFeatureWith3Ins4MoveCmpB(this SmartDispatcherFinder finder, Block block,
        RegisterContext context)
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
                    if ( right.kind == Arm64OperandKind.Immediate && right.immediateValue != 0)
                    {
                        curCmpName=left.registerName;
                        hasMov = true;
                    }
                }
                    
                    break;
                case OpCode.CMP:
                    var leftCmp = instruction.Operands()[0];
                    var rightCmp = instruction.Operands()[1];
                    if (finder._multiCompareRegs.Contains(leftCmp.registerName))
                    {
                        //Get is immediate value
                        var leftValue = context.GetRegister(leftCmp.registerName).GetLongValue();
                        if (leftValue != 0&& rightCmp.registerName==curCmpName)
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
        RegisterContext context)
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
                    if (finder._multiCompareRegs.Contains(left.registerName) && right.kind == Arm64OperandKind.Immediate
                                                                             && right.immediateValue != 0)
                    {
                        hasMov = true;
                    }

                    break;
                case OpCode.B:
                    hasB = true;
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
        RegisterContext context)
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
                    if (finder._multiCompareRegs.Contains(left.registerName))
                    {
                        //Get is immediate value
                        var leftValue = context.GetRegister(left.registerName).GetLongValue();
                        var rightValue = context.GetRegister(right.registerName).GetLongValue();
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