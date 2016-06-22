using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Mono.Cecil;

namespace Rest.Fody
{
    public sealed partial class ModuleWeaver
    {
        private TypeReference Ref<T>()
        {
            Type t = typeof(T);
            return new TypeReference(t.Namespace, t.Name, ModuleDefinition, Scope);
        }

        private bool TryGetServiceForAttr(TypeDefinition t, out ServiceForAttribute attr, out Uri uri)
        {
            attr = null;
            uri = null;

            CustomAttribute a = t.CustomAttributes
                .FirstOrDefault(x => x.AttributeType == Ref<ServiceForAttribute>());

            if (a == null)
                return false;

            string addr = a.Properties.First(x => x.Name == ADDRESS).Argument.Value as string;

            if (addr == null) // no address: expect provided HttpClient
            {
                uri = null;
                attr = new ServiceForAttribute(addr);
                return true;
            }
            else if (addr != null && Uri.TryCreate(addr, UriKind.Absolute, out uri)) // address provided
            {
                attr = new ServiceForAttribute(addr);
                return true;
            }
            else // address provided, but invalid
            {
                throw Ex(ThrowReason.InvalidAddress);
            }
        }

        private bool TryGetRestClientAttr(TypeDefinition t, out MethodDefinition getter)
        {
            getter = null;
            PropertyDefinition p = (from prop in t.Properties
                                    let attr = prop.CustomAttributes.FirstOrDefault(x => x.AttributeType == Ref<RestClientAttribute>())
                                    where attr != null
                                    select prop).FirstOrDefault();

            if (p == null)
                return false;

            if (p.PropertyType != Ref<HttpClient>())
                throw Ex(ThrowReason.InvalidRestClientAttrValue);

            getter = p.GetMethod;
            return true;
        }

        private bool IsValidHttpMethod(MethodDefinition m, out HttpMethod httpmethod, out string path)
        {
            httpmethod = null;
            path = null;

            TypeReference @ref = Ref<HttpMethodAttribute>();
            CustomAttribute a = m.CustomAttributes
                .FirstOrDefault(x => x.AttributeType == @ref || x.AttributeType.DeclaringType == @ref);

            if (a == null)
                return false;

            if (m.RVA != 0) // not extern
                throw Ex(ThrowReason.ExpectExternMethod);

            httpmethod = a.Properties.First(x => x.Name == HTTP_METHOD).Argument.Value as HttpMethod;
            path = a.Fields.First(x => x.Name == PATH).Argument.Value as string;

            if (httpmethod == null)
                throw Ex(ThrowReason.InvalidHttpMethod);

            return true;
        }
    }
}
