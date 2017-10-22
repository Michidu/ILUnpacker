using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace ILUnpacker
{
    internal class Program
    {
        private static AssemblyWriter _assemblyWriter;

        private static Assembly _assembly;

        internal static MethodDef _executingMethod;

        public static void Main(string[] args)
        {
            if (args.Length < 1)
                return;

            string path = args[0];

            _assemblyWriter = new AssemblyWriter(path);

            _assembly = Assembly.LoadFrom(path);

            Console.WriteLine("Unpacking..");

            var stringDecrypter = new StringDecrypter(_assembly);

            Memory.Hook(typeof(StackFrame).GetMethod("GetMethod", BindingFlags.Instance | BindingFlags.Public),
                typeof(Program).GetMethod("HookGetMethod", BindingFlags.Instance | BindingFlags.Public));

            int invokeToken = 0;

            var types = _assemblyWriter.ModuleDef.GetTypes();
            var typeDefs = types as IList<TypeDef> ?? types.ToList();

            foreach (TypeDef typeDef in typeDefs)
            {
                if (typeDef.Name == "<Module>")
                {
                    foreach (FieldDef fieldDef in typeDef.Fields)
                    {
                        if (fieldDef.FullName.Contains("<Module>::Invoke"))
                        {
                            invokeToken = fieldDef.MDToken.ToInt32();

                            goto exitLoop;
                        }
                    }
                }
            }

            exitLoop:

            if (invokeToken == 0)
                throw new Exception("Couldn't find Invoke");

            FieldInfo field = _assembly.Modules.FirstOrDefault().ResolveField(invokeToken);
            var invokeField = field.GetValue(null);
            MethodInfo invokeMethod = invokeField.GetType().GetMethod("Invoke");
            if (invokeMethod == null)
                throw new NullReferenceException("Couldn't find InvokeMethod");

            InvokeDelegates(typeDefs, invokeMethod, invokeField);

            stringDecrypter.ReplaceStrings(typeDefs);

            CleanCctor(typeDefs);

            _assemblyWriter.Save();

            Console.WriteLine("Unpacked file!");

            Console.Read();
        }

        private static void InvokeDelegates(IList<TypeDef> typeDefs, MethodInfo invokeMethod, object invokeField)
        {
            string moduleName = _assembly.Modules.FirstOrDefault().Name;

            IlReader ilReader = new IlReader(_assemblyWriter);

            foreach (TypeDef typeDef in typeDefs)
            {
                foreach (MethodDef methodDef in typeDef.Methods)
                {
                    if (methodDef.Module.Name != moduleName)
                        continue;

                    if (methodDef.HasBody &&
                        methodDef.Body.Instructions.Count > 2 &&
                        methodDef.Body.Instructions[0].OpCode == OpCodes.Ldsfld &&
                        methodDef.Body.Instructions[0].Operand.ToString().Contains("Invoke") &&
                        methodDef.Body.Instructions[1].IsLdcI4())
                    {
                        int methodIdx = (int) methodDef.Body.Instructions[1].Operand;

                        _executingMethod = methodDef;

                        var methodDelegate = invokeMethod.Invoke(invokeField, new object[] {methodIdx});

                        ilReader.ReadMethod(methodDelegate);
                    }
                }
            }
        }

        public MethodBase HookGetMethod()
        {
            var method = (MethodBase) GetType().GetField("method", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(this);

            if (method.DeclaringType?.Namespace == "ILUnpacker" && _executingMethod != null)
            {
                method = _assembly.Modules.FirstOrDefault().ResolveMethod(_executingMethod.MDToken.ToInt32());
            }

            return method;
        }

        private static void CleanCctor(IList<TypeDef> typeDefs)
        {
            foreach (TypeDef typeDef in typeDefs)
            {
                if (typeDef.Name == "<Module>")
                {
                    MethodDef cctor = typeDef.FindStaticConstructor();

                    if (cctor.HasBody)
                    {
                        cctor.Body.Instructions.Clear();
                        cctor.Body.ExceptionHandlers.Clear();

                        cctor.Body.Instructions.Add(new Instruction(OpCodes.Ret));
                    }

                    break;
                }
            }
        }
    }
}