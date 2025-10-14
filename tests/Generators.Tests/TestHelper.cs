using System.Reflection;
using Annotations;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Generators.Tests;

public static class TestHelper
{
    public static Task Verify<T>(string source) 
        where T : IIncrementalGenerator, new()
    {
        // Parse the provided string into a C# syntax tree
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source, path: "Test.cs");     
    
        // Reference necessary assemblies
        // This is needed for the compilation to understand basic types like object, string, etc.
        // Find the path to the .NET runtime assemblies
        string runtimePath = Path.GetDirectoryName(typeof(object).GetTypeInfo().Assembly.Location)!;
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).GetTypeInfo().Assembly.Location),
            MetadataReference.CreateFromFile(Path.Combine(runtimePath, "System.Runtime.dll")),
            MetadataReference.CreateFromFile(Assembly.Load("netstandard").Location),
            MetadataReference.CreateFromFile(typeof(StrongIdAttribute).Assembly.Location),
        };
        
        // Create a Roslyn compilation for the syntax tree.
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "Tests",
            syntaxTrees: [syntaxTree],
            references: references,
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable)
        );


        var diagnostics = compilation.GetDiagnostics();
        foreach (var diagnostic in diagnostics)
        {
            Console.WriteLine($"Diagnostic: {diagnostic}");
        }
        diagnostics.Should().BeEmpty();
        
        // Create an instance of our EnumGenerator incremental source generator
        var generator = new T();

        // The GeneratorDriver is used to run our generator against a compilation
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        // Run the source generator!
        driver = driver.RunGenerators(compilation);

        // Use verify to snapshot test the source generator output!
        return Verifier.Verify(driver);
    }
}