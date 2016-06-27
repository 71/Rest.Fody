using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Reactive;

namespace Rest.Fody.Tests
{
    class Program
    {
        static void Main(string[] args)
        {
            //HttpClient cl = new HttpClient();
            //Uri i = new Uri("http://example.com");

            GoodClient gc = new GoodClient();

            //new Task<string>(() => "").ContinueWith<WTFClass<DateTime>>(task =>
            //{
            //    return null;
            //});

            gc.SayMore("heyyy", DateTime.Now).ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(task.Exception.Message);
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine(task.Result);
                }
            });

            //System.Reactive.Threading.Tasks.TaskObservableExtensions.ToObservable(new Task(() => { }));

            //gc.Say().Subscribe(n =>
            //{
            //    Console.WriteLine("HEYA");
            //}, e =>
            //{
            //    Console.WriteLine("HEYA");
            //}, () =>
            //{
            //    Console.WriteLine("HEYA");
            //});
            
            Console.ReadKey();
        }
    }

    [ServiceFor("http://hello.com")]
    [Header("Authorization", "Bearer Something")]
    public class GoodClient : IDisposable
    {
        [Get("/")]
        public extern Task<WTFClass<DateTime>> SayHey();

        //[Get("/hello/{something}")]
        //[Header("Authorization", "Bearer Something else")]
        //public extern IObservable<string> Say(string something = "you");

        [Post("/hello/{hey}")]
        public extern Task<HttpStatusCode> SayMore([Alias("hey")] string something, [Body] DateTime date);

        [RestSerializer]
        public static string Serialize(object o)
        {
            if (o is DateTime)
                return ((DateTime)o).ToLongDateString();
            else
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
