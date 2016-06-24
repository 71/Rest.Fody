using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using Mono.Cecil;
using Rest.Fody.Weaving;

namespace Rest.Fody
{
    public sealed partial class ModuleWeaver
    {
        // Will log an MessageImportance.Normal message to MSBuild.
        public Action<string> LogDebug { get; set; } = m => { };

        // Will log an MessageImportance.High message to MSBuild.
        public Action<string> LogInfo { get; set; } = m => { };

        // Will log an warning message to MSBuild.
        public Action<string> LogWarning { get; set; } = m => { };

        // Will log an error message to MSBuild.
        public Action<string> LogError { get; set; } = m => { };

        // Will contain the full element XML from FodyWeavers.xml.
        public XElement Config { get; set; }

        // An instance of Mono.Cecil.ModuleDefinition for processing.
        public ModuleDefinition ModuleDefinition { get; set; }

        private AssemblyDefinition Http;
        private AssemblyDefinition Core;

        // An instance of Mono.Cecil.IAssemblyResolver for resolving assembly references.
        public IAssemblyResolver AssemblyResolver { get; set; }

        public Assembly Assembly { get; set; }
        public AssemblyName Name { get; set; }

        public void DEBUG(string format, params object[] args)
        {
#if DEBUG
            LogInfo(args == null || args.Length == 0 ? format : String.Format(format, args));
#endif
        }

        public ModuleWeaver()
        {
            Assembly = this.GetType().GetTypeInfo().Assembly;
            Name = Assembly.GetName();
        }

        public void AfterWeaving()
        {
            DEBUG("Write successful!");
        }

        private static IEnumerable<TypeDefinition> GetAllTypes(IEnumerable<TypeDefinition> types)
        {
            foreach (TypeDefinition type in types)
            {
                yield return type;

                foreach (TypeDefinition nestedType in GetAllTypes(type.NestedTypes))
                    yield return nestedType;
            }
        }

        private TypeReference HttpRequestMessageRef;
        private TypeReference HttpClientRef;
        private TypeReference HttpHeadersRef;
        private TypeReference UriRef;
        private TypeReference HttpMethodRef;

        private MethodReference HttpClient_Ctor;
        private MethodReference Uri_Ctor;
        private MethodReference HttpRequestMessage_Ctor;

        private MethodReference BaseAddress_Set;
        private MethodReference DefaultHeaders_Get;
        private MethodReference Method_Get;

        private MethodReference HttpHeaders_Add;
        private MethodReference HttpClient_SendAsync;

        private Logger Logger;
        private int ModifiedTypes = 0;
        private int ModifiedMethods = 0;


        private void Import()
        {
            Logger.Log("Resolving scopes.", false);
            Http = AssemblyResolver.Resolve("System.Net.Http");
            Core = AssemblyResolver.Resolve("System");
            if (Http == null || Core == null)
                throw new WeavingException("Couldn't find System.Net.Http and/or System. Aborting.");


            Logger.Log("Importing references.", false);
            HttpClientRef = ModuleDefinition.ImportType<HttpClient>(Http.MainModule);
            HttpRequestMessageRef = ModuleDefinition.ImportType<HttpRequestMessage>(Http.MainModule);
            HttpHeadersRef = ModuleDefinition.ImportType<HttpHeaders>(Http.MainModule);
            HttpMethodRef = ModuleDefinition.ImportType<HttpMethod>(Http.MainModule);
            UriRef = ModuleDefinition.ImportType<Uri>(Core.MainModule);
            

            Logger.Log("Importing constructors.", false);
            HttpClient_Ctor = ModuleDefinition.ImportCtor<HttpClient>();
            HttpRequestMessage_Ctor = ModuleDefinition.ImportCtor<HttpRequestMessage>(typeof(HttpMethod), typeof(string));
            Uri_Ctor = ModuleDefinition.ImportCtor<Uri>(typeof(string));


            Logger.Log("Importing properties.", false);
            BaseAddress_Set = ModuleDefinition.ImportSetter<HttpClient, Uri>(x => x.BaseAddress);
            DefaultHeaders_Get = ModuleDefinition.ImportGetter<HttpClient, HttpRequestHeaders>(x => x.DefaultRequestHeaders);
            Method_Get = ModuleDefinition.ImportGetter<HttpMethod, string>(x => x.Method);


            Logger.Log("Importing methods.", false);
            HttpHeaders_Add = ModuleDefinition.ImportMethod<HttpHeaders>("Add", typeof(string), typeof(string));
            HttpClient_SendAsync = ModuleDefinition.ImportMethod<HttpClient>("SendAsync", typeof(HttpRequestMessage));
        }

        public void ExecuteTest()
        {
            Logger = new Logger(LogDebug, LogInfo);
            Import();

            // IMPORTING VIA MSCORLIB: NOT WORKING.
            //var mscorlib = ModuleDefinition.AssemblyReferences.First(x => x.Name == "mscorlib");
            //
            //var httpClient = new TypeReference("System.Net.Http", "HttpClient", ModuleDefinition, mscorlib);
            //httpClient = ModuleDefinition.Import(httpClient);

            // IMPORTING VIA ASSEMBLY RESOLVER:
            var net = AssemblyResolver.Resolve("System.Net.Http");
            var httpClient = new TypeReference("System.Net.Http", "HttpClient", ModuleDefinition, Http.MainModule);
            Logger.Log(typeof(HttpClient).Namespace + '.' + typeof(HttpClient).Name);

            TypeDefinition type = ModuleDefinition.Types.First(x => x.Name == "GoodClient");
            var fld = new FieldDefinition("Something", Mono.Cecil.FieldAttributes.Private, HttpClientRef);
            type.Fields.Add(fld);

            //throw new WeavingException("Shutdown test build");
        }

