
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class SettingIdPartialStructSyntaxAnalyzer : DiagnosticAnalyzer
{
    // Diagnostic descriptors
    public static readonly DiagnosticDescriptor MustBeStructRule = new DiagnosticDescriptor(
        id: "SETTINGS001",
        title: "ISettingId implementor must be a struct",
        messageFormat: "Type '{0}' implements ISettingId<> but is not a struct. ISettingId<> implementors must be structs.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Types implementing ISettingId<> must be structs to ensure value semantics.");

    public static readonly DiagnosticDescriptor MustBePartialRule = new DiagnosticDescriptor(
        id: "SETTINGS002",
        title: "ISettingId implementor must be partial",
        messageFormat: "Struct '{0}' implements ISettingId<> but is not declared as partial. ISettingId<> implementors must be partial structs.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Structs implementing ISettingId<> must be partial to allow source generation.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(MustBeStructRule, MustBePartialRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeTypeDeclaration, SyntaxKind.ClassDeclaration, SyntaxKind.StructDeclaration);
    }

    private static void AnalyzeTypeDeclaration(SyntaxNodeAnalysisContext context)
    {
        var typeDeclaration = (TypeDeclarationSyntax)context.Node;
        var semanticModel = context.SemanticModel;

        // Get the symbol for this type
        var typeSymbol = semanticModel.GetDeclaredSymbol(typeDeclaration);
        if (typeSymbol == null) return;

        // Check if this type implements ISettingId<>
        if (!ImplementsISettingId(typeSymbol))
            return;

        // Rule 1: Must be a struct
        if (typeSymbol.TypeKind != TypeKind.Struct)
        {
            var diagnostic = Diagnostic.Create(
                MustBeStructRule,
                typeDeclaration.Identifier.GetLocation(),
                typeSymbol.Name);
            context.ReportDiagnostic(diagnostic);
            return; // No need to check partial if it's not even a struct
        }

        // Rule 2: Must be partial
        if (!HasPartialModifier(typeDeclaration))
        {
            var diagnostic = Diagnostic.Create(
                MustBePartialRule,
                typeDeclaration.Identifier.GetLocation(),
                typeSymbol.Name);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool ImplementsISettingId(INamedTypeSymbol typeSymbol)
    {
        const string ExpectedInterfaceName = "ISettingId";
        const string ExpectedNamespace = "Scriptable.Settings"; // Adjust as needed

        return typeSymbol.AllInterfaces.Any(i => 
            i.IsGenericType && 
            i.OriginalDefinition.Name == ExpectedInterfaceName &&
            i.OriginalDefinition.ContainingNamespace?.ToDisplayString() == ExpectedNamespace);
    }

    private static bool HasPartialModifier(TypeDeclarationSyntax typeDeclaration)
    {
        return typeDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword));
    }
}