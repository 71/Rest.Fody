using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Mono.Cecil;
using Rest.Fody.Weaving;

namespace Rest.Fody
{
    public sealed partial class ModuleWeaver
    {
        // types
        private TypeReference HttpRequestMessageRef;
        private TypeReference HttpClientRef;
        private TypeReference HttpHeadersRef;
        private TypeReference UriRef;
        private TypeReference HttpMethodRef;
        private TypeReference ObjectRef;

        // constructors
        private MethodReference HttpClient_Ctor;
        private MethodReference Uri_Ctor;
        private MethodReference HttpRequestMessage_Ctor;

        // properties
        private MethodReference BaseAddress_Set;
        private MethodReference DefaultHeaders_Get;
        private MethodReference Method_Get;

        // methods
        private MethodReference HttpHeaders_Add;
        private MethodReference HttpClient_SendAsync;
        private MethodReference Object_ToString;


        // proxy
        private TypeReference ProxyRef;
        private MethodReference Proxy_Ctor;
        private MethodReference Proxy_Compile;
        private MethodReference Proxy_AddHeader;
        private MethodReference Proxy_AddHeaders;
        private MethodReference Proxy_AddBodyStr;
        private MethodReference Proxy_AddBodyBuf;
        private MethodReference Proxy_AddQuery;
        private MethodReference Proxy_AddPathArg;


        // (de)serializer
        private MethodDefinition SerializeStr;
        private MethodDefinition SerializeBuf;
        private MethodDefinition DeserializeStr;
        private MethodDefinition DeserializeBuf;

        private void Import()
        {
            Logger.Log("Importing references.", false);
            HttpClientRef = ModuleDefinition.ImportType<HttpClient>();
            HttpRequestMessageRef = ModuleDefinition.ImportType<HttpRequestMessage>();
            HttpHeadersRef = ModuleDefinition.ImportType<HttpHeaders>();
            HttpMethodRef = ModuleDefinition.ImportType<HttpMethod>();
            UriRef = ModuleDefinition.ImportType<Uri>();
            ObjectRef = ModuleDefinition.ImportType<object>();


            Logger.Log("Importing constructors.", false);
            HttpClient_Ctor = ModuleDefinition.ImportCtor<HttpClient>();
            HttpRequestMessage_Ctor = ModuleDefinition.ImportCtor<HttpRequestMessage>(typeof(HttpMethod), typeof(string));
            Uri_Ctor = ModuleDefinition.ImportCtor<Uri>(typeof(string));


            Logger.Log("Importing properties.", false);
            BaseAddress_Set = ModuleDefinition.ImportSetter<HttpClient, Uri>(x => x.BaseAddress);
            DefaultHeaders_Get = ModuleDefinition.ImportGetter<HttpClient, HttpRequestHeaders>(x => x.DefaultRequestHeaders);
            Method_Get = ModuleDefinition.ImportGetter<HttpMethod, string>(x => x.Method);


            Logger.Log("Importing methods.", false);
            HttpHeaders_Add = ModuleDefinition.ImportMethod<HttpHeaders>(nameof(HttpHeaders.Add), typeof(string), typeof(string));
            HttpClient_SendAsync = ModuleDefinition.ImportMethod<HttpClient>(nameof(HttpClient.SendAsync), typeof(HttpRequestMessage));
            Object_ToString = ModuleDefinition.ImportMethod<object>(nameof(Object.ToString));


            Logger.Log("Importing everything proxy-related.", false);
            ProxyRef = ModuleDefinition.ImportType<MessageProxy>();
            Proxy_Ctor = ModuleDefinition.ImportCtor<MessageProxy>(typeof(HttpMethod), typeof(string));
            Proxy_Compile = ModuleDefinition.ImportMethod<MessageProxy>(nameof(MessageProxy.Compile));
            Proxy_AddHeader = ModuleDefinition.ImportMethod<MessageProxy>(nameof(MessageProxy.AddHeader), typeof(string), typeof(object));
            Proxy_AddHeaders = ModuleDefinition.ImportMethod<MessageProxy>(nameof(MessageProxy.AddHeaders), typeof(IDictionary<string, object>));
            Proxy_AddBodyStr = ModuleDefinition.ImportMethod<MessageProxy>(nameof(MessageProxy.AddBody), typeof(string));
            Proxy_AddBodyBuf = ModuleDefinition.ImportMethod<MessageProxy>(nameof(MessageProxy.AddBody), typeof(byte[]));
            Proxy_AddQuery = ModuleDefinition.ImportMethod<MessageProxy>(nameof(MessageProxy.AddQuery), typeof(string), typeof(object));
            Proxy_AddPathArg = ModuleDefinition.ImportMethod<MessageProxy>(nameof(MessageProxy.AddPathArg), typeof(string), typeof(object));


            Logger.Log("Importing serializer / deserializer.", false);

            FindDeserializeMethods(ModuleDefinition.GetTypes().SelectMany(x => x.Methods).Where(x => x.IsStatic),
                out SerializeStr, out SerializeBuf, out DeserializeStr, out DeserializeBuf);
        }

        private static void FindDeserializeMethods(IEnumerable<MethodDefinition> collection,
            out MethodDefinition serStr, out MethodDefinition serBuf,
            out MethodDefinition deserStr, out MethodDefinition deserBuf)
        {
            bool serDone = false,
                 deserDone = false;

            serStr = serBuf = deserStr = deserBuf = null;

            foreach (MethodDefinition method in collection)
            {
                if (!deserDone)
                {
                    CustomAttribute deserializeAttr = method.GetAttr<RestDeserializerAttribute>();
                    if (deserializeAttr != null)
                    {
                        if (method.GenericParameters.Count != 1 || method.GenericParameters[0].HasConstraints)
                            throw new WeavingException("A method marked [RestDeserializer] must accept a single unconstrained generic parameter.");

                        GenericParameter T = method.GenericParameters[0];
                        if (method.ReturnType != T)
                            throw new WeavingException("A method marked [RestDeserializer] must return T.");

                        if (method.Parameters[0].ParameterType.Is<string>())
                            deserStr = method;
                        else if (method.Parameters[0].ParameterType.Is<byte[]>())
                            deserBuf = method;
                        else
                            throw new WeavingException("A method marked [RestDeserializer] must accept a single parameter: either string or byte[].");

                        if (serDone) break;
                        deserDone = true;
                        continue;
                    }
                }

                if (!serDone)
                {
                    CustomAttribute serializeAttr = method.GetAttr<RestSerializerAttribute>();
                    if (serializeAttr != null)
                    {
                        if (method.Parameters.Count != 1 || !method.Parameters[0].ParameterType.Is<object>())
                            throw new WeavingException("A method marked [RestSerializer] must accept a single parameter: object.");

                        if (method.ReturnType.Is<string>())
                            serStr = method;
                        else if (method.ReturnType.Is<byte[]>())
                            serBuf = method;
                        else
                            throw new WeavingException("A method marked [RestSerializer] must either return string or byte[].");

                        if (deserDone) break;
                        serDone = true;
                        continue;
                    }
                }

            }
        }
    }
}
