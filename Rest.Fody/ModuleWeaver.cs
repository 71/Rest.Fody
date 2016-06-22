using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using Mono.Cecil;

namespace Rest.Fody
{
    public sealed partial class ModuleWeaver
    {
        // Will log an MessageImportance.Normal message to MSBuild. OPTIONAL
        public Action<string> LogDebug { get; set; } = m => { };

        // Will log an MessageImportance.High message to MSBuild. OPTIONAL
        public Action<string> LogInfo { get; set; } = m => { };

        // Will log an warning message to MSBuild. OPTIONAL
        public Action<string> LogWarning { get; set; } = m => { };

        // Will log an error message to MSBuild.
        public Action<string> LogError { get; set; } = m => { };

        // An instance of Mono.Cecil.ModuleDefinition for processing.
        public ModuleDefinition ModuleDefinition { get; set; }

        public IMetadataScope Scope { get; set; }

        public Assembly Assembly { get; set; }
        public AssemblyName Name { get; set; }

        public ModuleWeaver()
        {
            Assembly = this.GetType().GetTypeInfo().Assembly;
            Name = Assembly.GetName();
        }

        // * Scan types for a [ServiceFor] attribute
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
            Scope = ModuleDefinition.AssemblyReferences
                .Where(x => x.FullName == Assembly.GetName().FullName)
                .Single();

            // check all types for a valid [ServiceFor] attribute.
            ServiceForAttribute attr = null;
            Uri baseAddress = null;
            foreach (TypeDefinition t in ModuleDefinition.Types
                .Where(x => TryGetServiceForAttr(x, out attr, out baseAddress)))
            {
                // valid type, try to find a client
                MethodDefinition httpClientGetter = null;

                if (TryGetRestClientAttr(t, out httpClientGetter)) // http client given
                {
                    if (baseAddress == null)
                        LogInfo("Both BaseAddress and RestClient were given ; using RestClient.");
                }
                else if (baseAddress != null) // base address, gotta generate http client
                {
                    Queue<string> headers = new Queue<string>();

                    foreach (CustomAttribute a in t.CustomAttributes.Where(x => x.AttributeType == Ref<HeaderAttribute>()))
                    {
                        string header = a.Fields.First(x => x.Name == HEADER).Argument.Value as string;
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

                // http client getter exists ; gotta scan all methods for attributes
                HttpMethod httpMethod = null;
                string relativePath = null;
                foreach (MethodDefinition method in t.Methods.Where(x => IsValidHttpMethod(x, out httpMethod, out relativePath)))
                {
                    Queue<string> headers = new Queue<string>();
                    foreach (CustomAttribute a in method.CustomAttributes.Where(x => x.AttributeType == Ref<HeaderAttribute>()))
                    {
                        string header = a.Fields.First(x => x.Name == HEADER).Argument.Value as string;
                        if (header == null)
                            throw Ex(ThrowReason.Null, "Method Header");
                        else
                            headers.Enqueue(header);
                    }

                    AddRestClientMethod(t, httpClientGetter, method, relativePath, httpMethod, headers);
                }
            }
        }
    }
}
