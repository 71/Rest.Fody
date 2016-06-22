using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Rest.Fody.Weaving;

namespace Rest.Fody
{
    public sealed partial class ModuleWeaver
    {
        private MethodDefinition GetSendAsyncMethod()
        {
            return ModuleDefinition
                .Types
                .First(x => x == Ref<HttpClient>())
                .Methods
                .First(x => x.Name == "SendAsync" && x.Parameters.Count == 3);
        }

        private MethodDefinition CreateHttpRequestMessage()
        {
            return ModuleDefinition
                .Types
                .First(x => x == Ref<HttpRequestMessage>())
                .Methods
                .First(x => x.IsConstructor && x.Parameters.Count == 0);
        }

        private MethodDefinition MakeHttpClientGetter(TypeDefinition def, Uri baseAddr, IEnumerable<string> headers)
        {
            FieldDefinition field = new FieldDefinition($"${GENERATED_CLIENT_PATH}", FieldAttributes.Private, Ref<HttpClient>());
            HttpClient cl = new HttpClient { BaseAddress = baseAddr };

            foreach (string header in headers)
                cl.DefaultRequestHeaders.Add(header, header);
            field.Constant = cl;
            
            var getter = new MethodDefinition($"$get_{GENERATED_CLIENT_PATH}", MethodAttributes.Private, Ref<HttpClient>());
            getter.Body.Emit(il =>
            {
                il.Emit(OpCodes.Ldarg_0);       // this
                il.Emit(OpCodes.Ldfld, field);  // ----.[field] -> stack
                il.Emit(OpCodes.Ret);           // return stack
            });

            return getter;
        }

        private void AddRestClientMethod(TypeDefinition def, MethodDefinition clientGetter, MethodDefinition method, string path, HttpMethod httpMethod, IEnumerable<string> headers)
        {
            method.Body.Emit(il =>
            {
                il.Emit(OpCodes.Ldarg_0, clientGetter); // this.client -> stack
            });
        }
    }
}
