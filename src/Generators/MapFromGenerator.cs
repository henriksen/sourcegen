using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Generators;

[Generator]
public sealed class MapFromGenerator : IIncrementalGenerator
{
    private const string AttrFqn = "Annotations.MapFromAttribute";

    private static readonly DiagnosticDescriptor MissingPropertyDiag = new DiagnosticDescriptor(
        id: "MAP001",
        title: "Destination property has no matching source",
        messageFormat: "Property '{0}' on destination type '{1}' has no matching readable property on source type '{2}'",
        category: "Mapping",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor IncompatibleTypeDiag = new DiagnosticDescriptor(
        id: "MAP002",
        title: "Property types are incompatible",
        messageFormat: "Cannot assign source '{0}.{1}' (type '{2}') to destination '{3}.{4}' (type '{5}')",
        category: "Mapping",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext ctx)
    {
        // Find [MapFrom(typeof(...))] on classes/structs (syntax gate is cheap)
        var stream = ctx.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: AttrFqn,
            predicate: static (node, _) =>
                node is TypeDeclarationSyntax t
                && (t is ClassDeclarationSyntax || t is StructDeclarationSyntax)
                && t.AttributeLists.Count > 0,
            transform: static (attrCtx, _) =>
            {
                var dest = (INamedTypeSymbol)attrCtx.TargetSymbol;
                if (dest.DeclaredAccessibility != Accessibility.Public || dest.Arity != 0)
                    return null;

                var attr = attrCtx.Attributes[0]; // semantic match: our attribute
                if (attr.ConstructorArguments.Length != 1) return null;
                var srcType = attr.ConstructorArguments[0].Value as INamedTypeSymbol;
                if (srcType is null) return null;

                // Collect public, settable destination properties
                var destProps = dest.GetMembers().OfType<IPropertySymbol>()
                    .Where(p => p.DeclaredAccessibility == Accessibility.Public && !p.IsStatic && p.SetMethod is not null)
                    .ToArray();

                // Collect public, gettable source properties
                var srcProps = srcType.GetMembers().OfType<IPropertySymbol>()
                    .Where(p => p.DeclaredAccessibility == Accessibility.Public && !p.IsStatic && p.GetMethod is not null)
                    .ToArray();

                var ns = dest.ContainingNamespace.IsGlobalNamespace
                    ? null
                    : dest.ContainingNamespace.ToDisplayString();
                var attrSyntax = attr.ApplicationSyntaxReference?.GetSyntax();
                var attrLocation = attrSyntax?.GetLocation() ?? Location.None;

                return new MapModel(ns, dest, srcType, destProps, srcProps, attrLocation);
            })
            .Where(static m => m is not null)!;

        // Emit per destination type; also report diagnostics
        ctx.RegisterSourceOutput(stream, static (spc, model) =>
        {
            // Diagnostics: missing or incompatible properties
            var (missing, incompatible) = Analyze(model!);

            foreach (var d in missing) spc.ReportDiagnostic(d);
            foreach (var d in incompatible) spc.ReportDiagnostic(d);

            // If there are errors, skip codegen to avoid cascading failures
            if (missing.Count != 0 || incompatible.Count != 0) return;

            spc.AddSource($"{model!.Dest.Name}.Mapping.g.cs", Emit(model));
        });
    }

    private static (List<Diagnostic> missing, List<Diagnostic> incompatible) Analyze(MapModel m)
    {
        var missing = new List<Diagnostic>();
        var incompatible = new List<Diagnostic>();

        foreach (var dp in m.DestProps)
        {
            var srcProp = m.SrcProps.FirstOrDefault(p => p.Name == dp.Name);

            // Prefer the destination property's source location; fall back to the [MapFrom] attribute
            var loc = dp.Locations.FirstOrDefault(l => l.IsInSource)
                      ?? m.AttrLocation
                      ?? Location.None;

            if (srcProp is null)
            {
                missing.Add(Diagnostic.Create(
                    MissingPropertyDiag, loc,
                    dp.Name, m.Dest.Name, m.Src.Name));
                continue;
            }

            // exact type match → OK
            if (SymbolEqualityComparer.Default.Equals(dp.Type, srcProp.Type)) continue;

            // allow mapping to string via ToString()
            var destIsString = dp.Type.SpecialType == SpecialType.System_String;
            if (destIsString) continue;

            incompatible.Add(Diagnostic.Create(
                IncompatibleTypeDiag, loc,
                m.Src.Name, srcProp.Name, srcProp.Type.ToDisplayString(),
                m.Dest.Name, dp.Name, dp.Type.ToDisplayString()));
        }

        return (missing, incompatible);
    }

    private static string Emit(MapModel m)
    {
        var ns = m.Namespace is null ? null : $"namespace {m.Namespace};";
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        if (ns is not null) sb.AppendLine(ns).AppendLine();

        var destName = m.Dest.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        var srcName  = m.Src.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

        sb.AppendLine("using System;");
        sb.AppendLine();
        sb.AppendLine($"public static partial class {m.Dest.Name}Mapper");
        sb.AppendLine("{");
        sb.AppendLine($"    public static {destName} To{m.Dest.Name}(this {srcName} src)");
        sb.AppendLine("    {");
        sb.AppendLine($"        var dest = new {destName}();");

        foreach (var dp in m.DestProps)
        {
            var sp = m.SrcProps.FirstOrDefault(p => p.Name == dp.Name);
            if (sp is null) continue; // unreachable if diagnostics prevented emit

            var assignExpr =
                dp.Type.SpecialType == SpecialType.System_String &&
                dp.Type.SpecialType != sp.Type.SpecialType
                ? $"src.{sp.Name}?.ToString()"
                : $"src.{sp.Name}";

            sb.AppendLine($"        dest.{dp.Name} = {assignExpr};");
        }

        // partial hook for customization
        sb.AppendLine($"        OnAfterMap(src, ref dest);");
        sb.AppendLine($"        return dest;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine($"    static partial void OnAfterMap(in {srcName} src, ref {destName} dest);");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private sealed class MapModel(
        string? ns,
        INamedTypeSymbol dest,
        INamedTypeSymbol src,
        IPropertySymbol[] destProps,
        IPropertySymbol[] srcProps,
        Location attrLocation)
    {
        public string? Namespace { get; } = ns;
        public INamedTypeSymbol Dest { get; } = dest;
        public INamedTypeSymbol Src { get; } = src;
        public IPropertySymbol[] DestProps { get; } = destProps;
        public IPropertySymbol[] SrcProps { get; } = srcProps;
        public Location AttrLocation { get; } = attrLocation;
    }
}
