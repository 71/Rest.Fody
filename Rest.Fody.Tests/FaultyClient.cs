using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Rest.Fody.Tests
{
#if TESTING_FAULTY
    [ServiceFor]
    public class FaultyClient
    {
        public HttpClient client;

        public FaultyClient()
        {

        }

        [Get("/hello/world")]
        public extern Task<bool> Fail();

        [Get("http://test.com/hello/world/{str}")]
        public extern Task<bool> Fail(string str);
    }
#endif
}
