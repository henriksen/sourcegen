namespace Annotations;

public enum Lifetime { Singleton, Scoped, Transient }

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ServiceAttribute(Lifetime lifetime = Lifetime.Scoped) : Attribute
{
    // Positional: choose lifetime (default Scoped)
    public Lifetime Lifetime { get; } = lifetime;

    // Named: override the service interface to register against
    public Type? As { get; set; }

    // Named: also register the concrete type as itself (optional)
    public bool AsSelf { get; set; } = false;
}
