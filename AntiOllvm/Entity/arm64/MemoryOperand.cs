namespace AntiOllvm.entity;

public class MemoryOperand
{
    public string registerName;
    public string addend;


    public override string ToString()
    {
        return  registerName + " " + addend;
    }
}