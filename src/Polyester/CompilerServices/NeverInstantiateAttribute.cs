namespace Polyester.CompilerServices;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class NeverInstantiateAttribute : Attribute { }
