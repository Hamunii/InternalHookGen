using System;

namespace Hamunii.MonoMod.RuntimeDetour.HookGen;

public class AssemblyPublicizerOptions
{
    public HookGenTarget Target { get; set; } = HookGenTarget.All;
    public bool PublicizeCompilerGenerated { get; set; } = false;
    public bool Strip { get; set; } = false;

    internal bool HasTarget(HookGenTarget target)
    {
        return (Target & target) != 0;
    }
}

[Flags]
public enum HookGenTarget
{
    All = Types | Methods | Fields,
    None = 0,
    Types = 1 << 0,
    Methods = 1 << 1,
    Fields = 1 << 2,
}
