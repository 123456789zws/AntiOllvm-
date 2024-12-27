namespace AntiOllvm;

public class SPRegister : RegisterValue
{   
    
    private Dictionary<string,long> _spdirect = new();
    public override object Clone()
    {
        var sp=new SPRegister();
        foreach (var keyValuePair in _spdirect)
        {
            sp._spdirect.Add(keyValuePair.Key,keyValuePair.Value);
        }
        return sp;
    }
    
    public void Put(string key, long value)
    {
        _spdirect[key] = value;
    }
    public long Get(string key)
    {
        return _spdirect[key];
    }
}