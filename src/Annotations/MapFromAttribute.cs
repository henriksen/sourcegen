namespace Annotations;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class MapFromAttribute(Type source) : Attribute
{
    public Type Source { get; } = source;
}