using System;
using dnlib.DotNet.Emit;

namespace ILUnpacker
{
    internal class IlReader
    {
        private readonly AssemblyWriter _assemblyWriter;

        internal IlReader(AssemblyWriter assemblyWriter)
        {
            _assemblyWriter = assemblyWriter;
        }

        internal void ReadMethod(object method)
        {
            try
            {
                var methodBodyReader = new DynamicMethodBodyReader(_assemblyWriter.ModuleDef, method);
                methodBodyReader.Read();

                var methodDef = methodBodyReader.GetMethod();

                _assemblyWriter.WriteMethod(methodDef);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error in ReadMethod(): " + e.Message);
                throw;
            }
        }
    }
}