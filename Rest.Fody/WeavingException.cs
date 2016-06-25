using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rest.Fody
{
    public sealed class WeavingException : Exception
    {
        public WeavingException(string msg) : base(msg)
        {
        }

        /// <summary>
        /// "Attribute cannot have null parameters."
        /// </summary>
        public static WeavingException AttrValuesCannotBeNull =>
            new WeavingException("Attribute cannot have null parameters.");

        /// <summary>
        /// "This attribute must have {expected} values when on a {what}."
        /// </summary>
        public static WeavingException AttrValuesOutOfRange(int expected, string what) =>
            new WeavingException($"This attribute must have {expected} values when on a {what}.");
    }
}
