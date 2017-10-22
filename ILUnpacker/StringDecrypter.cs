using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace ILUnpacker
{
    internal class StringDecrypter
    {
        private readonly Assembly _assembly;

        private object _decryptField;
        private MethodInfo _decryptMethod;

        internal StringDecrypter(Assembly assembly)
        {
            _assembly = assembly;
        }

        internal void ReplaceStrings(IList<TypeDef> typeDefs)
        {
            foreach (TypeDef typeDef in typeDefs)
            {
                foreach (MethodDef methodDef in typeDef.Methods)
                {
                    if (!methodDef.HasBody)
                        continue;

                    var instructions = methodDef.Body.Instructions;
                    for (var i = 0; i < instructions.Count; i++)
                    {
                        Instruction instruction = instructions[i];

                        if (instruction.OpCode == OpCodes.Ldsfld &&
                            instruction.Operand.ToString().Contains("<Module>::String") &&
                            instructions[i + 1].IsLdcI4() &&
                            instructions[i + 2].OpCode == OpCodes.Callvirt &&
                            instructions[i + 2].Operand.ToString().Contains("Invoke"))
                        {
                            if (_decryptField == null)
                            {
                                FieldDef fieldDef = (FieldDef) instruction.Operand;
                                InitDecryptor(fieldDef);
                            }

                            int stringIdx = (int) instructions[i + 1].Operand;

                            instructions[i].OpCode = OpCodes.Ldstr;
                            instructions[i].Operand = GetString(stringIdx);

                            instructions[i + 1].OpCode = OpCodes.Nop;
                            instructions[i + 2].OpCode = OpCodes.Nop;
                        }
                    }
                }
            }
        }

        private void InitDecryptor(FieldDef fieldDef)
        {
            FieldInfo field = _assembly.Modules.FirstOrDefault()
                .ResolveField(fieldDef.MDToken.ToInt32());
            _decryptField = field.GetValue(null);

            _decryptMethod = _decryptField.GetType().GetMethod("Invoke");
        }

        private string GetString(int idx)
        {
            string res = (string) _decryptMethod.Invoke(_decryptField, new object[] {idx});
            return res;
        }
    }
}