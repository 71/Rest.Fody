using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rest.Fody
{
    /// <summary>
    /// Options parsed from the XML options.
    /// </summary>
    public sealed class WeavingOptions
    {
        /// <summary>
        /// Add headers to the http client, even if it is provided by the user.
        /// /!\ The headers are added to the client at the end of the constructor ; make sure the client is initialized by now.
        /// </summary>
        public bool AddHeadersToAlreadyExistingHttpClient = false;

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine($"AddHeadersToAlreadyExistingHttpClient: {AddHeadersToAlreadyExistingHttpClient}");

            return sb.ToString();
        }
    }
}
