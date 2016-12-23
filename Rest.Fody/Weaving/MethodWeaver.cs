using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Rest.Fody.Helpers;
using SR = System.Reflection;

namespace Rest.Fody.Weaving
{
    internal sealed class MethodWeaver : Weaver
    {
        #region References
        // types
        private TypeDefinition Reactive_TaskObservableExtensionsDef;

        // methods
        private MethodReference HttpClient_SendAsync;
        private MethodReference CancellationToken_None;
        private MethodReference Task_Start;

        // proxy
        private TypeReference ProxyRef;
        private MethodReference Proxy_Ctor;
        private MethodReference Proxy_Compile;
        private MethodReference Proxy_AddHeader;
        private MethodReference Proxy_AddHeaders;
        private MethodReference Proxy_AddQuery;
        private MethodReference Proxy_AddPathArg;
        private MethodReference Proxy_AddBodyStr;
        private MethodReference Proxy_AddBodyBuf;

        private MethodReference Proxy_GetContentString;
        private MethodReference Proxy_GetContentStream;
        private MethodReference Proxy_GetContentByteArray;
        private MethodReference Proxy_GetResponse;
        private MethodReference Proxy_GetStatusCode;

        // (de)serializer
        private MethodDefinition SerializeStr;
        private MethodDefinition SerializeBuf;
        private MethodDefinition DeserializeStr;
        private MethodDefinition DeserializeBuf;

        public override void ImportNecessaryReferences()
        {
            // types
            ProxyRef = Module.ImportType<MessageProxy>();

            TypeReference TaskObservableExt;
            if (Module.TryGetTypeReference("System.Reactive.Threading.Tasks.TaskObservableExtensions", out TaskObservableExt))
                Reactive_TaskObservableExtensionsDef = TaskObservableExt.Resolve();

            // ctor
            Proxy_Ctor = Module.ImportCtor<MessageProxy>(typeof(HttpMethod), typeof(string));

            // method
            HttpClient_SendAsync = Module.ImportMethod<HttpClient>(nameof(HttpClient.SendAsync), typeof(HttpRequestMessage), typeof(CancellationToken));
            CancellationToken_None = Module.ImportMethod<CancellationToken>("get_None");
            Task_Start = Module.ImportMethod<Task>("Start");

            // proxy methods
            Proxy_Compile = Module.ImportMethod<MessageProxy>(nameof(MessageProxy.Compile));
            Proxy_AddHeader = Module.ImportMethod<MessageProxy>(nameof(MessageProxy.AddHeader), typeof(string), typeof(object));
            Proxy_AddHeaders = Module.ImportMethod<MessageProxy>(nameof(MessageProxy.AddHeaders), typeof(IDictionary<string, object>));
            Proxy_AddQuery = Module.ImportMethod<MessageProxy>(nameof(MessageProxy.AddQuery), typeof(string), typeof(object));
            Proxy_AddPathArg = Module.ImportMethod<MessageProxy>(nameof(MessageProxy.AddPathArg), typeof(string), typeof(object));
            Proxy_AddBodyStr = Module.ImportMethod<MessageProxy>(nameof(MessageProxy.AddBody), typeof(string));
            Proxy_AddBodyBuf = Module.ImportMethod<MessageProxy>(nameof(MessageProxy.AddBody), typeof(byte[]));

            Proxy_GetContentString = Module.ImportMethod<AsyncProxy>(nameof(AsyncProxy.CallString), typeof(Task<HttpResponseMessage>));
            Proxy_GetContentByteArray = Module.ImportMethod<AsyncProxy>(nameof(AsyncProxy.CallByteArray), typeof(Task<HttpResponseMessage>));
            Proxy_GetContentStream = Module.ImportMethod<AsyncProxy>(nameof(AsyncProxy.CallStream), typeof(Task<HttpResponseMessage>));
            Proxy_GetResponse = Module.ImportMethod<AsyncProxy>(nameof(AsyncProxy.CallResponse), typeof(Task<HttpResponseMessage>));
            Proxy_GetStatusCode = Module.ImportMethod<AsyncProxy>(nameof(AsyncProxy.CallStatusCode), typeof(Task<HttpResponseMessage>));

            // some imports
            Utils.FindDeserializeMethods(Module.GetTypes().SelectMany(x => x.Methods).Where(x => x.IsStatic),
                ref SerializeStr, ref SerializeBuf, ref DeserializeStr, ref DeserializeBuf);
        }
        #endregion

