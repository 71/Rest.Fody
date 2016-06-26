using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Mono.Cecil;
using Rest.Fody.Weaving;

namespace Rest.Fody
{
    internal static class Utils
    {
        public static FileStream WaitOpenFile(string file, int timeout)
        {
            DateTime now = DateTime.Now;
            while (true)
            {
                try
                {
                    return File.OpenRead(file);
                }
                catch (Exception)
                {
                    Thread.Sleep(10);
                    if ((DateTime.Now - now).Milliseconds > timeout)
                        throw new TimeoutException($"File {file} still cannot be opened after {timeout} ms.");
                }
            }
        }

        public static void FindDeserializeMethods(IEnumerable<MethodDefinition> collection,
            ref MethodDefinition serStr, ref MethodDefinition serBuf,
            ref MethodDefinition deserStr, ref MethodDefinition deserBuf)
        {
            bool serDone = false,
                 deserDone = false;

            foreach (MethodDefinition method in collection)
            {
                if (!deserDone)
                {
                    CustomAttribute deserializeAttr = method.GetAttr<RestDeserializerAttribute>();
                    if (deserializeAttr != null)
                    {
                        if (method.GenericParameters.Count != 1 || method.GenericParameters[0].HasConstraints)
                            throw new WeavingException("A method marked [RestDeserializer] must accept a single unconstrained generic parameter.");

                        GenericParameter T = method.GenericParameters[0];
                        if (method.ReturnType != T)
                            throw new WeavingException("A method marked [RestDeserializer] must return T.");

                        if (method.Parameters[0].ParameterType.Is<string>())
                            deserStr = method;
                        else if (method.Parameters[0].ParameterType.Is<byte[]>())
                            deserBuf = method;
                        else
                            throw new WeavingException("A method marked [RestDeserializer] must accept a single parameter: either string or byte[].");

                        if (serDone) break;
                        deserDone = true;
                        continue;
                    }
                }

                if (!serDone)
                {
                    CustomAttribute serializeAttr = method.GetAttr<RestSerializerAttribute>();
                    if (serializeAttr != null)
                    {
                        if (method.Parameters.Count != 1 || method.Parameters[0].ParameterType.Name != "Object")
                            throw new WeavingException("A method marked [RestSerializer] must accept a single parameter: object.");

                        if (method.ReturnType.Is<string>())
                            serStr = method;
                        else if (method.ReturnType.Is<byte[]>())
                            serBuf = method;
                        else
                            throw new WeavingException("A method marked [RestSerializer] must either return string or byte[].");

                        if (deserDone) break;
                        serDone = true;
                        continue;
                    }
                }

            }
        }
    }
}
