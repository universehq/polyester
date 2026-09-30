namespace Polyester.CompilerServices;

#if NET11_0_OR_GREATER
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
public sealed class ResultImplAttribute : Attribute { }
#endif
