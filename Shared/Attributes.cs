using System;
using System.Net.Http;

namespace Rest
{
    #region HTTP Methods
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
        public GetAttribute(string path) : base(path) { }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class PostAttribute : HttpMethodAttribute
    {
        public static new HttpMethod Method { get { return HttpMethod.Post; } }
        public PostAttribute(string path) : base(path) { }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class PutAttribute : HttpMethodAttribute
    {
        public static new HttpMethod Method { get { return HttpMethod.Put; } }
        public PutAttribute(string path) : base(path) { }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class DeleteAttribute : HttpMethodAttribute
    {
        public static new HttpMethod Method { get { return HttpMethod.Delete; } }
        public DeleteAttribute(string path) : base(path) { }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class PatchAttribute : HttpMethodAttribute
    {
        public static new HttpMethod Method { get { return new HttpMethod("Patch"); } }
        public PatchAttribute(string path) : base(path) { }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class HeadAttribute : HttpMethodAttribute
    {
        public static new HttpMethod Method { get { return HttpMethod.Head; } }
        public HeadAttribute(string path) : base(path) { }
    }
    #endregion

    #region Alias & Body & Query
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    public class BodyAttribute : Attribute
    {
        public BodyAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    public class QueryAttribute : Attribute
    {
        public string Name { get; protected set; }
        public QueryAttribute(string name)
        {
            this.Name = name;
        }
    }

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

    #region Header
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
        /// As a method attribute, indicates that the default header (specified in class) will be removed.
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
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class ServiceForAttribute : Attribute
    {
        public string Address { get; private set; }

        public ServiceForAttribute(string address)
        {
            Address = address;
        }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class ServiceAttribute : Attribute
    {
        public ServiceAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class RestClientAttribute : Attribute
    {
        public RestClientAttribute() { }
    }
    #endregion
}
