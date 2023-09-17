using BepInEx;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Pdb;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace IlLine
{
    [BepInPlugin("com.poligamer.IlLine", "IlLine", "1.0.0")]
    public class IlLine : BaseUnityPlugin
    {
        private IlLine()
        {
            new ILHook(typeof(StackTrace).GetMethod("AddFrames", BindingFlags.Instance | BindingFlags.NonPublic), new ILContext.Manipulator(this.IlHook));
        }
        private void IlHook(ILContext il)
        {
            ILCursor ilcursor = new ILCursor(il);
            ILCursor ilcursor2 = ilcursor;
            Func<Instruction, bool>[] array = new Func<Instruction, bool>[1];
            array[0] = ((Instruction x) => ILPatternMatchingExt.MatchCallvirt(x, typeof(StackFrame).GetMethod("GetFileLineNumber", BindingFlags.Instance | BindingFlags.Public)));
            ilcursor2.GotoNext(array);
            ilcursor.RemoveRange(2);
            ilcursor.EmitDelegate<Func<StackFrame, string>>(new Func<StackFrame, string>(IlLine.GetLineOrIL));
        }

        private static string GetLineOrIL(StackFrame instace)
        {
            string dllPath = instace.GetMethod().DeclaringType.Assembly.Location;
            string pdbPath = Path.ChangeExtension(dllPath, "pdb");
            int fileLineNumber = instace.GetFileLineNumber();
            if (fileLineNumber == -1 || fileLineNumber == 0)
            {
                if (File.Exists(dllPath))
                {
                    if(File.Exists(pdbPath)) 
                    {
                        return GetLineFromPBD(instace.GetILOffset(), dllPath, pdbPath);
                    }
                    else
                    {
                        return "IL_" + instace.GetILOffset().ToString("X4");
                    }
                }
                else
                {
                    return "IL_" + instace.GetILOffset().ToString("X4");
                }
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
                                    int sourceLine = sequencePoint.StartLine;

                                    return sourceLine.ToString();
                                }
                            }
                        }
                    }
                }
            }

            return "IL_" + ilOffset.ToString("X4");
        }

        internal const string modname = "IlLine";

        internal const string version = "1.0.0";
    }
}
