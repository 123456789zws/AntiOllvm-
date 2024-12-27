namespace AntiOllvm;

public class Config
{
    public string ida_cfg_path { get; set; }
    public string fix_outpath { get; set; }
    /**
     * Enable the support of SP register default is false
     */
    public bool support_sp { get; set; }
}