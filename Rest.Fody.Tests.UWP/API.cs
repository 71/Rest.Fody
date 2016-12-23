using System;
using System.Collections.Generic;
using System.Reactive;
using System.Threading.Tasks;

namespace Rest.Fody.Tests.UWP
{
    [ServiceFor("http://example.com")]
    public sealed class API
    {
        [Get("/")]
        public extern IObservable<IEnumerable<Unit>> Get();

        [Get("/")]
        public extern Task<Unit> GetTask();

        [RestSerializer]
        private string Serialize(object o)
        {
            return "";
        }

        [RestDeserializer]
        private T Deserialize<T>(string s)
        {
            return default(T);
        }
    }
}
