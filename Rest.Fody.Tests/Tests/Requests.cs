using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Shouldly;

namespace Rest.Fody.Tests
{
    public sealed class RequestsTests
    {
        public static async Task BeginTests()
        {
            Api api = Should.NotThrow(() => new Api());
            
            api.GoToIndex().Method.Method.ShouldBe("Diff");
            api.GoToIndex().Content.ShouldBeNull();
            api.GoToIndex("'Tis a body.").Content.ShouldBeAssignableTo<StringContent>();
            (await api.GoToIndex(DateTime.Today).Content.ReadAsStringAsync()).ShouldBe(DateTime.Today.ToLongTimeString());
        }


        private class DiffAttribute : HttpMethodAttribute
        {
            public static new HttpMethod Method { get { return new HttpMethod("Diff"); } }

            public DiffAttribute(string path) : base(path)
            {
            }
        }

        [ServiceFor("http://example.com")]
        private class Api
        {
            [Diff("/")]
            public extern HttpRequestMessage GoToIndex();

            [Post("/")]
            public extern HttpRequestMessage GoToIndex([Body] string str);

            [Put("/hey")]
            public extern HttpRequestMessage GoToIndex([Body] DateTime when);

            [RestSerializer]
            private string Serialize(object o)
            {
                if (o is DateTime)
                    return ((DateTime)o).ToLongTimeString();
                else
                    return o?.ToString() ?? "<null>";
            }
        }
    }
}
