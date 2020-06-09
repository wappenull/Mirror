// this class generates OnSerialize/OnDeserialize when inheriting from MessageBase

using System.Linq;
using Mono.CecilX;
using Mono.CecilX.Cil;

namespace Mirror.Weaver
{
    static class MessageClassProcessor
    {

        static bool IsEmptyDefault(this MethodBody body)
        {
            return body.Instructions.All(instruction => instruction.OpCode == OpCodes.Nop || instruction.OpCode == OpCodes.Ret);
        }

        public static void Process(TypeDefinition td)
        {
            Weaver.DLog(td, "MessageClassProcessor Start");

            GenerateSerialization(td);
            if (Weaver.WeavingFailed)
            {
                return;
            }

            GenerateDeSerialization(td);
            Weaver.DLog(td, "MessageClassProcessor Done");
        }

        static MethodDefinition _FindMethodInMessage( TypeDefinition td, string methodName )
        {
            MethodDefinition method = td.Methods.FirstOrDefault(md => md.Name == methodName);

            // Wappen Hot fix: Also try base class
            TypeDefinition baseTd = td.BaseType?.Resolve( );
            while( method == null && baseTd != null )
            {
                if( baseTd.FullName == "Mirror.MessageBase" || baseTd.FullName == "Mirror.IMessageBase" )
                    break; // We will not use function from MessageBase layer
                method = baseTd.Methods.FirstOrDefault(md => md.Name == methodName);
            }

            return method;
        }

        static void GenerateSerialization(TypeDefinition td)
        {
            Weaver.DLog(td, "  GenerateSerialization");
            MethodDefinition existingMethod = _FindMethodInMessage( td, "Serialize" );
            if (existingMethod != null && !existingMethod.Body.IsEmptyDefault())
            {
                return;
            }

            if (td.Fields.Count == 0)
            {
                return;
            }

            // check for self-referencing types
            foreach (FieldDefinition field in td.Fields)
            {
                if (field.FieldType.FullName == td.FullName)
                {
                    Weaver.Error($"{td} has field ${field} that references itself");
                    return;
                }
            }

            MethodDefinition serializeFunc = existingMethod ?? new MethodDefinition("Serialize",
                    MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig,
                    Weaver.voidType);

            //only add to new method
            if (existingMethod == null)
            {
                serializeFunc.Parameters.Add(new ParameterDefinition("writer", ParameterAttributes.None, Weaver.CurrentAssembly.MainModule.ImportReference(Weaver.NetworkWriterType)));
            }
            ILProcessor serWorker = serializeFunc.Body.GetILProcessor();
            if (existingMethod != null)
            {
                //remove default nop&ret from existing empty interface method
                serWorker.Body.Instructions.Clear();
            }

            //if not struct(IMessageBase), likely same as using else {} here in all cases
            if (!td.IsValueType)
            {
                // call base
                MethodReference baseSerialize = Resolvers.ResolveMethodInParents(td.BaseType, Weaver.CurrentAssembly, "Serialize");
                if (baseSerialize != null)
                {
                    // base
                    serWorker.Append(serWorker.Create(OpCodes.Ldarg_0));
                    // writer
                    serWorker.Append(serWorker.Create(OpCodes.Ldarg_1));
                    serWorker.Append(serWorker.Create(OpCodes.Call, baseSerialize));
                }
            }

            foreach (FieldDefinition field in td.Fields)
            {
                if (field.IsStatic || field.IsPrivate || field.IsSpecialName)
                    continue;

                MethodReference writeFunc = Writers.GetWriteFunc(field.FieldType);
                if (writeFunc != null)
                {
                    serWorker.Append(serWorker.Create(OpCodes.Ldarg_1));
                    serWorker.Append(serWorker.Create(OpCodes.Ldarg_0));
                    serWorker.Append(serWorker.Create(OpCodes.Ldfld, field));
                    serWorker.Append(serWorker.Create(OpCodes.Call, writeFunc));
                }
                else
                {
                    Weaver.Error($"{field} has unsupported type");
                    return;
                }
            }
            serWorker.Append(serWorker.Create(OpCodes.Ret));

            //only add if not just replaced body
            if (existingMethod == null)
            {
                td.Methods.Add(serializeFunc);
            }
        }

        static void GenerateDeSerialization(TypeDefinition td)
        {
            Weaver.DLog(td, "  GenerateDeserialization");
            MethodDefinition existingMethod = _FindMethodInMessage( td, "Deserialize" );
            if (existingMethod != null && !existingMethod.Body.IsEmptyDefault())
            {
                return;
            }

            if (td.Fields.Count == 0)
            {
                return;
            }

            MethodDefinition serializeFunc = existingMethod ?? new MethodDefinition("Deserialize",
                    MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig,
                    Weaver.voidType);

            //only add to new method
            if (existingMethod == null)
            {
                serializeFunc.Parameters.Add(new ParameterDefinition("reader", ParameterAttributes.None, Weaver.CurrentAssembly.MainModule.ImportReference(Weaver.NetworkReaderType)));
            }
            ILProcessor serWorker = serializeFunc.Body.GetILProcessor();
            if (existingMethod != null)
            {
                //remove default nop&ret from existing empty interface method
                serWorker.Body.Instructions.Clear();
            }

            //if not struct(IMessageBase), likely same as using else {} here in all cases
            if (!td.IsValueType)
            {
                // call base
                MethodReference baseDeserialize = Resolvers.ResolveMethodInParents(td.BaseType, Weaver.CurrentAssembly, "Deserialize");
                if (baseDeserialize != null)
                {
                    // base
                    serWorker.Append(serWorker.Create(OpCodes.Ldarg_0));
                    // writer
                    serWorker.Append(serWorker.Create(OpCodes.Ldarg_1));
                    serWorker.Append(serWorker.Create(OpCodes.Call, baseDeserialize));
                }
            }

            foreach (FieldDefinition field in td.Fields)
            {
                if (field.IsStatic || field.IsPrivate || field.IsSpecialName)
                    continue;

                MethodReference readerFunc = Readers.GetReadFunc(field.FieldType);
                if (readerFunc != null)
                {
                    serWorker.Append(serWorker.Create(OpCodes.Ldarg_0));
                    serWorker.Append(serWorker.Create(OpCodes.Ldarg_1));
                    serWorker.Append(serWorker.Create(OpCodes.Call, readerFunc));
                    serWorker.Append(serWorker.Create(OpCodes.Stfld, field));
                }
                else
                {
                    Weaver.Error($"{field} has unsupported type");
                    return;
                }
            }
            serWorker.Append(serWorker.Create(OpCodes.Ret));

            //only add if not just replaced body
            if (existingMethod == null)
            {
                td.Methods.Add(serializeFunc);
            }
        }
    }
}
