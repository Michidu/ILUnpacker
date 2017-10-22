using System;
using System.IO;
using dnlib.DotNet;
using dnlib.DotNet.Writer;

namespace ILUnpacker
{
    internal class AssemblyWriter
    {
        private readonly string _assemblyPath;

        internal ModuleDefMD ModuleDef;

        internal AssemblyWriter(string assemblyPath)
        {
            _assemblyPath = assemblyPath;

            AssemblyResolver asmResolver = new AssemblyResolver();
            ModuleContext modCtx = new ModuleContext(asmResolver);

            asmResolver.EnableTypeDefCache = true;
            asmResolver.DefaultModuleContext = modCtx;

            ModuleDef = ModuleDefMD.Load(assemblyPath, modCtx);

            ModuleDef.Context = modCtx;
            ModuleDef.Context.AssemblyResolver.AddToCache(ModuleDef);
        }

        internal void WriteMethod(MethodDef methodDef)
        {
            MethodDef method = Program._executingMethod;
            if (method == null)
            {
                Console.WriteLine("Failed to write " + methodDef);
                return;
            }

            Program._executingMethod = null;

            method.FreeMethodBody();
            method.Body = methodDef.Body;
        }

        internal void Save()
        {
            string file = Path.GetFileNameWithoutExtension(_assemblyPath);
            string newPath = _assemblyPath.Replace(file, file + "_unpacked");

            var wOpts = new ModuleWriterOptions(ModuleDef);
            ModuleDef.Write(newPath, wOpts);
        }
    }
}