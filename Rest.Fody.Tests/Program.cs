using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace Rest.Fody.Tests
{
    class Program
    {
        static void Main(string[] args)
        {
            HttpClient cl = new HttpClient();
            Uri i = new Uri("http://example.com");

            GoodClient gc = new GoodClient();

            gc.SayHey().ContinueWith(task =>
            {
                Console.WriteLine(task.Result);
            });
            Console.ReadKey();
        }
    }
}
