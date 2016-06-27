using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Rest.Fody.Helpers
{
    /// <summary>
    /// Helper class used by Rest.Fody to use async/await methods from IL.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class AsyncProxy
    {
        #region Status codes
        private static HttpResponseMessage CheckSuccess(HttpResponseMessage msg)
        {
            if (!msg.IsSuccessStatusCode)
                throw new RestException(msg);
            return msg;
        }

        /// <summary>
        /// Returns the result of await <see cref="HttpResponseMessage"/>.<see cref="HttpContent.ReadAsStringAsync"/>.
        /// </summary>
        /// <exception cref="RestException">The HTTP request failed (300 > <see cref="HttpStatusCode"/> > 199)</exception>
        public static async Task<string> CallString(Task<HttpResponseMessage> task)
        {
            return await CheckSuccess(await task).Content.ReadAsStringAsync();
        }

        /// <summary>
        /// Returns the result of await <see cref="HttpResponseMessage"/>.<see cref="HttpContent.ReadAsStreamAsync"/>.
        /// </summary>
        /// <exception cref="RestException">The HTTP request failed (300 > <see cref="HttpStatusCode"/> > 199)</exception>
        public static async Task<Stream> CallStream(Task<HttpResponseMessage> task)
        {
            return await CheckSuccess(await task).Content.ReadAsStreamAsync();
        }

        /// <summary>
        /// Returns the result of await <see cref="HttpResponseMessage"/>.<see cref="HttpContent.ReadAsByteArrayAsync"/>.
        /// </summary>
        /// <exception cref="RestException">The HTTP request failed (300 > <see cref="HttpStatusCode"/> > 199)</exception>
        public static async Task<byte[]> CallByteArray(Task<HttpResponseMessage> task)
        {
            return await CheckSuccess(await task).Content.ReadAsByteArrayAsync();
        }

        /// <summary>
        /// Returns <see cref="HttpResponseMessage"/>.
        /// </summary>
        public static async Task<HttpResponseMessage> CallResponse(Task<HttpResponseMessage> task)
        {
            return await task;
        }

        /// <summary>
        /// Returns the result of await <see cref="HttpResponseMessage.StatusCode"/>.
        /// </summary>
        public static async Task<HttpStatusCode> CallStatusCode(Task<HttpResponseMessage> task)
        {
            return (await task).StatusCode;
        }
        #endregion
    }
}
