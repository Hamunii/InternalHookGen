# InternalHookGen.MSBuild

[MonoMod](https://github.com/MonoMod/MonoMod)'s HookGen in MSBuild.  
Forked from [BepInEx.AssemblyPublicizer](https://github.com/BepInEx/BepInEx.AssemblyPublicizer).

## Usage

### Installation

Add this NuGet package in your csproj:
```xml
<ItemGroup>
    <PackageReference Include="InternalHookGen.MSBuild" Version="1.*" PrivateAssets="all"/>
</ItemGroup>
```

### Usage
The usage is identical to [BepInEx.AssemblyPublicizer](https://github.com/BepInEx/BepInEx.AssemblyPublicizer), except `HookGen` is used instead of `Publicize`:
```xml
<ItemGroup>
    <!-- HookGen directly when referencing -->
    <Reference Include=".../TestProject.dll" HookGen="true" />
    <ProjectReference Include="../TestProject/TestProject.csproj" HookGen="true" />
    <PackageReference Include="TestProject" HookGen="true" />

    <!-- HookGen by assembly name -->
    <HookGen Include="TestProject" />
</ItemGroup>
```

### What is HookGen?

MonoMod's HookGen takes an assembly a generates a new assembly from it which contains MonoMod [Hooks](https://lethal.wiki/dev/fundamentals/patching-code/monomod-documentation#hook) (`On` namespace) and [ILHooks](https://lethal.wiki/dev/fundamentals/patching-code/monomod-documentation#ilhook) (`IL` namespace) as C# Events for almost every method, which you can subscribe to.

```cs
static class Example
{
    internal static void Init()
    {
        // Subscribing to the hook event provided by HookGen
        On.SomeNamespace.SomeType.Method += MyHook;
        IL.SomeNamespace.SomeType.Method += MyILHook;
    }
    
    private static void MyHook(On.SomeNamespace.SomeType.orig_Method orig, SomeType self)
    {
        Plugin.Logger.LogInfo("Hello from MonoMod Hook!");
        orig(self); 
    }

    private static void MyILHook(ILContext il)
    {
        ILCursor c = new(il);
        // ...
        Plugin.Logger.LogInfo("IL modifications done!");
    }
}
```

### What is not HookGen?

It's probably more likely that you only know HookGen, but not manual Hooking. For that, see the [Lethal Company Modding Wiki](https://lethal.wiki/dev/fundamentals/patching-code/monomod-documentation#hook).