        public event Action<MethodDefinition> RegisteredMethod;

        /// <summary>
        /// If a method is marked extern, transform it into a full method.
        /// </summary>
        public void RunMethod(MethodDefinition method, MethodDefinition httpClientGetter)
        {

            string relativePath;
            MethodReference httpMethodGetter = null;

            MethodDefinition serStr = SerializeStr,
                             serBuf = SerializeBuf,
                             deserStr = DeserializeStr,
                             deserBuf = DeserializeBuf;
            
            if (TryGetHttpMethod(method, out relativePath, out httpMethodGetter))
            {
                // find local serializers
                Utils.FindDeserializeMethods(method.DeclaringType.Methods.Where(x => !x.IsStatic),
                    ref serStr, ref serBuf, ref deserStr, ref deserBuf);

                Logger.Region("GENERATING METHOD", () => AddRestClientMethod(httpClientGetter, method, httpMethodGetter, relativePath, serStr, serBuf, deserStr, deserBuf));
                RegisteredMethod?.Invoke(method);
            }
        }

        /// <summary>
        /// Try getting a method's [HttpMethod] attribute.
        /// </summary>
        private bool TryGetHttpMethod(MethodDefinition m, out string path, out MethodReference httpMethodGetter)
        {
            path = null;
            httpMethodGetter = null;

            CustomAttribute a = m.GetAttr<HttpMethodAttribute>();

            if (a == null)
                return false;

            if (m.RVA != 0) // not extern
                throw m.Message("Expected method to be extern.");

            var prop = a.AttributeType.Resolve().Properties.First(x => x.Name == "Method");
            httpMethodGetter = Module.Import(prop.GetMethod);

            path = a.HasConstructorArguments
                ? a.ConstructorArguments[0].Value as string
                : null;

            if (path == null)
                throw m.Message("Attribute parameters cannot be null.");

            return true;
        }

        /// <summary>
        /// Transform an extern method to an api method.
        /// </summary>
        private void AddRestClientMethod(MethodDefinition httpClientGetter, MethodDefinition method, MethodReference httpMethodGetter, string path,
            MethodDefinition serStr, MethodDefinition serBuf,
            MethodDefinition deserStr, MethodDefinition deserBuf)
        {
            Logger.Log($"Generating new method {method.DeclaringType.Name}.{method.Name}", false);

            method.Body.Emit(il =>
            {
                if (!method.ReturnType.Is<HttpRequestMessage>())
                {
                    il.Emit(OpCodes.Ldarg_0);                   // this
                    il.Emit(OpCodes.Call, httpClientGetter);    // load this.HttpClient onto the stack
                }
                il.Emit(OpCodes.Call, httpMethodGetter);    // load the static Method property of the attribute
                il.Emit(OpCodes.Ldstr, path);               // load path onto the stack
                il.Emit(OpCodes.Newobj, Proxy_Ctor);        // create proxy

                // add headers
                foreach (var attr in method.GetAttrs<HeaderAttribute>())
                {
                    if (attr.ConstructorArguments.Count == 2)
                    {
                        if (attr.ConstructorArguments.Any(x => x.Value == null))
                            throw httpClientGetter.Message("Attribute parameters cannot be null.");

                        il.Emit(OpCodes.Ldstr, (string)attr.ConstructorArguments[0].Value);
                        il.Emit(OpCodes.Ldstr, (string)attr.ConstructorArguments[1].Value);
                        il.Emit(OpCodes.Callvirt, Proxy_AddHeader);
                    }
                    else
                        throw httpClientGetter.Message($"Expected 2 parameters, but got {attr.ConstructorArguments.Count} instead.");
                }

                byte tokenIndex = 0;

                if (method.HasParameters)
                    RunParameters(method, il, serStr, serBuf, out tokenIndex);  // edit request to match parameters (query, alias, body, etc)
                
                il.Emit(OpCodes.Callvirt, Proxy_Compile);               // compile proxy to HttpRequestMessage
                if (method.ReturnType.Is<HttpRequestMessage>())
                {
                    il.Emit(OpCodes.Ret);
                    return;
                }

                if (tokenIndex > 0)
                    il.Emit(OpCodes.Ldarg_S, tokenIndex);               // if provided, pass the cancellation token
                else
                    il.Emit(OpCodes.Call, CancellationToken_None);      // else, pass CancellationToken.None
                il.Emit(OpCodes.Callvirt, HttpClient_SendAsync);        // this.HttpClient.SendAsync()

                RunReturnValue(method, il, deserStr, deserBuf);         // make sure we return TTask<T>, and not Task<HttpResponseMessage>
                il.Emit(OpCodes.Ret);                                   // return
            });
        }

