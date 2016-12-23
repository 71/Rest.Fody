using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Rest.Fody.Helpers
{
    /// <summary>
    /// Utility class that resolves HttpClient, in order to make sure
    /// required assemblies are referenced in the build.
    /// </summary>
    internal sealed class LoadHack
    {
        private HttpClient clientHack = default(HttpClient);
        private Task taskHack = default(Task);
        private CancellationToken tokenHack = default(CancellationToken);
    }
}
