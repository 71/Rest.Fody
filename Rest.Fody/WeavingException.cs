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
    }
}
