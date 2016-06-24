using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rest.Fody
{
    public enum ThrowReason
    {
        InvalidAddress,
        InvalidRestClientAttrValue,
        NoClientNorAddress,
        ExpectExternMethod,
        InvalidHttpMethod,
        Null
    }

    public sealed class WeavingException : Exception
    {
        public WeavingException(string msg) : base(msg) { }
    }

    public sealed partial class ModuleWeaver
    {
        public Exception Ex(ThrowReason r, params object[] args)
        {
            string err = new Func<string>(() =>
            {
                 switch (r)
                 {
                    case ThrowReason.InvalidAddress: return "Invalid address given to [ServiceFor] attribute: {0}.";
                    case ThrowReason.InvalidRestClientAttrValue: return "Attribute [RestClient] expects an HttpClient for value.";
                    case ThrowReason.NoClientNorAddress: return "No HttpClient was specified using [RestClient], and no address was given to [ServiceFor].";
                    case ThrowReason.ExpectExternMethod: return "Expected extern method.";
                    case ThrowReason.InvalidHttpMethod: return "Invalid HttpMethod (null).";
                    case ThrowReason.Null: return "Null value: {0}";
                    default: return "Unknown error.";
                 }
            })();

            return Ex(String.Format(err, args));
        }

        public Exception Ex(string err)
        {
            //this.LogError(err);
            return new WeavingException(err);
        }
    }
}
