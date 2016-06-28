using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Reactive;
using System.Reactive.Linq;
using Shouldly;

namespace Rest.Fody.Tests
{
    public sealed class ReactiveTests
    {
        public static void BeginTests()
        {
            Api api = Should.NotThrow(() => new Api());
            
            api.GetFrontPage().Subscribe(n => n.ShouldBe(HttpStatusCode.OK));
            api.GetResponse(-1).Subscribe(n => n.ShouldNotBeNull(), n => { throw new Exception("Should not throw"); });
            api.GetResponseContent(-1).Subscribe(n => { throw new Exception("Should throw"); }, n => n.ShouldNotBeNull());
        }

        [ServiceFor("http://example.com")]
        private class Api
        {
            [Get("/")]
            public extern IObservable<HttpStatusCode> GetFrontPage();

            [Get("/{id}")]
            public extern IObservable<HttpResponseMessage> GetResponse(int id);

            [Get("/{id}")]
            public extern IObservable<string> GetResponseContent(int id);
        }
    }
}
