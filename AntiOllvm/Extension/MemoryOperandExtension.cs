using AntiOllvm.entity;

namespace AntiOllvm.Extension;

public static class MemoryOperandExtension
{
    
    public static bool IsOperandSpRegister(this MemoryOperand memoryOperand)
    {
        if (memoryOperand.registerName == "SP")
        {
            return true;
        }
        return false;
    }
}