using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
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
                            throw new WeavingException("Attributes [Header] on class must have two args in their constructor.");
                        else if (header.Any(x => String.IsNullOrWhiteSpace(x)))
                            throw new WeavingException("Attributes [Header] on class must have two non-null args in their constructor.");

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

        private void AddRestClientMethod(MethodDefinition httpClientGetter, MethodDefinition method, MethodReference httpMethodGetter, string path,
            MethodDefinition serStr, MethodDefinition serBuf,
            MethodDefinition deserStr, MethodDefinition deserBuf)
        {
            Logger.Log($"Generating new method {method.Name}", false);
            
            method.Body.Emit(il =>
            {
                il.Emit(OpCodes.Ldarg_0);                   // this
                il.Emit(OpCodes.Call, httpClientGetter);    // load this.HttpClient onto the stack
                
                il.Emit(OpCodes.Call, httpMethodGetter);// load the static Method property of the attribute
                il.Emit(OpCodes.Ldstr, path);           // load path onto the stack
                il.Emit(OpCodes.Newobj, Proxy_Ctor);    // create proxy

                // add headers
                foreach (var attr in method.GetAttrs<HeaderAttribute>())
                {
                    if (attr.ConstructorArguments.Count == 2)
                    {
                        if (attr.ConstructorArguments.Any(x => x.Value == null))
                            throw WeavingException.AttrValuesCannotBeNull;

                        il.Emit(OpCodes.Ldstr, (string)attr.ConstructorArguments[0].Value);
                        il.Emit(OpCodes.Ldstr, (string)attr.ConstructorArguments[1].Value);
                        il.Emit(OpCodes.Callvirt, Proxy_AddHeader);
                    }
                    else
                        throw WeavingException.AttrValuesOutOfRange(2, "Method");
                }

                if (method.HasParameters)
                    RunMethodParameters(method, il, serStr, serBuf);    // edit request to match parameters

                il.Emit(OpCodes.Callvirt, Proxy_Compile);               // compile proxy to HttpRequestMessage
                il.Emit(OpCodes.Callvirt, HttpClient_SendAsync);        // this.HttpClient.SendAsync()
                RunMethodReturnValue(method, il, deserStr, deserBuf);
                il.Emit(OpCodes.Ret);                                   // return
            });
        }

        private void RunMethodReturnValue(MethodDefinition m, ILProcessor il, MethodDefinition deserStr, MethodDefinition deserBuf)
        {
            // Gotta transform Task<HttpResponseMessage> to Task<T>
            string returnTypeStr = new string(m.FullName.SkipWhile(c => c != '<').Skip(1).TakeWhile(c => c != '>').ToArray());
            Type returnTypeSrc = Type.GetType(returnTypeStr);
            TypeReference returnType = ModuleDefinition.Import(returnTypeSrc);

            if (returnType == null)
                il.Emit(OpCodes.Nop);
            else if (returnType.Is<string>())
                il.Emit(OpCodes.Call, Proxy_GetContentString);
            else if (returnType.Is<Stream>())
                il.Emit(OpCodes.Call, Proxy_GetContentStream);
            else if (returnType.Is<byte[]>())
                il.Emit(OpCodes.Call, Proxy_GetContentByteArray);
            else if (returnType.Is<HttpStatusCode>())
                il.Emit(OpCodes.Call, Proxy_GetStatusCode);
            else if (returnType.Is<HttpResponseMessage>())
                il.Emit(OpCodes.Call, Proxy_GetResponse);
            else if (deserStr != null)
            {
                MethodDefinition cb = new MethodDefinition($"${m.Name}_cb", MethodAttributes.Private, returnType);
                cb.Parameters.Add(new ParameterDefinition(ModuleDefinition.Import(typeof(Task<string>))));
                cb.Body.Emit(i =>
                {
                    var resGetter = ModuleDefinition.Import(typeof(Task<string>).GetMethod("get_Result"));

                    var deser = deserStr.MakeGenericMethod(returnType);
                    if (deserStr.IsStatic)
                    {
                        i.Emit(OpCodes.Ldarg_1);
                        i.Emit(OpCodes.Call, resGetter);
                        i.Emit(OpCodes.Call, deser);
                    }
                    else
                    {
                        i.Emit(OpCodes.Ldarg_0);
                        i.Emit(OpCodes.Ldarg_1);
                        i.Emit(OpCodes.Call, resGetter);
                        i.Emit(OpCodes.Callvirt, deser);
                    }
                    i.Emit(OpCodes.Ret);
                });

                m.DeclaringType.Methods.Add(cb);

                var ctor = ModuleDefinition.Import(typeof(Func<,>).MakeGenericType(typeof(Task<string>), returnTypeSrc)
                    .GetConstructors().First());

                il.Emit(OpCodes.Call, Proxy_GetContentString);  // Task<HttpResponseMessage> -> Task<string>
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldftn, cb);                     // this.cb
                il.Emit(OpCodes.Newobj, ctor);                  // new Func<Task<string>, T>(cb)
                il.Emit(OpCodes.Call, ModuleDefinition.ImportContinueWith(typeof(Task<string>), returnTypeSrc));
            }
            else if (deserBuf != null)
            {
                MethodDefinition cb = new MethodDefinition($"${m.Name}_cb", MethodAttributes.Private, returnType);
                cb.Parameters.Add(new ParameterDefinition(ModuleDefinition.Import(typeof(Task<byte[]>))));
                cb.Body.Emit(i =>
                {
                    var resGetter = ModuleDefinition.Import(typeof(Task<byte[]>).GetMethod("get_Result"));

                    var deser = deserBuf.MakeGenericMethod(returnType);
                    if (deserStr.IsStatic)
                    {
                        i.Emit(OpCodes.Ldarg_1);
                        i.Emit(OpCodes.Call, resGetter);
                        i.Emit(OpCodes.Call, deser);
                    }
                    else
                    {
                        i.Emit(OpCodes.Ldarg_0);
                        i.Emit(OpCodes.Ldarg_1);
                        i.Emit(OpCodes.Call, resGetter);
                        i.Emit(OpCodes.Callvirt, deser);
                    }
                    i.Emit(OpCodes.Ret);
                });

                m.DeclaringType.Methods.Add(cb);

                var ctor = ModuleDefinition.Import(typeof(Func<,>).MakeGenericType(typeof(Task<byte[]>), returnTypeSrc)
                    .GetConstructors().First());

                il.Emit(OpCodes.Call, Proxy_GetContentString);  // Task<HttpResponseMessage> -> Task<string>
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldftn, cb);                     // this.cb
                il.Emit(OpCodes.Newobj, ctor);                  // new Func<Task<string>, T>(cb)
                il.Emit(OpCodes.Call, ModuleDefinition.ImportContinueWith(typeof(Task<byte[]>), returnTypeSrc));
            }
        }

        private void RunMethodParameters(MethodDefinition m, ILProcessor il, MethodDefinition serStr, MethodDefinition serBuf)
        {
            foreach (ParameterDefinition p in m.Parameters)
            {
                byte i = (byte)(p.Index + 1);

                CustomAttribute body = p.GetAttr<BodyAttribute>();
                if (body != null)
                {
                    if (p.ParameterType.Is<string>())
                    {
                        il.Emit(OpCodes.Ldarg_S, i);
                        il.Emit(OpCodes.Callvirt, Proxy_AddBodyStr);
                    }
                    else
                    {
                        if (serStr != null)
                        {
                            if (serStr.IsStatic)
                            {
                                il.Emit(OpCodes.Ldarg_S, i);
                                il.Emit(OpCodes.Box, p.ParameterType);
                                il.Emit(OpCodes.Call, serStr);
                            }
                            else
                            {
                                il.Emit(OpCodes.Ldarg_0);
                                il.Emit(OpCodes.Ldarg_S, i);
                                il.Emit(OpCodes.Box, p.ParameterType);
                                il.Emit(OpCodes.Callvirt, serStr);
                            }
                            il.Emit(OpCodes.Callvirt, Proxy_AddBodyStr);
                        }
                        else if (serBuf != null)
                        {
                            if (serBuf.IsStatic)
                            {
                                il.Emit(OpCodes.Ldarg_S, i);
                                il.Emit(OpCodes.Box, p.ParameterType);
                                il.Emit(OpCodes.Call, serBuf);
                            }
                            else
                            {
                                il.Emit(OpCodes.Ldarg_0);
                                il.Emit(OpCodes.Ldarg_S, i);
                                il.Emit(OpCodes.Box, p.ParameterType);
                                il.Emit(OpCodes.Callvirt, serBuf);
                            }
                            il.Emit(OpCodes.Callvirt, Proxy_AddBodyBuf);
                        }
                        else
                            throw new WeavingException($"No serialization method specified by [{nameof(RestSerializerAttribute)}].");
                    }

                    continue;
                }

                CustomAttribute headers = p.GetAttr<HeadersAttribute>();
                if (headers != null)
                {
                    if (!p.ParameterType.Is<IDictionary<string, object>>(true))
                        throw new WeavingException($"Expected {nameof(IDictionary<string, object>)} but got {p.ParameterType}");

                    il.Emit(OpCodes.Ldarg_S, i);
                    il.Emit(OpCodes.Box, p.ParameterType);
                    il.Emit(OpCodes.Callvirt, Proxy_AddHeaders);

                    continue;
                }

                CustomAttribute header = p.GetAttr<HeaderAttribute>();
                if (header != null)
                {
                    if (header.ConstructorArguments.Count != 1)
                        throw WeavingException.AttrValuesOutOfRange(1, "Parameter");
                    if (header.ConstructorArguments[0].Value == null)
                        throw WeavingException.AttrValuesCannotBeNull;

                    il.Emit(OpCodes.Ldstr, (string)header.ConstructorArguments[0].Value);
                    il.Emit(OpCodes.Ldarg_S, i);
                    il.Emit(OpCodes.Box, p.ParameterType);
                    il.Emit(OpCodes.Callvirt, Proxy_AddHeader);

                    continue;
                }

                CustomAttribute query = p.GetAttr<QueryAttribute>();
                if (query != null)
                {
                    if (query.ConstructorArguments[0].Value == null)
                        throw WeavingException.AttrValuesCannotBeNull;

                    il.Emit(OpCodes.Ldstr, (string)query.ConstructorArguments[0].Value);
                    il.Emit(OpCodes.Ldarg_S, i);
                    il.Emit(OpCodes.Box, p.ParameterType);
                    il.Emit(OpCodes.Callvirt, Proxy_AddQuery);

                    continue;
                }
                
                string argName = p.Name;

                CustomAttribute alias = p.GetAttr<AliasAttribute>();
                if (alias != null)
                {
                    if (alias.ConstructorArguments.Any(x => x.Value == null))
                        throw WeavingException.AttrValuesCannotBeNull;

                    argName = (string)alias.ConstructorArguments[0].Value;
                }

                il.Emit(OpCodes.Ldstr, argName);
                il.Emit(OpCodes.Ldarg_S, i);
                il.Emit(OpCodes.Box, p.ParameterType);
                il.Emit(OpCodes.Callvirt, Proxy_AddPathArg);
            }
        }
    }
}
