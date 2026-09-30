using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Polyester.CodeAnalysis;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NeverInstantiateAnalyzer : DiagnosticAnalyzer
{
    private const string NeverInstantiateAttributeName =
        "Polyester.CompilerServices.NeverInstantiateAttribute";

    private static readonly DiagnosticDescriptor Rule = new(
        "UPS0001",
        "Type cannot be instantiated",
        "Type '{0}' cannot be instantiated",
        "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterOperationAction(AnalyzeObjectCreation, OperationKind.ObjectCreation);
    }

    private static void AnalyzeObjectCreation(OperationAnalysisContext context)
    {
        var operation = (IObjectCreationOperation)context.Operation;

        if (operation.Type is not INamedTypeSymbol type)
        {
            return;
        }

        if (
            !type.GetAttributes()
                .Any(attribute =>
                    attribute.AttributeClass?.ToDisplayString() == NeverInstantiateAttributeName
                )
        )
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(Rule, operation.Syntax.GetLocation(), type.Name)
        );
    }
}
