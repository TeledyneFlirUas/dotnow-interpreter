using dotnow.Runtime.CIL;
using System;
using System.Collections.Generic;

namespace dotnow.Runtime
{
    internal static class RuntimeType
    {
        // Methods
        internal static bool IsInstanceOfType(CILTypeInfo type, object obj)
        {
            // Check for null
            if (obj == null)
                return false;

            // Get the type
            Type objType = obj.GetInterpretedType();

            // Check for assignable
            return IsAssignable(objType, type.Type);
        }

        internal static bool IsAssignable(Type dst, Type src)
        {
            // Check for direct match
            if (dst == src)
                return true;

            // Can't use IsAssignable check on clr types - causes hard crash in some cases
            if (dst.IsCLRType() == true)
            {
                // Check for sub class - need further work to support interfaces??
                if (src.IsSubclassOf(dst) == true)
                    return true;
            }
            else
            {
                // Runtime IsAssignableFrom returns false for any non-runtime Type (such as CLRType), so walk an interpreted
                // source type up to its first native (interop) base class and test from there.
                // Example: CharacterAnimatorController : AnimatorController : MonoBehaviour -> test MonoBehaviour against dst.
                Type interopSrc = src;
                while (interopSrc != null && interopSrc.IsCLRType() == true)
                    interopSrc = interopSrc.BaseType;

                // Check for assignable
                if (interopSrc != null && dst.IsAssignableFrom(interopSrc) == true)
                    return true;

                // Interfaces implemented by an interpreted type (or by its interpreted bases)
                if (dst.IsInterface == true && src.IsCLRType() == true)
                {
                    for (Type clrSrc = src; clrSrc != null && clrSrc.IsCLRType() == true; clrSrc = clrSrc.BaseType)
                    {
                        Type[] interfaces = clrSrc.GetInterfaces();

                        for (int i = 0; i < interfaces.Length; i++)
                        {
                            if (interfaces[i] == dst || (interfaces[i].IsCLRType() == false && dst.IsAssignableFrom(interfaces[i]) == true))
                                return true;
                        }
                    }
                }
            }

            // Handle arrays
            if (dst.IsArray == true && src.IsArray == true &&
                dst.GetArrayRank() == src.GetArrayRank() &&
                IsReferenceAssignable(dst.GetElementType(), src.GetElementType()) == true)
            {
                return true;
            }

            // Handle generics arrays
            if (src.IsArray == true && dst.IsGenericType == true &&
                (dst.GetGenericTypeDefinition() == typeof(IEnumerable<>) ||
                dst.GetGenericTypeDefinition() == typeof(IList<>) ||
                dst.GetGenericTypeDefinition() == typeof(ICollection<>)) &&
                dst.GetGenericArguments()[0] == src.GetElementType())
            {
                return true;
            }

            // Not convertible
            return false;
        }

        private static bool IsReferenceAssignable(Type dst, Type src)
        {
            // Check for direct match
            if (dst == src)
                return true;

            // Check assignable
            if (dst.IsValueType == false && src.IsValueType == false && IsAssignable(dst, src) == true)
                return true;

            // Not convertible
            return false;
        }
    }
}
