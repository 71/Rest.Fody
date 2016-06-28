using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Mono.Cecil;
using Mono.Cecil.Cil;
using TinyIoC;

namespace Rest.Fody
{
    internal static class Extensions
    {
        private static TinyIoCContainer Container
        {
            get { return TinyIoCContainer.Current; }
        }

        private static Logger Logger
        {
            get { return Container.Resolve<Logger>(); }
        }

        /// <summary>
        /// Returns a new <see cref="WeavingException"/>, whose <see cref="Exception.Message"/> is
        /// "(TypeName) {msg}"
        /// </summary>
        public static WeavingException Message(this TypeDefinition self, string msg)
        {
            return new WeavingException($"({self.Name}) {msg}");
        }

        /// <summary>
        /// Returns a new <see cref="WeavingException"/>, whose <see cref="Exception.Message"/> is
        /// "(TypeName.MethodName) {msg}"
        /// </summary>
        public static WeavingException Message(this MethodDefinition self, string msg)
        {
            return new WeavingException($"({self.DeclaringType.Name}.{self.Name}) {msg}");
        }

        #region Misc
        public static void Emit(this Mono.Cecil.Cil.MethodBody body, Action<ILProcessor> il)
        {
            il(body.GetILProcessor());
        }

        public static void EmitToBeginning(this Mono.Cecil.Cil.MethodBody body, params Instruction[] il)
        {
            if (il.Length == 0) return;
            var proc = body.GetILProcessor();

            if (body.Instructions.Count == 0)
            {
                foreach (var i in il)
                    proc.Append(i);
            }
            else
            {
                proc.InsertBefore(body.Instructions[0], il[0]);

                if (il.Length == 1) return;
                for (int i = 1; i < il.Length; i++)
                {
                    proc.InsertAfter(body.Instructions[i - 1], il[i]);
                }
            }
        }

        public static void EmitMany(this Mono.Cecil.Cil.MethodBody body, IEnumerable<Instruction> instructions)
        {
            body.Emit(il =>
            {
                foreach (Instruction i in instructions)
                    il.Append(i);
            });
        }

        public static void EmitManyBefore(this Mono.Cecil.Cil.MethodBody body, int index, params Instruction[] instructions)
        {
            body.Emit(il =>
            {
                var before = body.Instructions[index];
                foreach (Instruction i in instructions)
                    il.InsertBefore(before, i);
            });
        }


        public static bool Is<T>(this TypeReference typeRef, bool acceptDerivedTypes = false)
        {
            return Is(typeRef, typeof(T), acceptDerivedTypes);
        }

        public static bool Is(this TypeReference typeRef, Type t, bool acceptDerivedTypes = false)
        {
            TypeDefinition def;
            return acceptDerivedTypes
                ? (def = typeRef.Resolve()).FullName == t.FullName || (def.BaseType != null && def.BaseType.Is(t))
                : typeRef.FullName == t.FullName;
        }

        //public static Type AsType(this TypeReference type)
        //{
        //    string fn = Assembly.CreateQualifiedName(type.Module.Assembly.FullName, type.GetReflectionName());
        //    return Type.GetType(fn, an => Container.Resolve<Assembly>(an.FullName), (a, s, b) => a.GetType(s, b), true);
        //}

        private static string GetReflectionName(this TypeReference type)
        {
            if (type.IsGenericInstance)
            {
                var genericInstance = (GenericInstanceType)type;
                return string.Format("{0}.{1}[{2}]", genericInstance.Namespace, type.Name, String.Join(",", genericInstance.GenericArguments.Select(p => p.GetReflectionName()).ToArray()));
            }
            return type.FullName;
        }

        public static Type AsType(this TypeReference typeRef)
        {
            Type t = Container.Resolve<Type[]>().First(x => x.Name == typeRef.Name && x.Namespace == typeRef.Namespace);
            if (typeRef is GenericInstanceType)
                t = t.MakeGenericType(((GenericInstanceType)typeRef).GenericArguments.Select(x => x.AsType()).ToArray());
            return t;
        }
        #endregion

        #region Generic
        public static GenericInstanceMethod MakeGenericMethod(this MethodReference method, params TypeReference[] genericArguments)
        {
            var result = new GenericInstanceMethod(method);
            foreach (var argument in genericArguments)
                result.GenericArguments.Add(argument);
            return result;
        }

        public static GenericInstanceType MakeGenericType(this TypeReference type, params TypeReference[] genericArguments)
        {
            var result = new GenericInstanceType(type);
            foreach (var argument in genericArguments)
                result.GenericArguments.Add(argument);
            return result;
        }
        #endregion

        #region Import
        public static TypeReference ImportType<T>(this ModuleDefinition module)
        {
            return module.Import(typeof(T));
        }

