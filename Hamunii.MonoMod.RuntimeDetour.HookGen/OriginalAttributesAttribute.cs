using System.Collections.Generic;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.DotNet.Signatures.Types;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables.Rows;

namespace Hamunii.MonoMod.RuntimeDetour.HookGen;

internal class OriginalAttributesAttribute
{
    private static Dictionary<HookGenTarget, string> _typeNames =
        new()
        {
            [HookGenTarget.Types] = "TypeAttributes",
            [HookGenTarget.Methods] = "MethodAttributes",
            [HookGenTarget.Fields] = "FieldAttributes",
        };

    private Dictionary<HookGenTarget, TypeSignature> _attributesTypes = new();
    private Dictionary<HookGenTarget, MethodDefinition> _constructors = new();

    public TypeDefinition Type { get; }

    public OriginalAttributesAttribute(ModuleDefinition module)
    {
        var corLibScope = module.CorLibTypeFactory.CorLibScope;
        var attributeReference = corLibScope
            .CreateTypeReference("System", "Attribute")
            .ImportWith(module.DefaultImporter);
        var baseConstructorReference = attributeReference
            .CreateMemberReference(
                ".ctor",
                MethodSignature.CreateInstance(module.CorLibTypeFactory.Void)
            )
            .ImportWith(module.DefaultImporter);

        Type = new TypeDefinition(
            "Hamunii.MonoMod.RuntimeDetour.HookGen",
            "OriginalAttributesAttribute",
            TypeAttributes.NotPublic | TypeAttributes.Sealed,
            attributeReference
        );
        module.TopLevelTypes.Add(Type);

        foreach (var pair in _typeNames)
        {
            var attributesType = _attributesTypes[pair.Key] = corLibScope
                .CreateTypeReference("System.Reflection", pair.Value)
                .ImportWith(module.DefaultImporter)
                .ToTypeSignature();

            var constructorDefinition = new MethodDefinition(
                ".ctor",
                MethodAttributes.HideBySig
                    | MethodAttributes.SpecialName
                    | MethodAttributes.RuntimeSpecialName
                    | MethodAttributes.Public,
                MethodSignature.CreateInstance(module.CorLibTypeFactory.Void, attributesType)
            );
            Type.Methods.Add(constructorDefinition);

            var body = constructorDefinition.CilMethodBody = new CilMethodBody(
                constructorDefinition
            );
            body.Instructions.Add(CilOpCodes.Ldarg_0);
            body.Instructions.Add(CilOpCodes.Call, baseConstructorReference);
            body.Instructions.Add(CilOpCodes.Ret);

            _constructors[pair.Key] = constructorDefinition;
        }
    }

    private CustomAttribute ToCustomAttribute(HookGenTarget target, int value)
    {
        return new CustomAttribute(
            _constructors[target],
            new CustomAttributeSignature(
                new[] { new CustomAttributeArgument(_attributesTypes[target], value) }
            )
        );
    }

    public CustomAttribute ToCustomAttribute(TypeAttributes attributes) =>
        ToCustomAttribute(HookGenTarget.Types, (int)attributes);

    public CustomAttribute ToCustomAttribute(MethodAttributes attributes) =>
        ToCustomAttribute(HookGenTarget.Methods, (int)attributes);

    public CustomAttribute ToCustomAttribute(FieldAttributes attributes) =>
        ToCustomAttribute(HookGenTarget.Fields, (int)attributes);
}
