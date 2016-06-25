using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Rest.Fody
{
    public class Bridge
    {
        private static HttpResponseMessage CheckSuccess(HttpResponseMessage msg)
        {
            if (!msg.IsSuccessStatusCode)
                throw new RestException(msg);
            return msg;
        }

        public static async Task<string> CallString(Task<HttpResponseMessage> task)
        {
            return await CheckSuccess(await task).Content.ReadAsStringAsync();
        }

        public static async Task<Stream> CallStream(Task<HttpResponseMessage> task)
        {
            return await CheckSuccess(await task).Content.ReadAsStreamAsync();
        }

        public static async Task<byte[]> CallByteArray(Task<HttpResponseMessage> task)
        {
            return await CheckSuccess(await task).Content.ReadAsByteArrayAsync();
        }

        public static async Task<HttpResponseMessage> CallResponse(Task<HttpResponseMessage> task)
        {
            return await task;
        }

        public static async Task<HttpStatusCode> CallStatusCode(Task<HttpResponseMessage> task)
        {
            return (await task).StatusCode;
        }
    }
}
