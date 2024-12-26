namespace AntiOllvm;

public class Immediate : RegisterValue
{
    public long Value { get; set; }
    public Immediate(long v)
    {
        Value = v;
        
    }
    public override object Clone()
    {
        return new Immediate(Value);
    }
}