# InternalHookGen.MSBuild

MonoMod's HookGen in MSBuild.  
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