        /// <summary>
        /// Make sure we return <see cref="Task{TResult}"/> or <see cref="IObservable{T}"/>.
        /// </summary>
        private void RunReturnValue(MethodDefinition m, ILProcessor il, MethodDefinition deserStr, MethodDefinition deserBuf)
        {
            if (!m.ReturnType.Is<Task>(true)
                && !m.ReturnType.Is(typeof(IObservable<>), true)
                && !m.ReturnType.Is<HttpRequestMessage>())
                throw m.Message("Return type must be HttpRequestMessage, Task, Task<T> or IObservable<T>.");
            
            TypeReference returnType = (m.ReturnType as GenericInstanceType)?.GenericArguments[0];

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
            else
            {
                GenericInstanceMethod deser;
                bool isStatic;
                GenericInstanceType genType;
                MethodReference resGetter;
                MethodReference contentGetter;

                if (deserStr != null)
                {
                    deser = deserStr.MakeGenericMethod(returnType);
                    isStatic = deserStr.IsStatic;
                    genType =
                        Module.GetReference("System.Threading.Tasks.Task`1")
                              .MakeGenericInstanceType(Module.TypeSystem.String);
                    contentGetter = Proxy_GetContentString;
                    resGetter = Module.ImportGetter<Task<string>, string>(x => x.Result);
                }
                else if (deserBuf != null)
                {
                    deser = deserBuf.MakeGenericMethod(returnType);
                    isStatic = deserBuf.IsStatic;
                    genType =
                        Module.GetReference("System.Threading.Tasks.Task`1")
                              .MakeGenericInstanceType(Module.TypeSystem.Byte.MakeArrayType());
                    contentGetter = Proxy_GetContentByteArray;
                    resGetter = Module.ImportGetter<Task<byte[]>, byte[]>(x => x.Result);
                }
                else
                    throw m.Message("No serializer / deserializer found.");

                MethodDefinition cb = new MethodDefinition($"${m.Name}_cb", MethodAttributes.Private, returnType);
                cb.Parameters.Add(new ParameterDefinition(Module.Import(genType)));
                cb.Body.Emit(i =>
                {
                    if (isStatic)
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

                var funcType = Module.Import(Module.ImportType(typeof(Func<,>))
                                                   .MakeGenericType(genType, returnType));

                var ctor = Module.Import(typeof(Func<,>)
                                 .GetConstructor(new[] { typeof(object), typeof(IntPtr) }));


                MethodReference continueWith = Module
                    .Import(typeof(Task<>)
                    .GetMethods().First(x => x.Name == "ContinueWith"
                                          && x.GetGenericArguments().Length == 1
                                          && x.GetParameters().Length == 1));

                ctor.DeclaringType = funcType;
                continueWith.DeclaringType = genType;

                continueWith = Module.Import(continueWith).MakeGenericMethod(returnType);
                
                il.Emit(OpCodes.Call, contentGetter);           // Task<HttpResponseMessage> -> Task<string> / Task<byte[]>
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldftn, cb);                     // this.cb
                il.Emit(OpCodes.Newobj, ctor);                  // new Func<Task<...>, T>(cb)
                il.Emit(OpCodes.Callvirt, continueWith);        // task.ContinueWith(...)
            }

            // handle IObservable
            if (m.ReturnType.Name == "IObservable`1")
            {
                if (Reactive_TaskObservableExtensionsDef == null)
                    throw m.Message("Cannot find type System.Reactive.Threading.Tasks.TaskObservableExtensions.");
                
                il.Emit(OpCodes.Call, Module.ImportToObservable(Reactive_TaskObservableExtensionsDef, returnType));
            }
        }

        /// <summary>
        /// Transform every parameters of a method to properties for the newly created <see cref="HttpRequestMessage"/>.
        /// </summary>
        private void RunParameters(MethodDefinition m, ILProcessor il, MethodDefinition serStr, MethodDefinition serBuf, out byte cancellationTokenIndex)
        {
            cancellationTokenIndex = byte.MinValue;

            foreach (ParameterDefinition p in m.Parameters)
            {
                byte i = (byte)(p.Index + 1);

                if (p.ParameterType.Is<CancellationToken>())
                {
                    if (cancellationTokenIndex > 0)
                        throw m.Message("Only one CancellationToken can be provided.");

                    cancellationTokenIndex = i;
                    continue;
                }

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
                            throw m.Message($"No serialization method specified by [{nameof(RestSerializerAttribute)}].");
                    }

                    continue;
                }

                CustomAttribute headers = p.GetAttr<HeadersAttribute>();
                if (headers != null)
                {
                    if (!p.ParameterType.Is<IDictionary<string, object>>(true))
                        throw m.Message($"Expected {nameof(IDictionary<string, object>)} but got {p.ParameterType}");

                    il.Emit(OpCodes.Ldarg_S, i);
                    il.Emit(OpCodes.Box, p.ParameterType);
                    il.Emit(OpCodes.Callvirt, Proxy_AddHeaders);

                    continue;
                }

                CustomAttribute header = p.GetAttr<HeaderAttribute>();
                if (header != null)
                {
                    if (header.ConstructorArguments.Count != 1)
                        throw m.Message($"[Header] expected a single parameter, but got {header.ConstructorArguments.Count} instead.");
                    if (header.ConstructorArguments[0].Value == null)
                        throw m.Message($"Parameters for [Header] mustn't be null.");

                    il.Emit(OpCodes.Ldstr, (string)header.ConstructorArguments[0].Value);
                    il.Emit(OpCodes.Ldarg_S, i);
                    il.Emit(OpCodes.Box, p.ParameterType);
                    il.Emit(OpCodes.Callvirt, Proxy_AddHeader);

                    continue;
                }

                CustomAttribute query = p.GetAttr<QueryAttribute>();
                if (query != null)
                {
                    string name = query.HasConstructorArguments
                        ? (string)query.ConstructorArguments[0].Value
                        : p.Name.TrimStart('@');

                    il.Emit(OpCodes.Ldstr, name);
                    il.Emit(OpCodes.Ldarg_S, i);
                    il.Emit(OpCodes.Box, p.ParameterType);
                    il.Emit(OpCodes.Callvirt, Proxy_AddQuery);

                    continue;
                }

                string argName = p.Name.TrimStart('@');

                CustomAttribute alias = p.GetAttr<AliasAttribute>();
                if (alias != null)
                {
                    if (alias.ConstructorArguments.Any(x => x.Value == null))
                        throw m.Message($"Parameters for [Alias] mustn't be null.");

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
