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

        // An instance of Mono.Cecil.IAssemblyResolver for resolving assembly references.
        public IAssemblyResolver AssemblyResolver { get; set; }

        public Assembly Assembly { get; set; }
        public AssemblyName Name { get; set; }

        public ModuleWeaver()
        {
            Assembly = this.GetType().GetTypeInfo().Assembly;
            Name = Assembly.GetName();
        }

        public void AfterWeaving()
        {
            Logger.Log("Weaving successful!");
            Logger.Log($"Changed {ModifiedTypes} classes, and created {ModifiedMethods} methods.", true);
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

        private Logger Logger;
        private int ModifiedTypes = 0;
        private int ModifiedMethods = 0;

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

                        // if we have no mean of deserializing / serializing, try to find one
                        MethodDefinition
                            serStr = SerializeStr,
                            serBuf = SerializeBuf,
                            deserStr = DeserializeStr,
                            deserBuf = DeserializeBuf;

                        FindDeserializeMethods(t.Methods.Where(x => !x.IsStatic), ref serStr, ref serBuf, ref deserStr, ref deserBuf);

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
                                    throw WeavingException.AttrValuesCannotBeNull;
                                else
                                    headers.Enqueue(header);
                            }

                            httpClientGetter = MakeHttpClientGetter(t, baseAddress, headers);
                        }
                        else // no http client & no base address
                        {
                            throw new WeavingException("No base address was given, and no HttpClient marked [RestClient] could be found.");
                        }

                        Logger.Log($"Adding methods for type {t.Name}", false);

                        // http client getter exists ; gotta scan all methods for attributes

                        for (int i = 0; i < t.Methods.Count; i++)
                        {
                            string relativePath = null;
                            MethodReference httpMethodGetter = null;

                            MethodDefinition method = t.Methods[i];
                            if (IsValidHttpMethod(method, out relativePath, out httpMethodGetter))
                            {
                                Logger.Log($"Creating method: {t.Name}.{method.Name}");

                                Logger.Log("GENERATING METHOD", () => AddRestClientMethod(httpClientGetter, method, httpMethodGetter, relativePath, serStr, serBuf, deserStr, deserBuf));

                                ModifiedMethods++;
                                Logger.Log($"Done creating extern method {t.Name}.{method.Name}.");
                            }
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
