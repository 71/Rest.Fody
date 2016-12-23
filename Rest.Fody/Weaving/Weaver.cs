using Mono.Cecil;
using Rest.Fody.Weaving;

namespace Rest.Fody
{
    internal abstract class Weaver
    {
        public static MethodWeaver MethodWeaver = new MethodWeaver();
        public static ClassWeaver ClassWeaver = new ClassWeaver();

        public ModuleDefinition Module { get; set; }
        public WeavingOptions Options { get; set; }

        public Logger Logger => Logger.Instance;

        public abstract void ImportNecessaryReferences();
    }
}
