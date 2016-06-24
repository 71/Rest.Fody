using System.Net.Http;
using System.Threading.Tasks;

namespace Rest.Fody.Tests
{
    [ServiceFor("http://hello.com/api/v1")]
    [Header("Authorization", "Bearer Something")]
    public class GoodClient
    {
        //[RestClient]
        //private HttpClient Client { get; set; } = new HttpClient() { BaseAddress = new System.Uri("http://hello.com/api/v1") };

        [Get("/hello")]
        public extern Task<string> SayHey();
    }
}
