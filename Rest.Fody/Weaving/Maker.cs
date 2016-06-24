using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Rest.Fody.Weaving;

namespace Rest.Fody
{
    public sealed partial class ModuleWeaver
    {
        private MethodDefinition MakeHttpClientGetter(TypeDefinition def, Uri baseAddr, IEnumerable<string[]> headers)
        {
            Logger.Log("Generating new HttpClient field", false);
            return Logger.Log("CREATING HTTPCLIENT", new Func<MethodDefinition>(() =>
            {
                // create holding field
                FieldDefinition field = new FieldDefinition($"${GENERATED_CLIENT_PATH}", FieldAttributes.Private, HttpClientRef);
                def.Fields.Add(field);
                
                Logger.Log("Generating field getter", false);

                var getter = new MethodDefinition($"get_$${GENERATED_CLIENT_PATH}", MethodAttributes.Private, HttpClientRef);
                getter.Body.Emit(il =>
                {
                    il.Emit(OpCodes.Ldarg_0);       // this
                    il.Emit(OpCodes.Ldfld, field);  // this.[field] -> stack
                    il.Emit(OpCodes.Ret);           // return stack
                });

                def.Methods.Add(getter);

                // add instructions to create httpclient to constructor
                foreach (var ctor in def.Methods.Where(x => x.IsConstructor))
                {
                    Logger.Log("Generating new HttpClient field initializer in constructor", false);

                    List<Instruction> instructions = new List<Instruction>
                    {
                        Instruction.Create(OpCodes.Ldarg_0),
                        Instruction.Create(OpCodes.Newobj, HttpClient_Ctor),        // create http client
                        Instruction.Create(OpCodes.Dup),

                        Instruction.Create(OpCodes.Ldstr, baseAddr.OriginalString), // load str to stack
                        Instruction.Create(OpCodes.Newobj, Uri_Ctor),               // create uri
                        Instruction.Create(OpCodes.Callvirt, BaseAddress_Set),      // set BaseAddress

                        Instruction.Create(OpCodes.Nop)
                    };
                    
                    foreach (string[] header in headers)
                    {
                        instructions.Add(Instruction.Create(OpCodes.Dup));
                        instructions.Add(Instruction.Create(OpCodes.Callvirt, DefaultHeaders_Get));

                        if (header.Length != 2)
                            throw Ex("Attributes [Header] on class must have two args in their constructor.");
                        else if (header.Any(x => String.IsNullOrWhiteSpace(x)))
                            throw Ex("Attributes [Header] on class must have two non-null args in their constructor.");

                        instructions.Add(Instruction.Create(OpCodes.Ldstr, header[0]));          // push header name to stack
                        instructions.Add(Instruction.Create(OpCodes.Ldstr, header[1]));          // push header value to stack
                        instructions.Add(Instruction.Create(OpCodes.Callvirt, HttpHeaders_Add)); // call Add() on DefaultHttpHeaders

                        instructions.Add(Instruction.Create(OpCodes.Nop));
                    }

                    instructions.Add(Instruction.Create(OpCodes.Stfld, field));

                    ctor.Body.EmitToBeginning(instructions.ToArray());
                }

                return getter;
            }));
        }

        private void AddRestClientMethod(MethodDefinition httpClientGetter, MethodDefinition method, MethodReference httpMethodGetter, string path, IEnumerable<string[]> headers)
        {
            method.Body.Emit(il =>
            {
                il.Emit(OpCodes.Ldarg_0);               // this
                il.Emit(OpCodes.Call, httpClientGetter);// load this.HttpClient onto the stack
                
                il.Emit(OpCodes.Call, httpMethodGetter);            // load the static Method property of the attribute
                il.Emit(OpCodes.Ldstr, path);                       // load path onto the stack
                il.Emit(OpCodes.Newobj, HttpRequestMessage_Ctor);   // load new HttpRequestMessage()
                
                il.Emit(OpCodes.Callvirt, HttpClient_SendAsync);    // this.HttpClient.SendAsync()
                il.Emit(OpCodes.Ret);                               // return result of SendAsync()
            });
        }
    }
}
