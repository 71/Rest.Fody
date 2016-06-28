using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;

namespace Rest
{
    /// <summary>
    /// Exception thrown when the <see cref="ResponseMessage"/>'s <see cref="StatusCode"/> is under 200 or above 299.
    /// </summary>
    /// <remarks>
    /// The exception will not be thrown on error if the user requests a <see cref="HttpStatusCode"/> or a <see cref="HttpResponseMessage"/>.
    /// </remarks>
    public sealed class RestException : Exception
    {
        /// <summary>
        /// The <see cref="HttpResponseMessage"/> whose <see cref="HttpResponseMessage.IsSuccessStatusCode"/> property returned <c>false</c>. 
        /// </summary>
        public HttpResponseMessage ResponseMessage { get; private set; }

        /// <summary>
        /// Shorthand property that returns <see cref="ResponseMessage"/>.<see cref="StatusCode"/>.
        /// </summary>
        public HttpStatusCode StatusCode { get { return ResponseMessage.StatusCode; } }

        /// <summary>
        /// Indicates that the error was caused by the network (no internet connection), and not
        /// by the response (bad status code).
        /// </summary>
        public bool IsError { get; private set; }

        internal RestException(HttpResponseMessage msg) : base($"{(int)msg.StatusCode} - {msg.ReasonPhrase}")
        {
            ResponseMessage = msg;
            IsError = false;
        }

        internal RestException(string msg) : base(msg)
        {
            IsError = true;
        }

        public override string ToString()
        {
            return $"Rest error: {ResponseMessage.ReasonPhrase}";
        }
    }
}
