using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Rest
{
    #region HTTP Methods
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public abstract class HttpMethodAttribute : Attribute
    {
        public virtual HttpMethod Method { get; }

        protected string path;
        public virtual string Path
        {
            get { return path; }
            protected set { path = value; }
        }

        public HttpMethodAttribute(string path)
        {
            Path = path;
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class GetAttribute : HttpMethodAttribute
    {
        public GetAttribute(string path) : base(path) { }

        public override HttpMethod Method
        {
            get { return HttpMethod.Get; }
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class PostAttribute : HttpMethodAttribute
    {
        public PostAttribute(string path) : base(path) { }

        public override HttpMethod Method
        {
            get { return HttpMethod.Post; }
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class PutAttribute : HttpMethodAttribute
    {
        public PutAttribute(string path) : base(path) { }

        public override HttpMethod Method
        {
            get { return HttpMethod.Put; }
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class DeleteAttribute : HttpMethodAttribute
    {
        public DeleteAttribute(string path) : base(path) { }

        public override HttpMethod Method
        {
            get { return HttpMethod.Delete; }
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class PatchAttribute : HttpMethodAttribute
    {
        public PatchAttribute(string path) : base(path) { }

        public override HttpMethod Method
        {
            get { return new HttpMethod("PATCH"); }
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class HeadAttribute : HttpMethodAttribute
    {
        public HeadAttribute(string path) : base(path) { }

        public override HttpMethod Method
        {
            get { return HttpMethod.Head; }
        }
    }
    #endregion

    #region Body & Query
    public enum BodySerializationMethod
    {
        Json, UrlEncoded
    }

    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    public class BodyAttribute : Attribute
    {
        public BodySerializationMethod SerializationMethod { get; protected set; }

        public BodyAttribute(BodySerializationMethod serializationMethod = BodySerializationMethod.Json)
        {
            SerializationMethod = serializationMethod;
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
    #endregion

    #region Header
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class HeaderAttribute : Attribute
    {
        public string Header { get; private set; }

        public HeaderAttribute(string header)
        {
            Header = header;
        }
    }
    #endregion

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class ServiceForAttribute : Attribute
    {
        public string Address { get; private set; }

        public ServiceForAttribute(string address)
        {
            Address = address;
        }

        public ServiceForAttribute()
        {
            Address = null;
        }
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class RestClientAttribute : Attribute
    {
        public RestClientAttribute() { }
    }
}
