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
using TinyIoC;

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

        private TinyIoCContainer Container
        {
            get { return TinyIoCContainer.Current; }
        }

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
            Container.Register(Logger);


            // load assemblies
            var references = References.Split(';');
            var types = new List<Type>();

            using (FileStream fs = Utils.WaitOpenFile(AssemblyFilePath, 1000))
            {
                byte[] data = new byte[fs.Length];
                fs.Read(data, 0, data.Length);

                ReferencedAssembly = Assembly.Load(data);
                Container.Register(ReferencedAssembly, ReferencedAssembly.FullName);

                types.AddRange(ReferencedAssembly.SafeGetTypes());
            }

            foreach (AssemblyName a in ReferencedAssembly.GetReferencedAssemblies())
            {
                Assembly resolved = null;

                try
                {
                    resolved = Assembly.Load(a);
                }
                catch (Exception)
                {
                    using (FileStream fs = Utils.WaitOpenFile(references.First(x => x.Contains(a.Name)), 1000))
                    {
                        byte[] data = new byte[fs.Length];
                        fs.Read(data, 0, data.Length);
                        resolved = Assembly.Load(data);
                    }
                }
                finally
                {
                    Logger.Log($"Loaded assembly {resolved.GetName().Name}");
                    Container.Register(resolved, resolved.FullName);

                    types.AddRange(resolved.SafeGetTypes());
                }
            }

            Container.Register(types.ToArray());
            

            // registration
            Container.Register(ModuleDefinition);

            ClassWeaver cw = new ClassWeaver();
            MethodWeaver mw = new MethodWeaver();

            Container.BuildUp(cw);
            Container.BuildUp(mw);

            cw.ImportNecessaryReferences();
            mw.ImportNecessaryReferences();

            Container.Register(cw);
            Container.Register(mw);
            
            //foreach (var t in ModuleDefinition.GetTypes()) Logger.Log("[TypeDef] " + t.FullName);
            //foreach (var t in ModuleDefinition.GetTypeReferences()) Logger.Log("[TypeRef] " + t.FullName);
            //foreach (var t in ModuleDefinition.GetMemberReferences()) Logger.Log("[Member] " + t.FullName);


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
            Container.Register(opts);

            var Proxy_ThrowOnError = ModuleDefinition.ImportField<AsyncProxy>(nameof(AsyncProxy.ThrowOnError));

            ModuleDefinition.ImportType<AsyncProxy>().Resolve().Methods.First(x => x.IsConstructor).Body.EmitToBeginning(
                Instruction.Create(opts.ThrowRestExceptionOnInternetError ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0),
                Instruction.Create(OpCodes.Stsfld, Proxy_ThrowOnError)
            );

            // stats
            cw.RegisteredClass += (TypeDefinition t) =>
            {
                ModifiedTypes++;
            };

            mw.RegisteredMethod += (MethodDefinition m) =>
            {
                ModifiedMethods++;
            };
            

            // actual execution
            try
            {
                Parallel.ForEach(ModuleDefinition.GetTypes(), cw.RunType);
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
