using AntiOllvm.entity;
using AntiOllvm.Logging;

namespace AntiOllvm;

public class Register : ICloneable
{
    public string name { get; set; }
    public RegisterValue value { get; set; }

    public const long UNKNOWN_VALUE = 0;
    
    public void SetLongValue( long v)
    {
        value = new Immediate(v);
    }
    public long GetLongValue()
    {
        if (value is Immediate immediate)
        {
            IConvertible convertible = immediate.Value;
            return convertible.ToLong();
        }
        throw new Exception(" value is not Immediate");
        // return UNKNOWN_VALUE;
    }

    public int GetIntValue()
    {
        try
        {
            var l = GetLongValue();
            return Convert.ToInt32(l);
        }
        catch (Exception e)
        {   long l = GetLongValue();
            var i = (int)GetLongValue();
            // Logger.InfoNewline($" {l} ({l:X}) Change To  {i}");
            return i;
        }
      
    }

    public object Clone()
    {
        return new Register
        {
            name = name,
            value = (RegisterValue)value.Clone()
        };
    }
}