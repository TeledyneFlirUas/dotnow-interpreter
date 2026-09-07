using dotnow.Runtime.CIL;
using System;
using System.Linq.Expressions;
using System.Reflection;

namespace dotnow.Interop
{
    /// <summary>
    /// Creates real .NET delegates that target interpreted (or interop) methods.
    /// Used by 'newobj' on a delegate type when the function pointer argument was produced by 'ldftn'/'ldvirtftn'.
    /// </summary>
    internal static class __delegate
    {
        /// <summary>
        /// Receives the native call from a delegate and forwards it into the interpreter via reflection.
        /// </summary>
        internal sealed class InterpretedThunk
        {
            // Internal
            internal static readonly MethodInfo invokeMethod = typeof(InterpretedThunk).GetMethod(nameof(Invoke), BindingFlags.Instance | BindingFlags.Public);

            // Private
            private readonly MethodBase method;
            private readonly object target;

            // Constructor
            public InterpretedThunk(MethodBase method, object target)
            {
                this.method = method;
                this.target = target;
            }

            // Methods
            public object Invoke(object[] args)
            {
                // CLRMethodInfo.Invoke pushes a reflection frame on the interpreter thread, so this is safe to call
                // both from native code (Unity events) and re-entrantly from interpreted code invoking the delegate.
                return method.Invoke(target, args);
            }
        }

        // Methods
        public static bool IsDelegateType(Type type)
        {
            return type != null && type.IsCLRType() == false && typeof(Delegate).IsAssignableFrom(type) && type != typeof(Delegate) && type != typeof(MulticastDelegate);
        }

        /// <summary>
        /// Create a delegate of <paramref name="delegateType"/> bound to <paramref name="method"/> on <paramref name="target"/> (null for static).
        /// </summary>
        public static Delegate CreateDelegate(Type delegateType, object target, CILMethodInfo method)
        {
            // Check for null
            if (delegateType == null)
                throw new ArgumentNullException(nameof(delegateType));

            if (method == null)
                throw new ArgumentNullException(nameof(method));

            // Interpreted delegate types cannot be instantiated as real delegates - only BCL/Unity/compiled delegate types are supported
            if (delegateType.IsCLRType() == true)
                throw new NotSupportedException("Delegate types declared in interpreted code are not supported. Use Action/Func/UnityAction or a delegate type from a compiled assembly: " + delegateType);

            MethodInfo invoke = delegateType.GetMethod("Invoke", BindingFlags.Instance | BindingFlags.Public);

            if (invoke == null)
                throw new ArgumentException("Not a delegate type: " + delegateType);

            // Interop method - the runtime can bind it directly (virtual dispatch on the target is handled by CreateDelegate)
            if ((method.Flags & CILMethodFlags.Interpreted) == 0 && method.Method is MethodInfo interopMethod)
            {
                if (interopMethod.IsStatic == true)
                    return Delegate.CreateDelegate(delegateType, interopMethod);

                // An interpreted instance must be unwrapped to the native base object the method belongs to
                object interopTarget = target != null
                    ? target.UnwrapAsType(interopMethod.DeclaringType)
                    : null;

                if (interopTarget == null)
                    throw new NullReferenceException("Delegate target could not be marshalled to " + interopMethod.DeclaringType);

                return Delegate.CreateDelegate(delegateType, interopTarget, interopMethod);
            }

            // Interpreted method - build a delegate with the exact signature that forwards into the interpreter.
            // Expression trees compile to IL on JIT platforms and are interpreted by System.Linq.Expressions on IL2CPP.
            ParameterInfo[] parameters = invoke.GetParameters();
            ParameterExpression[] parameterExpressions = new ParameterExpression[parameters.Length];
            Expression[] boxedArguments = new Expression[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                Type parameterType = parameters[i].ParameterType;

                if (parameterType.IsByRef == true)
                    throw new NotSupportedException("Delegates with ref/out parameters are not supported for interpreted targets: " + delegateType);

                parameterExpressions[i] = Expression.Parameter(parameterType, parameters[i].Name ?? ("arg" + i));
                boxedArguments[i] = Expression.Convert(parameterExpressions[i], typeof(object));
            }

            InterpretedThunk thunk = new InterpretedThunk(method.Method, target);

            Expression call = Expression.Call(
                Expression.Constant(thunk),
                InterpretedThunk.invokeMethod,
                Expression.NewArrayInit(typeof(object), boxedArguments));

            Expression body = invoke.ReturnType == typeof(void)
                ? call
                : Expression.Convert(call, invoke.ReturnType);

            return Expression.Lambda(delegateType, body, parameterExpressions).Compile();
        }
    }
}
