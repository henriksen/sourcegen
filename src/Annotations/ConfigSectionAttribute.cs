namespace Annotations;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ConfigSectionAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}