        public static MethodReference ImportMethod<T>(this ModuleDefinition module, string name, params Type[] paramTypes)
        {
            return module.Import(typeof(T).GetMethod(name, paramTypes));
        }

        public static MethodReference ImportContinueWith(this ModuleDefinition module, Type task, Type target)
        {
            return module.Import((from m in task.GetMethods()
                                  where m.Name == "ContinueWith"
                                  let p = m.GetParameters()
                                  where p.Length == 1 && p[0].ParameterType.Name == "Func`2"
                                  select m.MakeGenericMethod(target)).First());
        }

        public static MethodReference ImportToObservable(this ModuleDefinition module, TypeDefinition toe, TypeReference target)
        {
            return module.Import(toe.Methods.First(x => x.Name == "ToObservable" && x.ContainsGenericParameter)).MakeGenericMethod(target);
        }

        public static FieldReference ImportField<T, TField>(this ModuleDefinition module, Expression<Func<T, TField>> ex)
        {
            MemberExpression dp = ex.Body as MemberExpression;
            return module.Import(typeof(T).GetField(dp.Member.Name));
        }

        public static FieldReference ImportField<T>(this ModuleDefinition module, string name)
        {
            return module.Import(typeof(T).GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static));
        }

        public static MethodReference ImportGetter<T, TProp>(this ModuleDefinition module, Expression<Func<T, TProp>> ex)
        {
            MemberExpression dp = ex.Body as MemberExpression;
            return module.Import(typeof(T).GetProperty(dp.Member.Name).GetMethod);
        }

        public static MethodReference ImportSetter<T, TProp>(this ModuleDefinition module, Expression<Func<T, TProp>> ex)
        {
            MemberExpression dp = ex.Body as MemberExpression;
            return module.Import(typeof(T).GetProperty(dp.Member.Name).SetMethod);
        }

        public static MethodReference ImportCtor<T>(this ModuleDefinition module, params Type[] paramTypes)
        {
            return module.Import(typeof(T).GetConstructor(paramTypes));
        }

        public static TypeReference GetReference(this ModuleDefinition module, string fullName)
        {
            TypeReference @ref;
            return module.TryGetTypeReference(fullName, out @ref) ? @ref : module.GetType(fullName);
        }
        #endregion


        #region Custom Attributes
        public static CustomAttribute GetAttr<TAttr>(this MethodDefinition def) where TAttr : Attribute
        {
            return def.CustomAttributes.FirstOrDefault(x => x.AttributeType.Is<TAttr>(true));
        }

        public static CustomAttribute GetAttr<TAttr>(this TypeDefinition def) where TAttr : Attribute
        {
            return def.CustomAttributes.FirstOrDefault(x => x.AttributeType.Is<TAttr>(true));
        }

        public static CustomAttribute GetAttr<TAttr>(this FieldDefinition def) where TAttr : Attribute
        {
            return def.CustomAttributes.FirstOrDefault(x => x.AttributeType.Is<TAttr>(true));
        }

        public static CustomAttribute GetAttr<TAttr>(this PropertyDefinition def) where TAttr : Attribute
        {
            return def.CustomAttributes.FirstOrDefault(x => x.AttributeType.Is<TAttr>(true));
        }

        public static CustomAttribute GetAttr<TAttr>(this ParameterDefinition def) where TAttr : Attribute
        {
            return def.CustomAttributes.FirstOrDefault(x => x.AttributeType.Is<TAttr>(true));
        }

        public static IEnumerable<CustomAttribute> GetAttrs<TAttr>(this MethodDefinition def) where TAttr : Attribute
        {
            return def.CustomAttributes.Where(x => x.AttributeType.Is<TAttr>(true));
        }

        public static IEnumerable<CustomAttribute> GetAttrs<TAttr>(this TypeDefinition def) where TAttr : Attribute
        {
            return def.CustomAttributes.Where(x => x.AttributeType.Is<TAttr>(true));
        }

        public static IEnumerable<CustomAttribute> GetAttrs<TAttr>(this FieldDefinition def) where TAttr : Attribute
        {
            return def.CustomAttributes.Where(x => x.AttributeType.Is<TAttr>(true));
        }

        public static IEnumerable<CustomAttribute> GetAttrs<TAttr>(this PropertyDefinition def) where TAttr : Attribute
        {
            return def.CustomAttributes.Where(x => x.AttributeType.Is<TAttr>(true));
        }

        public static IEnumerable<CustomAttribute> GetAttrs<TAttr>(this ParameterDefinition def) where TAttr : Attribute
        {
            return def.CustomAttributes.Where(x => x.AttributeType.Is<TAttr>(true));
        }
        #endregion
    }
}
