using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rest.Fody
{
    public sealed partial class ModuleWeaver
    {
        const string ADDRESS = "Address";
        const string HTTP_METHOD = "Method";
        const string PATH = "path";
        const string GENERATED_CLIENT_PATH = "generatedHttpClient";
        const string HEADER = "Header";
    }
}
