namespace Annotations;

[AttributeUsage(AttributeTargets.Struct)]
public sealed class StrongIdAttribute
    (string underlying = "System.Guid") : Attribute
{
    /// <summary>
    /// Underlying CLR type. Defaults to System.Guid.
    /// Examples: "System.Guid", "int", "long", "string".
    /// </summary>
    public string Underlying { get; } = underlying;
}
