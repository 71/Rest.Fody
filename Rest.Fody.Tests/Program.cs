using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Reactive;
using System.Reactive.Threading.Tasks;

namespace Rest.Fody.Tests
{
    class Program
    {
        static void Main(string[] args)
        {
            Task.Run(async () =>
            {
                ReactiveTests.BeginTests();
                await RequestsTests.BeginTests();
                await HttpClientTests.BeginTests();
            }).ToObservable().Subscribe(u =>
            {
                Console.WriteLine("Test successful.");
            }, ex =>
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(ex.Message);
                Console.ResetColor();
            });

            Console.ReadKey();
        }
    }

    [ServiceFor("http://hello.com")]
    [Header("Authorization", "Bearer Something")]
    public class GoodClient : IDisposable
    {
        [Get("/")]
        public extern Task<WTFClass<DateTime>> SayHey();

        [Get("/hello/{something}")]
        [Header("Authorization", "Bearer Something else")]
        public extern IObservable<string> Say(string something = "you");

        [Post("/hello/{hey}")]
        public extern Task<HttpStatusCode> SayMore([Alias("hey")] string something, [Body] DateTime date);

        [RestSerializer]
        public static string Serialize(object o)
        {
            if (o is DateTime)
                return ((DateTime)o).ToLongDateString();

            return o.ToString();
        }

        [RestDeserializer]
        public static T Deserialize<T>(string s)
        {
            return default(T);
        }

        public void Dispose()
        {

        }
    }

    public class WTFClass<T>
    {

    }
}
