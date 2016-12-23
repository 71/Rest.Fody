using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Rest.Fody.Helpers;
using Rest.Fody.Weaving;

namespace Rest.Fody
{
    public sealed partial class ModuleWeaver
    {
        /// <summary>
        /// Will log a MessageImportance.Normal message to MSBuild.
        /// </summary>
        public Action<string> LogDebug { get; set; } = m => { };

        /// <summary>
        /// Will log a MessageImportance.High message to MSBuild.
        /// </summary>
        public Action<string> LogInfo { get; set; } = m => { };

        /// <summary>
        /// Will log a MessageImportance.Warning message to MSBuild.
        /// </summary>
        public Action<string> LogWarning { get; set; } = m => { };

        /// <summary>
        /// Will log an error message to MSBuild.
        /// </summary>
        public Action<string> LogError { get; set; } = m => { };

        /// <summary>
        /// Will contain the full XML from FodyWeavers.xml.
        /// </summary>
        public XElement Config { get; set; }
        
        /// <summary>
        ///An instance of Mono.Cecil.ModuleDefinition for processing.
        /// </summary>
        public ModuleDefinition ModuleDefinition { get; set; }
        
        /// <summary>
        ///  An instance of Mono.Cecil.IAssemblyResolver for resolving assembly references.
        /// </summary>
        public IAssemblyResolver AssemblyResolver { get; set; }
        
        /// <summary>
        /// Will contain the full path of the target assembly.
        /// </summary>
        public string AssemblyFilePath { get; set; }
        
        /// <summary>
        /// Will contain a semicomma delimetered string that contains 
        /// all the references for the target project. 
        /// A copy of the contents of the @(ReferencePath).
        /// </summary>
        public string References { get; set; }

        public Assembly ExecutingAssembly { get; set; }
        public Assembly ReferencedAssembly { get; set; }

        public ModuleWeaver()
        {
            ExecutingAssembly = Assembly.GetExecutingAssembly();
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
            // load logger
            Logger = new Logger(LogDebug, LogInfo);

            // options
            WeavingOptions opts = new WeavingOptions();
            foreach (XAttribute attr in Config.Attributes())
            {
                if (attr.Name == "AddHeadersToAlreadyExistingHttpClient")
                    opts.AddHeadersToAlreadyExistingHttpClient = attr.Value.Equals("true", StringComparison.InvariantCultureIgnoreCase);
                else if (attr.Name == "ThrowRestExceptionOnInternetError")
                    opts.ThrowRestExceptionOnInternetError = attr.Value.Equals("true", StringComparison.InvariantCultureIgnoreCase);
            }

            Logger.Log(opts.ToString(), true);

            var Proxy_ThrowOnError = ModuleDefinition.ImportField<AsyncProxy>(nameof(AsyncProxy.ThrowOnError));

            ModuleDefinition.ImportType<AsyncProxy>().Resolve().Methods.First(x => x.IsConstructor).Body.EmitToBeginning(
                Instruction.Create(opts.ThrowRestExceptionOnInternetError ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0),
                Instruction.Create(OpCodes.Stsfld, Proxy_ThrowOnError)
            );

            Weaver.ClassWeaver.Options = Weaver.MethodWeaver.Options = opts;
            Weaver.ClassWeaver.Module = Weaver.MethodWeaver.Module = ModuleDefinition;

            Weaver.ClassWeaver.ImportNecessaryReferences();
            Weaver.MethodWeaver.ImportNecessaryReferences();

            // stats
            Weaver.ClassWeaver.RegisteredClass += t =>
            {
                ModifiedTypes++;
            };

            Weaver.MethodWeaver.RegisteredMethod += m =>
            {
                ModifiedMethods++;
            };
            

            // actual execution
            try
            {
#if DEBUG
                foreach (var type in ModuleDefinition.GetTypes())
                    Weaver.ClassWeaver.RunType(type);
#else
                Parallel.ForEach(ModuleDefinition.GetTypes(), cw.RunType);
#endif
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

        public void AfterWeaving()
        {
            Logger.Log("Weaving successful!");
            Logger.Log($"Changed {ModifiedTypes} classes, and created {ModifiedMethods} methods.", true);
        }
    }
}
