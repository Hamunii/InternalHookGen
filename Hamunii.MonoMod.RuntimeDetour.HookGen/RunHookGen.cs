using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using Mono.Cecil;
using MonoMod;
using MonoMod.InlineRT;
using MonoMod.RuntimeDetour.HookGen;

namespace Hamunii.MonoMod.RuntimeDetour.HookGen;

public static class HookGenRunner
{
    private static bool skipHashing => false;
    private static readonly object hookGenLock = new object();

    public static bool RunHookGen(string pathIn, string pathOut)
    {
        // var assemblyName = plugin.GUID;
        // var location = plugin.Path;

        // string mmhookFolder = Patcher.mmhookPath;

        // if(plugin.Path.Contains(Paths.ManagedPath))
        //     mmhookFolder = Path.Combine(Patcher.mmhookPath, "Managed");
        // else if(plugin.Path.Contains(Paths.PluginPath))
        //     mmhookFolder = Path.Combine(Patcher.mmhookPath, "plugins");
        // else if(plugin.Path.Contains(Paths.BepInExAssemblyDirectory))
        //     mmhookFolder = Path.Combine(Patcher.mmhookPath, "core");
        // else if(plugin.Path.Contains(Paths.PatcherPluginPath))
        //     mmhookFolder = Path.Combine(Patcher.mmhookPath, "patchers");

        // var fileExtension = ".dll";
        // var mmhookFileName = "MMHOOK_" + assemblyName + fileExtension;
        // string pathIn = location;
        // string pathOut = Path.Combine(mmhookFolder, mmhookFileName);
        // bool shouldCreateDirectory = true;

        // foreach (string mmhookFile in Patcher.mmHookFiles)
        // {
        //     if (Path.GetFileName(mmhookFile).Equals(mmhookFileName))
        //     {
        //         pathOut = mmhookFile;
        //         // // Console.WriteLine($"Previous location found for '{mmhookFileName}'. Using that location to save instead.");
        //         shouldCreateDirectory = false;
        //         break;
        //     }
        // }
        // if (shouldCreateDirectory)
        // {
        //     Directory.CreateDirectory(mmhookFolder);
        // }

        var fileInfo = new FileInfo(pathIn);
        var size = fileInfo.Length;
        long hash = 0;

        // if (File.Exists(pathOut))
        // {
        //     try
        //     {
        //         using (var oldMM = AssemblyDefinition.ReadAssembly(pathOut))
        //         {
        //             bool mmSizeHash = oldMM.MainModule.GetType("BepHookGen.size" + size) != null;
        //             if (mmSizeHash)
        //             {
        //                 if (skipHashing)
        //                 {
        //                     Patcher.ExtendedLogging($"[{nameof(RunHookGen)}] (skip hashing) Already ran for latest version of {mmhookFileName}");
        //                     return true;
        //                 }
        //                 hash = fileInfo.MakeHash();
        //                 bool mmContentHash = oldMM.MainModule.GetType("BepHookGen.content" + hash) != null;
        //                 if (mmContentHash)
        //                 {
        //                     Patcher.ExtendedLogging($"[{nameof(RunHookGen)}] Already ran for latest version of {mmhookFileName}");
        //                     plugin.MMHOOKDate = File.GetLastWriteTime(pathOut).Ticks;
        //                     return true;
        //                 }
        //             }
        //         }
        //     }
        //     catch (BadImageFormatException)
        //     {
        //         // Patcher.Logger.LogWarning($"Failed to read {Path.GetFileName(pathOut)}, probably corrupted, remaking one.");
        //     }
        // }

        Environment.SetEnvironmentVariable("MONOMOD_HOOKGEN_PRIVATE", "1");
        Environment.SetEnvironmentVariable("MONOMOD_DEPENDENCY_MISSING_THROW", "0");

        {
            // Doing this instead of using(){} because MonoModder creation needs to be locked, but
            // we want to do the rest asynchronously, which I don't know how else I would do it.
            // This is basically what using(){} does, at least according to https://stackoverflow.com/a/75483
            MonoModder mm = null!;
            try
            {
                lock (hookGenLock)
                {
                    mm = new MonoModder()
                    {
                        InputPath = pathIn,
                        OutputPath = pathOut,
                        ReadingMode = ReadingMode.Deferred
                    };

                    (mm.AssemblyResolver as BaseAssemblyResolver)?.AddSearchDirectory(
                        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                    );

                    mm.Read();

                    mm.MapDependencies();

                    if (File.Exists(pathOut))
                    {
                        Console.WriteLine($"Clearing {pathOut}");
                        File.Delete(pathOut);
                    }
                }

                HookGenerator gen;
                Console.WriteLine($"[{nameof(RunHookGen)}] Starting HookGenerator for {pathIn}");
                lock (hookGenLock)
                {
                    gen = new HookGenerator(mm, Path.GetFileName(pathOut));
                }

                using (ModuleDefinition mOut = gen.OutputModule)
                {
                    gen.Generate();
                    mOut.Types.Add(
                        new TypeDefinition(
                            "BepHookGen",
                            "size" + size,
                            Mono.Cecil.TypeAttributes.Class | Mono.Cecil.TypeAttributes.Public,
                            mOut.TypeSystem.Object
                        )
                    );
                    if (!skipHashing)
                    {
                        mOut.Types.Add(
                            new TypeDefinition(
                                "BepHookGen",
                                "content" + (hash == 0 ? fileInfo.MakeHash() : hash),
                                Mono.Cecil.TypeAttributes.Class | Mono.Cecil.TypeAttributes.Public,
                                mOut.TypeSystem.Object
                            )
                        );
                    }
                    lock (hookGenLock)
                    {
                        mOut.Write(pathOut);
                    }
                }

                Console.WriteLine($"[{nameof(RunHookGen)}] HookGen done for {pathIn}");
                // plugin.MMHOOKDate = File.GetLastWriteTime(pathOut).Ticks;
            }
            catch (BadImageFormatException)
            {
                // plugin.BadImageFormat = true;
            }
            catch (Exception e)
            {
                File.WriteAllText(pathOut, e.ToString());
                try
                {
                    Console.WriteLine($"[{nameof(RunHookGen)}] Error in HookGen for {pathIn}: {e}");
                }
                catch (Exception) { }
            }
            finally
            {
                mm?.Dispose();
            }
        }

        return true;
    }

    private static long MakeHash(this FileInfo fileInfo)
    {
        var fileStream = fileInfo.OpenRead();
        byte[] hashbuffer = null!;
        using (MD5 md5 = new MD5CryptoServiceProvider())
        {
            hashbuffer = md5.ComputeHash(fileStream);
        }
        long hash = BitConverter.ToInt64(hashbuffer, 0);
        return hash != 0 ? hash : 1;
    }
}
