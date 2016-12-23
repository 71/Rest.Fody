using System.Text;

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

        /// <summary>
        /// Throws <see cref="RestException"/> instead of <see cref="System.Net.Http.HttpRequestException"/> on
        /// connection error. <see cref="RestException."/>
        /// </summary>
        public bool ThrowRestExceptionOnInternetError = false;

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("SETTINGS:");
            sb.AppendLine($"AddHeadersToAlreadyExistingHttpClient: {AddHeadersToAlreadyExistingHttpClient}");
            sb.AppendLine($"ThrowRestExceptionOnInternetError: {ThrowRestExceptionOnInternetError}");

            return sb.ToString();
        }
    }
}
