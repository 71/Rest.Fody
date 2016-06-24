using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Mono.Cecil;
using Rest.Fody.Weaving;

namespace Rest.Fody
{
    public sealed partial class ModuleWeaver
    {
        private bool TryGetServiceAttr(TypeDefinition t, out Uri uri)
        {
            uri = null;

            if (t.Name == "<Module>")
                return false;

            CustomAttribute a = t.GetAttr<ServiceForAttribute>();

            if (a == null)
            {
                a = t.GetAttr<ServiceAttribute>();

                if (a == null)
                    return false;
                
                return true;
            }

            string addr = a.ConstructorArguments[0].Value as string;

            if (addr != null && Uri.TryCreate(addr, UriKind.Absolute, out uri)) // address provided
            {
                return true;
            }
            else // address provided, but invalid
            {
                throw Ex(ThrowReason.InvalidAddress, addr);
            }
        }

        private bool TryGetRestClientAttr(TypeDefinition t, out MethodDefinition getter)
        {
            getter = null;
            PropertyDefinition p = (from prop in t.Properties
                                    let attr = prop.GetAttr<RestClientAttribute>()
                                    where attr != null
                                    select prop).FirstOrDefault();

            if (p == null)
                return false;

            if (!p.PropertyType.Is<HttpClient>())
                throw Ex(ThrowReason.InvalidRestClientAttrValue);

            getter = p.GetMethod;
            return true;
        }

        private bool IsValidHttpMethod(MethodDefinition m, out string path, out MethodReference httpMethodGetter)
        {
            path = null;
            httpMethodGetter = null;

            CustomAttribute a = m.GetAttr<HttpMethodAttribute>();

            if (a == null)
                return false;

            if (m.RVA != 0) // not extern
                throw Ex(ThrowReason.ExpectExternMethod);

            var prop = a.AttributeType.Resolve().Properties.First(x => x.Name == "Method");
            httpMethodGetter = ModuleDefinition.Import(prop.GetMethod);

            path = a.HasConstructorArguments
                ? a.ConstructorArguments[0].Value as string
                : null;

            if (path == null)
                throw Ex(ThrowReason.Null, "HttpMethodAttribute");

            return true;
        }
    }
}
