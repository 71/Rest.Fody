# Rest.Fody
A [Fody](https://github.com/Fody/Fody) addin, heavily inspired by [Refit](https://github.com/paulcbetts/refit) and [RestEase](https://github.com/canton7/RestEase).  
Thankfully, the source code for [ReactiveUI.Fody](https://github.com/kswoll/ReactiveUI.Fody) was easy to understand, and greatly helped me.

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

### Rest.Fody
All the weaving code.

### Rest.Fody.Portable
All the attributes.

### Rest.Fody.Tests
Some random tests ; TODO.

### Shared
Code shared between Rest.Fody.Portable and Rest.Fody.

## API

### Basic anatomy of a class
```csharp
[ServiceFor("http://example.com/api/v1")]
public class API
{
    public API()
    {
    }
    
    [Get("/user")]
    public extern Task<User> GetUser();
}
```
A class will be recognized and modified by Rest.Fody if either:
- It has the attribute `[ServiceFor(URL)]` ;
- It has the attribute `[Service]`, and contains a non-virtual HttpClient marked with the attribute `[RestClient]`.

In the first case, a private `HttpClient` field will be created, and be used internally.  
With the `[ServiceFor(URL)]` attribute, it is also possible to specify custom headers, via the
`[Header(name, value)]` attribute.

### Making requests
```csharp
[Get("/")]
public extern Task CheckInternetConnection();
```
A request must be marked `extern`, and return either a `Task`, a `Task<T>`, or a `IObservable<T>` (in which case `System.Reactive.Linq` must be referenced).  
On failure, a request will throw a `RestException`, which contains a `HttpResponseMessage`.

### Deserialization / Serialization
To free itself of any dependency besides Fody and Mono.Cecil, Rest.Fody will not be able to deserialize or serialize types besides all numeric types, `Stream`, `string` and `byte[]`. When deserializing, `HttpResponseMessage` is also accepted.

To add your own (de)serialization, declare one of those methods with the `[RestSerialize]` or `[RestDeserialize]` attribute:
- `string Serialize(object obj)` *or* `byte[] Serialize(object obj)`
- `T Deserialize<T>(string src)` *or* `T Deserialize<T>(byte[] bytes)`

Valid implementations:
```csharp
[Service]
public class API
{
    [RestDeserializer] private T Deserialize<T>(string str) { ... }
    [RestDeserializer] private T Deserialize<T>(byte[] buf) { ... }
    [RestSerializer] private string Serialize(object o) { ... }
    [RestSerializer] private byte[] Serialize(object o) { ... }
}
```
or
```csharp
public class Utils
{
    [RestDeserializer] public static T Deserialize<T>(string str) { ... }
    [RestDeserializer] public static T Deserialize<T>(byte[] buf) { ... }
    [RestSerializer] public static string Serialize(object o) { ... }
    [RestSerializer] public static byte[] Serialize(object o) { ... }
}
```
The instance methods will be chosen in priority, but will fallback to the static methods if needed.

### Query, dynamic url
```csharp
[Get("/todo")]
public extern Task<List<Todo>> GetTodos([Query] int offset, [Query] int count);

[Post("/todo/{todoId}")]
public extern Task<Todo> SaveTodo(string todoId);

[Delete("/todo/{todoId}")]
public extern Task<Todo> DeleteTodo([Alias("todoId")] string id, [Query] string @if);
```
Four ways to specify query parameters:
- `[Query] T obj` or `[Query(name)] T obj`
- `[Query] IDictionary<string, T> query` or `[Query(name)] IDictionary<string, T> query`

If the name of the query starts with a '@', it will be removed.

Two ways to change a dynamic url:
- `string id`
- `[Alias(name)] string id`

### Body
```csharp
[Put("/todo/{todoId}")]
public extern Task<Todo> UpdateTodo(string todoId, [Body] Todo todo);
```
The body of the request must be **unique**, and marked with the `[Body]` attribute.

### Headers
```csharp
[ServiceFor("http://example.com/api/v1")]
[Header("Authorization", "Bearer xxx")]
public class API
{
    [Header("Authorization")]
    public extern Task DoSomethingWithoutAuthenticating([Header("X-Client")] string client);
}
```
Headers can be specified on both classes, methods and parameters:
- On classes, `[Header(name)]` will throw, and `[Header(name, value)]` will add a default header.
- On methods, `[Header(name)]` will remove a default header, and `[Header(name, value)]` will override or add a new header.
- On parameters, `[Header(name)]` will override or add a new header, and `[Header(name, value)]` will throw.

**Note**: Default headers specified on a class will be ignored if the class provides its own `HttpClient`.  
**Note**: A `[Headers]` attribute is valid on parameters that implements `IDictionary<string, string>`.

## Options
In `FodyWeaver.xml`, options can be passed to Rest.Fody via XML.  
- **AddHeadersToAlreadyExistingHttpClient** (Default: `False`): Add ``[Header]`` on `HttpClient`, even if it is provided by the user.

## Misc
- Yes, you can do `[Get("http://full.url/user")]`.
- You can add your own attributes if they inherit the `HttpMethodAttribute` and override its static property `Method` with the `new` keyword.
- Most checks are done on **build**, meaning most runtime exceptions will be avoided. But that does not mean that it is perfectly safe.
- There is no need to create a `Serialize(object)` method if we only manipulate byte arrays and strings. Same remark with `T Deserialize<T>()`.
- If you use `IObservable<T>`, make sure `System.Reactive.Linq` is referenced, **and** used. If you never use it in your project, it will unlikely be referenced during build.
- By default, `CancellationToken.None` will be used ; you can however pass your own `CancellationToken` to any of your requests. 
