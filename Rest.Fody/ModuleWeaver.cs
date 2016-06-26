using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Mono.Cecil;
using Rest.Fody.Weaving;
using TinyIoC;

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

        // Will contain the full path of the target assembly.
        public string AssemblyFilePath { get; set; }

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
            // registration
            Container.Register(ModuleDefinition);
            Container.Register(new WeavingOptions { });

            ClassWeaver cw = new ClassWeaver();
            MethodWeaver mw = new MethodWeaver();

            Logger = new Logger(LogDebug, LogInfo);
            Container.Register(Logger);

            Container.BuildUp(cw);
            Container.BuildUp(mw);

            cw.ImportNecessaryReferences();
            mw.ImportNecessaryReferences();

            Container.Register(cw);
            Container.Register(mw);

            // load assemblies
            using (FileStream fs = Utils.WaitOpenFile(AssemblyFilePath, 1000))
            {
                byte[] data = new byte[fs.Length];
                fs.Read(data, 0, data.Length);
                ReferencedAssembly = Assembly.Load(data);
            }

            Container.Register(ReferencedAssembly);
            Container.Register(ReferencedAssembly.GetReferencedAssemblies().Select(a => Assembly.Load(a)).ToArray());
            Container.Register(ReferencedAssembly.SafeGetTypes().Concat(Container.Resolve<Assembly[]>().SelectMany(x => x.SafeGetTypes())).ToArray());


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