        // * Scan types for a [Service] or [ServiceFor] attribute
        // * Scan type for a [RestClient] marked HttpClient
        //   * If it exists, use it.
        //   * If it doesn't exist but a path is given by [ServiceFor], create it.
        //   * Else, throw.
        // * Scan for header attributes
        // * Scan two methods: T Deserialize<T>(string src) and string Serialize<T>(T obj)
        // * Scan extern methods marked with an attribute which inherits [HttpMethodAttribute]
        //   * If their attribute is valid, create them.
        //   * Else, throw.
        public void Execute()
        {
            // trying whether or not HttpClient == HttpClient

            Logger = new Logger(LogDebug, LogInfo);
            Logger.Log("IMPORTING", Import);
            Logger.Log("Imported all necessary types, methods, properties and fields.", false);

            try
            {
                Logger.Log("Started weaving.");
                Logger.Log("WEAVING", () =>
                {
                    Logger.Log("Checking all types for a [Service] or [ServiceFor] attribute.");
                    
                    Uri baseAddress = null;
                    foreach (TypeDefinition t in ModuleDefinition.GetTypes().Where(x => TryGetServiceAttr(x, out baseAddress)))
                    {
                        Logger.Log($"Processing type: {t.Name}");

                        // valid type, try to find a client
                        MethodDefinition httpClientGetter = null;

                        if (TryGetRestClientAttr(t, out httpClientGetter)) // http client given
                        {
                            if (baseAddress != null)
                                Logger.Log($"[{t.FullName}] Both BaseAddress and RestClient were given ; using RestClient.", true);
                        }
                        else if (baseAddress != null) // base address, gotta generate http client
                        {
                            Queue<string[]> headers = new Queue<string[]>();

                            foreach (CustomAttribute a in t.GetAttrs<HeaderAttribute>())
                            {
                                string[] header = a.ConstructorArguments.Select(x => x.Value as string).ToArray();

                                if (header == null)
                                    throw Ex(ThrowReason.Null, "Type Header");
                                else
                                    headers.Enqueue(header);
                            }

                            httpClientGetter = MakeHttpClientGetter(t, baseAddress, headers);
                        }
                        else // no http client & no base address
                        {
                            throw Ex(ThrowReason.NoClientNorAddress);
                        }

                        Logger.Log($"Adding methods for type {t.Name}", false);

                        // http client getter exists ; gotta scan all methods for attributes
                        string relativePath = null;
                        MethodReference httpMethodGetter = null;
                        foreach (MethodDefinition method in t.Methods.Where(x => IsValidHttpMethod(x, out relativePath, out httpMethodGetter)))
                        {
                            Logger.Log($"Creating method: {t.Name}.{method.Name}");

                            Queue<string[]> headers = new Queue<string[]>();
                            foreach (CustomAttribute a in method.GetAttrs<HeaderAttribute>())
                            {
                                string[] header = a.ConstructorArguments.Select(x => x.Value as string).ToArray();

                                if (header == null)
                                    throw Ex(ThrowReason.Null, "Method [HeaderAttribute]");
                                else
                                    headers.Enqueue(header);
                            }

                            Logger.Log("GENERATING METHOD", () => AddRestClientMethod(httpClientGetter, method, httpMethodGetter, relativePath, headers));

                            ModifiedMethods++;
                            Logger.Log($"Done creating extern method {t.Name}.{method.Name}.");
                        }

                        ModifiedTypes++;
                        Logger.Log($"Done adding methods for type {t.Name}.");
                    }
                });
            }
            catch (WeavingException)
            {
                throw;
            }
#if DEBUG
            catch (Exception)
            {
                LogError($"Current task: {Logger.Step}");
                throw;
            }
#endif
        }

        private static void AnalyzeObject(object o, Logger l)
        {
#if DEBUG
            if (o == null)
            {
                l.Log("Given object is null");
                return;
            }

            l.Log($"ANALYZING {o.GetType().Name}", () =>
            {
                var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                foreach (MethodInfo i in o.GetType().GetMethods(flags).Where(x => x.Name.StartsWith("set_") || x.Name.StartsWith("get_")))
                    l.Log($"{i.Name}()");
                foreach (PropertyInfo i in o.GetType().GetProperties(flags))
                    l.Log($"{i.Name} {{{(i.GetMethod != null ? " get;" : "")} {(i.SetMethod != null ? "set; " : "")}}}{(i.GetMethod != null ? $" = {i.GetValue(o) ?? "null"}" : "")}");
                foreach (FieldInfo i in o.GetType().GetFields(flags))
                    l.Log($"{i.Name} = {i.GetValue(o) ?? "null"};");
                l.Log($"Done analyzing {o.GetType().Name}");
            });
#endif
        }
    }
}
