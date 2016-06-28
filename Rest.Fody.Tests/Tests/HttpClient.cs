using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Shouldly;

namespace Rest.Fody.Tests
{
    public sealed class HttpClientTests
    {
        public static async Task BeginTests()
        {
            Api api = Should.NotThrow(() => new Api());

            await api.Get404Page().ShouldThrowAsync<RestException>();
            Should.NotThrow(api.GetIndexPage);
        }

        [Service]
        private class Api
        {
            [RestClient]
            private HttpClient client { get; set; } = new HttpClient();

            [Get("http://www.example.com")]
            public extern Task<string> GetIndexPage();

            [Get("http://www.example.com/inexistant_page")]
            public extern Task<string> Get404Page();
        }
    }
}
