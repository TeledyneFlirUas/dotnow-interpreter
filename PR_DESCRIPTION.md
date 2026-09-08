## Summary

Extends the interpreter so ordinary C# from the Roslyn C# 2.0 runtime-compile path executes under dotnow on IL2CPP. Previously any method containing `try/catch/finally`, `foreach` over `List<T>`, `using`, `lock`, a lambda/delegate, or a `switch` jump table either failed to load or threw `NotImplementedException`. Also fixes several metadata/reflection bugs found along the way.

Base: upstream `ddebbcbe7feb`. Verified in the Unity Editor (Mono) with a test script covering closures, method groups, delegate invocation, `try/catch/finally` and `List<T>` iteration. Not yet verified on an IL2CPP device build.

## Interpreter (`Runtime/Runtime/CIL/CILInterpreter.cs`)

- Structured exception handling: `leave`/`leave.s`, `endfinally`/`endfault`, `rethrow`, plus a dispatcher that unwinds interpreted call frames, unwraps `TargetInvocationException`, selects the innermost matching catch clause, runs intervening `finally`/`fault` blocks and enters the handler in the same frame.
- `castclass` and all `conv.ovf.*` variants.
- `ldftn`/`ldvirtftn`; `newobj` on a delegate type builds a real .NET delegate (lambdas, closures, method groups, `UnityEvent.AddListener`, `Action`/`Func` fields).
- `callvirt`: null-guard on `VTable`.

## Interop (`Runtime/Interop/__delegate.cs`, rewritten)

Delegate factory: `Delegate.CreateDelegate` for interop targets; for interpreted targets an expression-tree thunk forwarding into `CLRMethodInfo.Invoke` (interpreted by `System.Linq.Expressions` on IL2CPP, no dynamic codegen). `CLRMethodInfo.CreateDelegate` uses the same factory.

## Metadata / reflection fixes

- `CLRExceptionHandlingClause`: `TryLength` returned `TryOffset`; `CatchType` was resolved for `finally` clauses (nil handle → "Type handle is nil" on method load).
- `AssemblyLoadContext.ResolveTypeReference`: nested types in compiled assemblies (`List<T>.Enumerator`, `Camera.MonoOrStereoscopicEye`).
- `CLRFieldInfo.SetValue`: resolve the field handle before writing.
- `CLRType.GetField/GetFields`: return inherited non-private instance fields.
- `RuntimeType.IsAssignable`: walks interpreted types to their first native base; checks interfaces. Interpreted classes can derive from other interpreted classes.
- `CILTypeInfo`: vtable also for sealed interpreted types (closure classes).
- `ILAnalyzer`: `switch` jump table was not skipped, mis-parsing the rest of the method.

## Not covered / known limitations

- Exception filters (`catch … when`).
- Delegate types declared in interpreted code; `ref`/`out` delegate parameters to interpreted targets.
- Generic instantiations over interpreted types (unchanged).
- Unimplemented opcodes: `calli`, `localloc`, `sizeof`, `cpobj`, `cpblk`/`initblk`, `jmp`, `arglist`, `mkrefany`/`refanyval`/`refanytype`.
- Edge cases: `rethrow` in an outer catch after an inner catch completed rethrows the inner exception; an exception thrown inside a `finally` and caught in the same method continues on the handler frame.
