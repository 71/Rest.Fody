using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;

namespace Rest
{
    public sealed class RestException : Exception
    {
        public HttpResponseMessage ResponseMessage { get; private set; }

        public HttpStatusCode StatusCode { get { return ResponseMessage.StatusCode; } }

        internal RestException(HttpResponseMessage msg)
        {
            ResponseMessage = msg;
        }
    }
}
