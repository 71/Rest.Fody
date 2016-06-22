using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Rest.Fody.Weaving
{
    public static class UtilsExtensions
    {
        public static void Emit(this MethodBody body, Action<ILProcessor> il)
        {
            il(body.GetILProcessor());
        }

        public static FieldReference BindDefinition(this FieldReference field, TypeReference genericTypeDefinition)
        {
            if (!genericTypeDefinition.HasGenericParameters)
                return field;

            var genericDeclaration = new GenericInstanceType(genericTypeDefinition);
            foreach (var parameter in genericTypeDefinition.GenericParameters)
            {
                genericDeclaration.GenericArguments.Add(parameter);
            }
            var reference = new FieldReference(field.Name, field.FieldType, genericDeclaration);
            return reference;
        }
    }
}
