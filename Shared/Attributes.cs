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

    /// <summary>
    /// Indicates a <see cref="HttpMethod.Get"/> request.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class GetAttribute : HttpMethodAttribute
    {
        public static new HttpMethod Method { get { return HttpMethod.Get; } }

        public GetAttribute(string path) : base(path) { }
    }

    /// <summary>
    /// Indicates a <see cref="HttpMethod.Post"/> request.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class PostAttribute : HttpMethodAttribute
    {
        public static new HttpMethod Method { get { return HttpMethod.Post; } }

        public PostAttribute(string path) : base(path) { }
    }

    /// <summary>
    /// Indicates a <see cref="HttpMethod.Put"/> request.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class PutAttribute : HttpMethodAttribute
    {
        public static new HttpMethod Method { get { return HttpMethod.Put; } }

        public PutAttribute(string path) : base(path) { }
    }

    /// <summary>
    /// Indicates a <see cref="HttpMethod.Delete"/> request.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class DeleteAttribute : HttpMethodAttribute
    {
        public static new HttpMethod Method { get { return HttpMethod.Delete; } }

        public DeleteAttribute(string path) : base(path) { }
    }

    /// <summary>
    /// Indicates a PATCH request.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class PatchAttribute : HttpMethodAttribute
    {
        public static new HttpMethod Method { get { return new HttpMethod("Patch"); } }

        public PatchAttribute(string path) : base(path) { }
    }

    /// <summary>
    /// Indicates a <see cref="HttpMethod.Head"/> request.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class HeadAttribute : HttpMethodAttribute
    {
        public static new HttpMethod Method { get { return HttpMethod.Head; } }

        public HeadAttribute(string path) : base(path) { }
    }
    #endregion

    #region Alias & Body & Query
    /// <summary>
    /// Indicates a HTTP request message body, that will be serialized and set to <see cref="HttpRequestMessage.Content"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    public class BodyAttribute : Attribute
    {
        public BodyAttribute()
        {
        }
    }

    /// <summary>
    /// Indicates a HTTP request message query, such as "?id=azerty".
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    public class QueryAttribute : Attribute
    {
        public string Name { get; protected set; }

        public QueryAttribute(string name)
        {
            this.Name = name;
        }

        public QueryAttribute()
        {
            this.Name = null;
        }
    }

    /// <summary>
    /// Indicates an alias for this parameter. Used when the provided url
    /// has missing parts, ie "/user/{username}".
    /// If a parameter is already named username, you can use the alias attribute instead.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    public class AliasAttribute : Attribute
    {
        public string Name { get; protected set; }

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
        public HeadersAttribute()
        {
        }
    }
    #endregion

    #region Misc (Service, ServiceFor, RestClient)
    /// <summary>
    /// Indicates that this class will be used for HTTP requests.
    /// Overriden by a <see cref="HttpClient"/> marked with the attribute <see cref="RestClientAttribute"/>.
    /// </summary>
    /// <remarks>
    /// If the <see cref="HttpClient"/> is generated by Rest.Fody and the class implements <see cref="IDisposable"/>,
    /// the <see cref="HttpClient"/> will be disposed after disposing this class.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class ServiceForAttribute : Attribute
    {
        public string Address { get; protected set; }
        
        /// <param name="address"><see cref="HttpClient.BaseAddress"/></param>
        public ServiceForAttribute(string address)
        {
            Address = address;
        }
    }

    /// <summary>
    /// Indicates that this class will be used for HTTP requests.
    /// Must have a <see cref="HttpClient"/> property marked with the attribute <see cref="RestClientAttribute"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class ServiceAttribute : Attribute
    {
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
    /// <summary>
    /// Indicates that this method (static or instance) will be used to serialize
    /// an object into <see cref="string"/> or <see cref="byte[]"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class RestSerializerAttribute : Attribute
    {
        public RestSerializerAttribute()
        {
        }
    }

    /// <summary>
    /// Indicates that this method (static or instance) will be used to deserialize
    /// a <see cref="string"/> or <see cref="byte[]"/> into an object.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class RestDeserializerAttribute : Attribute
    {
        public RestDeserializerAttribute()
        {
        }
    }
    #endregion
}
