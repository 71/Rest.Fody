using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mono.Cecil;
using TinyIoC;

namespace Rest.Fody
{
    internal abstract class Weaver
    {
        public WeavingOptions Options { get; set; }
        public ModuleDefinition Module { get; set; }
        public Logger Logger { get; set; }

        protected TinyIoCContainer Container
        {
            get { return TinyIoCContainer.Current; }
        }

        public abstract void ImportNecessaryReferences();
    }
}
