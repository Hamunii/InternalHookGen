using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Hamunii.MonoMod.RuntimeDetour.HookGen.MSBuild;

public class HookGenTask : Task
{
    [Required]
    public string IntermediateOutputPath { get; set; }

    [Required]
    public ITaskItem[] ReferencePath { get; set; }

    [Required]
    public ITaskItem[] PackageReference { get; set; }

    [Required]
    public ITaskItem[] HookGen { get; set; }

    [Output]
    public ITaskItem[] RemovedReferences { get; private set; }

    [Output]
    public ITaskItem[] HookGeneratedReferences { get; private set; }

    public override bool Execute()
    {
        var outputDirectory = Path.Combine(IntermediateOutputPath, "MMHOOK");
        Directory.CreateDirectory(outputDirectory);

        var packagesToPublicize = PackageReference
            .Where(x => x.GetBoolMetadata("HookGen"))
            .ToDictionary(x => x.ItemSpec);
        var assemblyNamesToPublicize = HookGen.ToDictionary(x => x.ItemSpec);

        // var removedReferences = new List<ITaskItem>();
        var hookGeneratedReferences = new List<ITaskItem>();

        foreach (var taskItem in ReferencePath)
        {
            var fileName = taskItem.GetMetadata("FileName");

            ITaskItem optionsHolder;

            if (taskItem.GetBoolMetadata("HookGen"))
            {
                optionsHolder = taskItem;
            }
            else if (assemblyNamesToPublicize.TryGetValue(fileName, out optionsHolder)) { }
            else if (
                taskItem.TryGetMetadata("NuGetPackageId", out var nuGetPackageId)
                && packagesToPublicize.TryGetValue(nuGetPackageId, out optionsHolder)
            ) { }
            else
            {
                continue;
            }

            var options = new AssemblyPublicizerOptions();

            if (optionsHolder.TryGetMetadata("HookGenTarget", out var rawTarget))
            {
                if (Enum.TryParse<HookGenTarget>(rawTarget, true, out var parsedTarget))
                {
                    options.Target = parsedTarget;
                }
                else
                {
                    throw new FormatException(
                        $"String '{rawTarget}' was not recognized as a valid HookGenTarget."
                    );
                }
            }

            if (
                optionsHolder.TryGetMetadata(
                    "PublicizeCompilerGenerated",
                    out var rawPublicizeCompilerGenerated
                )
            )
            {
                options.PublicizeCompilerGenerated = bool.Parse(rawPublicizeCompilerGenerated);
            }

            if (optionsHolder.TryGetMetadata("Strip", out var rawStrip))
            {
                options.Strip = bool.Parse(rawStrip);
            }

            var assemblyPath = taskItem.GetMetadata("FullPath");
            var hash = ComputeHash(File.ReadAllBytes(assemblyPath), options);

            var hookGeneratedAssemblyPath = Path.Combine(
                outputDirectory,
                "MMHOOK_" + Path.GetFileName(assemblyPath)
            );
            var hashPath = hookGeneratedAssemblyPath + ".md5";

            // removedReferences.Add(taskItem);

            var hookGeneratedReference = new TaskItem(hookGeneratedAssemblyPath);
            taskItem.CopyMetadataTo(hookGeneratedReference);
            hookGeneratedReference.RemoveMetadata("ReferenceAssembly");
            hookGeneratedReferences.Add(hookGeneratedReference);

            if (File.Exists(hashPath) && File.ReadAllText(hashPath) == hash)
            {
                Log.LogMessage($"{fileName} was already hookGenerated, skipping");
                continue;
            }

            HookGenRunner.RunHookGen(assemblyPath, hookGeneratedAssemblyPath);
            // AssemblyPublicizer.Publicize(assemblyPath, hookGeneratedAssemblyPath, options);


            var originalDocumentationPath = Path.Combine(
                Path.GetDirectoryName(assemblyPath)!,
                fileName + ".xml"
            );
            if (File.Exists(originalDocumentationPath))
            {
                File.Copy(
                    originalDocumentationPath,
                    Path.Combine(outputDirectory, fileName + ".xml"),
                    true
                );
            }

            File.WriteAllText(hashPath, hash);

            Log.LogMessage($"MMHOOK generated for {fileName}");
        }

        // RemovedReferences = removedReferences.ToArray();
        HookGeneratedReferences = hookGeneratedReferences.ToArray();

        return true;
    }

    private static string ComputeHash(byte[] bytes, AssemblyPublicizerOptions options)
    {
        static void Hash(ICryptoTransform hash, byte[] buffer)
        {
            hash.TransformBlock(buffer, 0, buffer.Length, buffer, 0);
        }

        static void HashString(ICryptoTransform hash, string str) =>
            Hash(hash, Encoding.UTF8.GetBytes(str));
        static void HashBool(ICryptoTransform hash, bool value) =>
            Hash(hash, BitConverter.GetBytes(value));
        static void HashInt(ICryptoTransform hash, int value) =>
            Hash(hash, BitConverter.GetBytes(value));

        using var md5 = MD5.Create();

        HashString(
            md5,
            typeof(HookGenRunner)
                .Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                .InformationalVersion
        );
        HashString(
            md5,
            typeof(HookGenTask)
                .Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                .InformationalVersion
        );

        HashInt(md5, (int)options.Target);
        HashBool(md5, options.PublicizeCompilerGenerated);
        HashBool(md5, options.Strip);

        md5.TransformFinalBlock(bytes, 0, bytes.Length);

        return ByteArrayToString(md5.Hash);
    }

    private static string ByteArrayToString(IReadOnlyCollection<byte> data)
    {
        var builder = new StringBuilder(data.Count * 2);

        foreach (var b in data)
        {
            builder.AppendFormat("{0:x2}", b);
        }

        return builder.ToString();
    }
}
