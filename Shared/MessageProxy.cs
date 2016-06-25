using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Rest.Fody.Helpers
{
    /// <summary>
    /// Class used by Rest.Fody to make creating HttpRequestMessage's easier
    /// from IL.
    /// </summary>
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

        public MessageProxy AddQuery(string name, object value)
        {
            querymap[name] = value.ToString();
            return this;
        }

        public MessageProxy AddBody(string body)
        {
            msg.Content = new StringContent(body);
            return this;
        }

        public MessageProxy AddBody(byte[] body)
        {
            msg.Content = new ByteArrayContent(body);
            return this;
        }

        public MessageProxy AddPathArg(string name, object value)
        {
            endpath = endpath.Replace($"{{{name}}}", value.ToString());
            return this;
        }

        public MessageProxy AddHeader(string name, object value)
        {
            msg.Headers.Add(name, value.ToString());
            return this;
        }

        public MessageProxy AddHeaders(IDictionary<string, object> headers)
        {
            foreach (var header in headers)
                msg.Headers.Add(header.Key, header.Value.ToString());
            return this;
        }

        public HttpRequestMessage Compile()
        {
            StringBuilder queryString = new StringBuilder(endpath);
            int i = 0;
            foreach (var query in querymap)
                queryString.AppendFormat("{0}{1}={2}", i++ == 0 ? '?' : '&', query.Key, query.Value);

            msg.RequestUri = new Uri(queryString.ToString(), UriKind.RelativeOrAbsolute);
            return msg;
        }
    }
}
