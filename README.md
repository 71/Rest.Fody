# Rest.Fody
A [Fody](https://github.com/Fody/Fody) addin, heavily inspired by [Refit](https://github.com/paulcbetts/refit) and [RestEase](https://github.com/canton7/RestEase).  
Thankfully, the source code for [ReactiveUI.Fody](https://github.com/kswoll/ReactiveUI.Fody) was easy to understand, and greatly helped me.

## Disclaimer
Right now, it **doesn't** work. It should be working (but *not* production-ready) within a few days.

## Basic syntax
````csharp
[ServiceFor("http://example.com/api/v2")]
public class API
{
    [Get("/user")]
    public extern async Task<MyUser> GetUser();
}
````

## Source structure

### Attributes.cs
#### Namespace: `Rest`
Attributes usable in the user's code.

### Constants.cs
#### Namespace: `Rest.Fody`
Constants, such as type & property names.

### ModuleWeavers.cs
#### Namespace: `Rest.Fody`
The entry point of the weaver. Defines basic methods.

### Weaving/Middleware.cs
#### Namespace: `Rest.Fody`

## API