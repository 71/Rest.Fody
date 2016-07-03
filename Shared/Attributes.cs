using System;
using System.Net.Http;

namespace Rest
{
    #region HTTP Methods
    /// <summary>
    /// Indicates the <see cref="HttpMethod"/> of the request, and its relative (or absolute) url.
    /// When inherited, the static <see cref="Method"/> property must be set to something else.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public abstract class HttpMethodAttribute : Attribute
    {
        public static HttpMethod Method { get { return null; } }

        public string Path { get; protected set; }

        public HttpMethodAttribute(string path)
        {
            Path = path;
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class GetAttribute : HttpMethodAttribute
    {
        public static new HttpMethod Method { get { return HttpMethod.Get; } }

        /// <summary>
        /// Make a <see cref="HttpMethod.Get"/> request.
        /// </summary>
        public GetAttribute(string path) : base(path) { }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class PostAttribute : HttpMethodAttribute
    {
        public static new HttpMethod Method { get { return HttpMethod.Post; } }

        /// <summary>
        /// Make a <see cref="HttpMethod.Post"/> request.
        /// </summary>
        public PostAttribute(string path) : base(path) { }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class PutAttribute : HttpMethodAttribute
    {
        public static new HttpMethod Method { get { return HttpMethod.Put; } }

        /// <summary>
        /// Make a <see cref="HttpMethod.Put"/> request.
        /// </summary>
        public PutAttribute(string path) : base(path) { }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class DeleteAttribute : HttpMethodAttribute
    {
        public static new HttpMethod Method { get { return HttpMethod.Delete; } }

        /// <summary>
        /// Make a <see cref="HttpMethod.Delete"/> request.
        /// </summary>
        public DeleteAttribute(string path) : base(path) { }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class PatchAttribute : HttpMethodAttribute
    {
        public static new HttpMethod Method { get { return new HttpMethod("Patch"); } }

        /// <summary>
        /// Make a PATCH request.
        /// </summary>
        public PatchAttribute(string path) : base(path) { }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class HeadAttribute : HttpMethodAttribute
    {
        public static new HttpMethod Method { get { return HttpMethod.Head; } }

        /// <summary>
        /// Make a <see cref="HttpMethod.Head"/> request.
        /// </summary>
        public HeadAttribute(string path) : base(path) { }
    }
    #endregion

    #region Alias & Body & Query
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    public class BodyAttribute : Attribute
    {
        /// <summary>
        /// Indicates a HTTP request message body, that will be serialized and set to <see cref="HttpRequestMessage.Content"/>.
        /// </summary>
        public BodyAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    public class QueryAttribute : Attribute
    {
        public string Name { get; protected set; }

        /// <summary>
        /// Indicates a HTTP request message query, such as "?id=azerty".
        /// </summary>
        public QueryAttribute(string name)
        {
            this.Name = name;
        }

        /// <summary>
        /// Indicates a HTTP request message query, such as "?id=azerty".
        /// </summary>
        public QueryAttribute()
        {
            this.Name = null;
        }
    }

    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    public class AliasAttribute : Attribute
    {
        public string Name { get; protected set; }

        /// <summary>
        /// Indicates an alias for this parameter. Used when the provided url
        /// has missing parts, ie "/user/{username}".
        /// If a parameter is already named username, you can use the alias attribute instead.
        /// </summary>
        public AliasAttribute(string name)
        {
            this.Name = name;
        }
    }
    #endregion

    #region Header(s)
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class HeaderAttribute : Attribute
    {
        public string Name { get; private set; }
        public string Value { get; private set; }

        /// <summary>
        /// As a method attribute, indicates that the header will be removed.
        /// As a class attribute, indicates that this header will be used in every request,
        /// except if overriden by another <see cref="HeaderAttribute"/>.
        /// As a parameter attribute, throws.
        /// </summary>
        public HeaderAttribute(string name, string value)
        {
            Name = name;
            Value = value;
        }

        /// <summary>
        /// As a parameter attribute, indicates that the header will have the value
        /// specified by <see cref="Name"/>.
        /// As a method attribute, throws.
        /// As a class attribute, throws.
        /// </summary>
        public HeaderAttribute(string name)
        {
            Name = name;
        }
    }

    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    public class HeadersAttribute : Attribute
    {
        /// <summary>
        /// Indicates that the given value (must be IDictionary{string,object}) will
        /// set multiple headers.
        /// </summary>
        public HeadersAttribute()
        {
        }
    }
    #endregion

    #region Misc (Service, ServiceFor, RestClient)
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class ServiceForAttribute : Attribute
    {
        public string Address { get; protected set; }

        /// <summary>
        /// Indicates that this class will be used for HTTP requests.
        /// Overriden by a <see cref="HttpClient"/> marked with the attribute <see cref="RestClientAttribute"/>.
        /// </summary>
        /// <remarks>
        /// If the <see cref="HttpClient"/> is generated by Rest.Fody and the class implements <see cref="IDisposable"/>,
        /// the <see cref="HttpClient"/> will be disposed after disposing this class.
        /// </remarks>
        /// <param name="address"><see cref="HttpClient.BaseAddress"/></param>
        public ServiceForAttribute(string address)
        {
            Address = address;
        }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class ServiceAttribute : Attribute
    {
        /// <summary>
        /// Indicates that this class will be used for HTTP requests.
        /// Must have a <see cref="HttpClient"/> property marked with the attribute <see cref="RestClientAttribute"/>.
        /// </summary>
        public ServiceAttribute()
        {
        }
    }

    /// <summary>
    /// Indicates that this <see cref="HttpClient"/> will be used to make requests.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class RestClientAttribute : Attribute
    {
        public RestClientAttribute()
        {
        }
    }
    #endregion

    #region Serializer / Deserializer
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class RestSerializerAttribute : Attribute
    {
        /// <summary>
        /// Indicates that this method (static or instance) will be used to serialize
        /// an object into <see cref="string"/> or <see cref="byte[]"/>.
        /// </summary>
        public RestSerializerAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class RestDeserializerAttribute : Attribute
    {
        /// <summary>
        /// Indicates that this method (static or instance) will be used to deserialize
        /// a <see cref="string"/> or <see cref="byte[]"/> into an object.
        /// </summary>
        public RestDeserializerAttribute()
        {
        }
    }
    #endregion

    #region After / Before Hooks
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class BeforeSendAttribute : Attribute
    {
        /// <summary>
        /// Indicates that this method will be called before sending a request.
        /// Takes a single parameter: <see cref="HttpRequestMessage"/>.
        /// </summary>
        public BeforeSendAttribute()
        {
        }
    }
    
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class AfterSendAttribute : Attribute
    {
        /// <summary>
        /// Indicates that this method will be called after receiving a successful response.
        /// Takes a single parameter: <see cref="HttpResponseMessage"/>.
        /// </summary>
        public AfterSendAttribute()
        {
        }
    }
    #endregion
}
