using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Rest.Fody.Helpers
{
    /// <summary>
    /// Helper class used by Rest.Fody to create <see cref="HttpRequestMessage"/>s easily from IL.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class MessageProxy
    {
        private string rawpath;
        private string endpath;

        private HttpRequestMessage msg = new HttpRequestMessage();
        private IDictionary<string, object> querymap = new Dictionary<string, object>();
       
        public MessageProxy(HttpMethod method, string path)
        {
            msg.Method = method;
            rawpath = endpath = path.TrimStart('/');
        }

        /// <summary>
        /// Add a query to the url, ie: "?offset=50"
        /// </summary>
        /// <param name="value">The value of the query, on which <see cref="object.ToString"/> will be called.</param>
        public MessageProxy AddQuery(string name, object value)
        {
            if (value is IDictionary<string, object>)
            {
                foreach (var kvp in ((IDictionary<string, object>)value))
                    AddQuery(kvp.Key, kvp.Value);
            }
            else
                querymap[name] = value.ToString();
            return this;
        }

        /// <summary>
        /// Add a string body to the request. Only works for PUT and POST methods.
        /// </summary>
        public MessageProxy AddBody(string body)
        {
            msg.Content = new StringContent(body);
            return this;
        }

        /// <summary>
        /// Add a byte[] body to the request. Only works for PUT and POST methods.
        /// </summary>
        public MessageProxy AddBody(byte[] body)
        {
            msg.Content = new ByteArrayContent(body);
            return this;
        }

        /// <summary>
        /// Replace a segment of the url (ie: "{taskId}") by <paramref name="value"/>.ToString()
        /// </summary>
        /// <param name="value">The value of the query, on which <see cref="object.ToString"/> will be called.</param>
        public MessageProxy AddPathArg(string name, object value)
        {
            endpath = endpath.Replace($"{{{name}}}", value.ToString());
            return this;
        }

        /// <summary>
        /// Add or replace the header <paramref name="name"/> by <paramref name="value"/>.ToString()
        /// </summary>
        /// <param name="value">The value of the query, on which <see cref="object.ToString"/> will be called.</param>
        public MessageProxy AddHeader(string name, object value)
        {
            msg.Headers.Add(name, value.ToString());
            return this;
        }

        /// <summary>
        /// Add multiple headers to the request.
        /// </summary>
        /// <param name="headers">A map of headers</param>
        public MessageProxy AddHeaders(IDictionary<string, object> headers)
        {
            foreach (var header in headers)
                msg.Headers.Add(header.Key, header.Value.ToString());
            return this;
        }

        /// <summary>
        /// Return the created <see cref="HttpRequestMessage"/>.
        /// </summary>
        public HttpRequestMessage Compile()
        {
            Regex.Replace(endpath, @"{.+?}$", String.Empty);

            if (Regex.IsMatch(endpath, @"{.+?}"))
                throw new ArgumentOutOfRangeException("Not all URI parameters were given.");

            StringBuilder queryString = new StringBuilder(endpath);
            // check if the path given by the user doesn't already have query params
            int i = endpath.IndexOf('?') > 0 ? 1 : 0;
            foreach (var query in querymap)
                queryString.AppendFormat("{0}{1}={2}", i++ == 0 ? '?' : '&', query.Key, query.Value);

            msg.RequestUri = new Uri(queryString.ToString(), UriKind.RelativeOrAbsolute);
            return msg;
        }
    }
}
