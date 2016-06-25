using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Rest.Fody.Tests
{
    class Program
    {
        static void Main(string[] args)
        {
            HttpClient cl = new HttpClient();
            Uri i = new Uri("http://example.com");

            GoodClient gc = new GoodClient();

            gc.Say().ContinueWith(task =>
            {
                Console.WriteLine(task.Result);
                Console.WriteLine(task.IsFaulted);
            });

            gc.SayMore("something", DateTime.Now).ContinueWith(task =>
            {
                Console.WriteLine(task.Result);
                Console.WriteLine(task.IsFaulted);
            });
            Console.ReadKey();
        }
    }

    [ServiceFor("http://hello.com/api/v1")]
    [Header("Authorization", "Bearer Something")]
    public class GoodClient
    {
        [Get("/hello/hey")]
        public extern Task<string> SayHey();

        [Get("/hello/{something}")]
        [Header("Authorization", "Bearer Something else")]
        public extern Task<string> Say(string something = "you");

        [Post("/hello/{hey}")]
        public extern Task<string> SayMore([Alias("hey")] string something, [Body] DateTime date);

        [RestSerializer]
        public string Serialize(object o)
        {
            if (o is DateTime)
                return ((DateTime)o).ToLongDateString();
            else
                return o.ToString();
        }
    }
}
