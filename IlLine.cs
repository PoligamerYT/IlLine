#region

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Pdb;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

#endregion

namespace IlLine;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class IlLine : BaseUnityPlugin
{
    private IlLine()
    {
        new ILHook(typeof(StackTrace).GetMethod("AddFrames", BindingFlags.Instance | BindingFlags.NonPublic), IlHook);
    }

    private void IlHook(ILContext il)
    {
        var ilcursor = new ILCursor(il);
        var ilcursor2 = ilcursor;
        var array = new Func<Instruction, bool>[1];
        array[0] = x =>
            x.MatchCallvirt(typeof(StackFrame).GetMethod("GetFileLineNumber", BindingFlags.Instance | BindingFlags.Public));
        ilcursor2.GotoNext(array);
        ilcursor.RemoveRange(2);
        ilcursor.EmitDelegate(GetLineOrIL);
    }

    private static string GetLineOrIL(StackFrame instance)
    {
        var dllPath = instance.GetMethod().DeclaringType.Assembly.Location;
        var pdbPath = Path.ChangeExtension(dllPath, "pdb");
        var fileLineNumber = instance.GetFileLineNumber();
        if (fileLineNumber == -1 || fileLineNumber == 0)
        {
            if (File.Exists(dllPath))
            {
                if (File.Exists(pdbPath))
                {
                    return GetLineFromPBD(instance.GetILOffset(), dllPath, pdbPath);
                }
                return "IL_" + instance.GetILOffset().ToString("X4");
            }
            return "IL_" + instance.GetILOffset().ToString("X4");
        }
        return fileLineNumber.ToString();
    }

    private static string GetLineFromPBD(int ilOffset, string dllPath, string pdbPath)
    {
        using (var assemblyDefinition = AssemblyDefinition.ReadAssembly(dllPath, new ReaderParameters
               {
                   ReadSymbols = true,
                   SymbolReaderProvider = new PdbReaderProvider(),
                   SymbolStream = new FileStream(pdbPath, FileMode.Open, FileAccess.Read)
               }))
        {
            foreach (var module in assemblyDefinition.Modules)
            {
                foreach (var method in module.GetTypes().SelectMany(t => t.Methods))
                {
                    foreach (var instruction in method.Body.Instructions)
                    {
                        if (instruction.Offset == ilOffset)
                        {
                            var sequencePoint = method.DebugInformation.GetSequencePoint(instruction);
                            if (sequencePoint != null)
                            {
                                var sourceLine = sequencePoint.StartLine;

                                return sourceLine.ToString();
                            }
                        }
                    }
                }
            }
        }

        return "IL_" + ilOffset.ToString("X4");
    }
}
