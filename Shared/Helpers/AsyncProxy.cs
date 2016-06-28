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
        public readonly static bool ThrowOnError;

        #region Status codes
        private static async Task<HttpResponseMessage> CheckSuccess(Task<HttpResponseMessage> msgTask)
        {
            if (ThrowOnError)
            {
                try
                {
                    var msg = await msgTask;
                    if (!msg.IsSuccessStatusCode)
                        throw new RestException(msg);
                    return msg;
                }
                catch (HttpRequestException e)
                {
                    throw new RestException(e.Message);
                }
            }
            else
            {
                var msg = await msgTask;
                if (!msg.IsSuccessStatusCode)
                    throw new RestException(msg);
                return msg;
            }
        }

        /// <summary>
        /// Returns the result of await <see cref="HttpResponseMessage"/>.<see cref="HttpContent.ReadAsStringAsync"/>.
        /// </summary>
        /// <exception cref="RestException">The HTTP request failed (300 > <see cref="HttpStatusCode"/> > 199)</exception>
        public static async Task<string> CallString(Task<HttpResponseMessage> task)
        {
            return await (await CheckSuccess(task)).Content.ReadAsStringAsync();
        }

        /// <summary>
        /// Returns the result of await <see cref="HttpResponseMessage"/>.<see cref="HttpContent.ReadAsStreamAsync"/>.
        /// </summary>
        /// <exception cref="RestException">The HTTP request failed (300 > <see cref="HttpStatusCode"/> > 199)</exception>
        public static async Task<Stream> CallStream(Task<HttpResponseMessage> task)
        {
            return await (await CheckSuccess(task)).Content.ReadAsStreamAsync();
        }

        /// <summary>
        /// Returns the result of await <see cref="HttpResponseMessage"/>.<see cref="HttpContent.ReadAsByteArrayAsync"/>.
        /// </summary>
        /// <exception cref="RestException">The HTTP request failed (300 > <see cref="HttpStatusCode"/> > 199)</exception>
        public static async Task<byte[]> CallByteArray(Task<HttpResponseMessage> task)
        {
            return await (await CheckSuccess(task)).Content.ReadAsByteArrayAsync();
        }

        /// <summary>
        /// Returns <see cref="HttpResponseMessage"/>.
        /// </summary>
        public static async Task<HttpResponseMessage> CallResponse(Task<HttpResponseMessage> task)
        {
            if (ThrowOnError)
            {
                try { return await task; }
                catch (HttpRequestException e) { throw new RestException(e.Message); }
            }
            else
                return await task;
        }

        /// <summary>
        /// Returns the result of await <see cref="HttpResponseMessage.StatusCode"/>.
        /// </summary>
        public static async Task<HttpStatusCode> CallStatusCode(Task<HttpResponseMessage> task)
        {
            if (ThrowOnError)
            {
                try { return (await task).StatusCode; }
                catch (HttpRequestException e) { throw new RestException(e.Message); }
            }
            else
                return (await task).StatusCode;
        }
        #endregion
    }
}
