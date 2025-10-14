using System.Runtime.CompilerServices;
using Generators;
using Generators.Tests;

namespace Fluxilum.Tests.Generators;

public class StrongIdGeneratorTests
{
    [Test]
    public async Task StrongId_GeneratesExpectedCode()
    {
        var source = """
             using Annotations;

             [StrongId]
             public readonly partial struct OrderId;
         """;

        await TestHelper.Verify<StrongIdGenerator>(source);
    }
}

public static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Init()
    {
        VerifySourceGenerators.Initialize();
    }
}