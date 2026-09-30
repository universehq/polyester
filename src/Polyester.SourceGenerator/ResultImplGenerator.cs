using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Polyester.SourceGenerator;

[Generator(LanguageNames.CSharp)]
public sealed class ResultImplGenerator : IIncrementalGenerator
{
    private const string AttributeMetadataName = "Polyester.CompilerServices.ResultImplAttribute";
    private const string SuccessMetadataName = "Polyester.Success`1";
    private const string FailureMetadataName = "Polyester.Failure`1";

    private static readonly DiagnosticDescriptor UnionRequired = new(
        "PSG0001",
        "Result implementation requires a union",
        "Type '{0}' must be a union to use ResultImplAttribute",
        "Polyester.SourceGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    private static readonly DiagnosticDescriptor PartialRequired = new(
        "PSG0002",
        "Result union must be partial",
        "Union '{0}' must be partial to use ResultImplAttribute",
        "Polyester.SourceGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    private static readonly DiagnosticDescriptor CasesRequired = new(
        "PSG0003",
        "Result union cases are invalid",
        "Union '{0}' must declare exactly two cases in this order: Polyester.Success<TValue> and Polyester.Failure<TError>",
        "Polyester.SourceGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    private static readonly DiagnosticDescriptor ContainingTypeMustBePartial = new(
        "PSG0004",
        "Containing type must be partial",
        "Containing type '{0}' must be partial so result union '{1}' can be generated",
        "Polyester.SourceGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    private static readonly SymbolDisplayFormat FullyQualifiedNullableFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
            SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions
                | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
        );

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.ForAttributeWithMetadataName(
            AttributeMetadataName,
            static (node, _) => node is TypeDeclarationSyntax,
            static (attributeContext, cancellationToken) =>
                CreateCandidate(attributeContext, cancellationToken)
        );

        context.RegisterSourceOutput(
            candidates,
            static (productionContext, candidate) => Emit(productionContext, candidate)
        );
    }

    private static Candidate CreateCandidate(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken
    )
    {
        var symbol = (INamedTypeSymbol)context.TargetSymbol;
        var location = context.TargetNode.GetLocation();

        if (!symbol.IsUnion || context.TargetNode is not UnionDeclarationSyntax)
        {
            return Candidate.Invalid(
                Diagnostic.Create(UnionRequired, location, symbol.ToDisplayString())
            );
        }

        var declarations = symbol
            .DeclaringSyntaxReferences.Select(reference => reference.GetSyntax(cancellationToken))
            .OfType<UnionDeclarationSyntax>()
            .ToImmutableArray();

        if (
            declarations.IsDefaultOrEmpty
            || declarations.Any(declaration =>
                !declaration.Modifiers.Any(SyntaxKind.PartialKeyword)
            )
        )
        {
            return Candidate.Invalid(
                Diagnostic.Create(PartialRequired, location, symbol.ToDisplayString())
            );
        }

        for (
            var containingType = symbol.ContainingType;
            containingType is not null;
            containingType = containingType.ContainingType
        )
        {
            var nonPartialDeclaration = containingType
                .DeclaringSyntaxReferences.Select(reference =>
                    reference.GetSyntax(cancellationToken)
                )
                .OfType<TypeDeclarationSyntax>()
                .FirstOrDefault(declaration =>
                    !declaration.Modifiers.Any(SyntaxKind.PartialKeyword)
                );

            if (nonPartialDeclaration is not null)
            {
                return Candidate.Invalid(
                    Diagnostic.Create(
                        ContainingTypeMustBePartial,
                        nonPartialDeclaration.Identifier.GetLocation(),
                        containingType.ToDisplayString(),
                        symbol.ToDisplayString()
                    )
                );
            }
        }

        var caseDeclaration = declarations.FirstOrDefault(declaration =>
            declaration.ParameterList is not null
        );
        if (caseDeclaration?.ParameterList is not { Parameters.Count: 2 } parameters)
        {
            return Candidate.Invalid(
                Diagnostic.Create(CasesRequired, location, symbol.ToDisplayString())
            );
        }

        var compilation = context.SemanticModel.Compilation;
        var semanticModel = compilation.GetSemanticModel(caseDeclaration.SyntaxTree);
        var successCase =
            semanticModel.GetTypeInfo(parameters.Parameters[0].Type!, cancellationToken).Type
            as INamedTypeSymbol;
        var failureCase =
            semanticModel.GetTypeInfo(parameters.Parameters[1].Type!, cancellationToken).Type
            as INamedTypeSymbol;
        var successDefinition = compilation.GetTypeByMetadataName(SuccessMetadataName);
        var failureDefinition = compilation.GetTypeByMetadataName(FailureMetadataName);

        if (
            successCase is null
            || failureCase is null
            || successDefinition is null
            || failureDefinition is null
            || !SymbolEqualityComparer.Default.Equals(
                successCase.OriginalDefinition,
                successDefinition
            )
            || !SymbolEqualityComparer.Default.Equals(
                failureCase.OriginalDefinition,
                failureDefinition
            )
        )
        {
            return Candidate.Invalid(
                Diagnostic.Create(CasesRequired, location, symbol.ToDisplayString())
            );
        }

        return Candidate.Valid(
            symbol,
            successCase.TypeArguments[0],
            failureCase.TypeArguments[0],
            ShouldAddNotNullConstraint(symbol, failureCase.TypeArguments[0], declarations)
        );
    }

    private static bool ShouldAddNotNullConstraint(
        INamedTypeSymbol resultType,
        ITypeSymbol errorType,
        ImmutableArray<UnionDeclarationSyntax> declarations
    )
    {
        if (
            errorType is not ITypeParameterSymbol typeParameter
            || !SymbolEqualityComparer.Default.Equals(typeParameter.ContainingSymbol, resultType)
            || typeParameter.HasNotNullConstraint
            || typeParameter.HasValueTypeConstraint
            || typeParameter.HasReferenceTypeConstraint
            || typeParameter.HasUnmanagedTypeConstraint
        )
        {
            return false;
        }

        return declarations.All(declaration =>
            declaration.ConstraintClauses.All(clause =>
                clause.Name.Identifier.ValueText != typeParameter.Name
            )
        );
    }

    private static void Emit(SourceProductionContext context, Candidate candidate)
    {
        if (candidate.Diagnostic is not null)
        {
            context.ReportDiagnostic(candidate.Diagnostic);
            return;
        }

        var source = Render(
            candidate.Type!,
            candidate.ValueType!,
            candidate.ErrorType!,
            candidate.AddNotNullConstraint
        );
        context.AddSource(CreateHintName(candidate.Type!), SourceText.From(source, Encoding.UTF8));
    }

    private static string Render(
        INamedTypeSymbol type,
        ITypeSymbol valueType,
        ITypeSymbol errorType,
        bool addNotNullConstraint
    )
    {
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated/>");
        builder.AppendLine("#nullable enable");

        var indent = 0;
        if (!type.ContainingNamespace.IsGlobalNamespace)
        {
            builder
                .Append("namespace ")
                .Append(type.ContainingNamespace.ToDisplayString())
                .AppendLine();
            builder.AppendLine("{");
            indent++;
        }

        var containingTypes = GetContainingTypes(type);
        foreach (var containingType in containingTypes)
        {
            AppendIndent(builder, indent);
            builder.Append(GetAccessibility(containingType.DeclaredAccessibility));
            AppendTypeModifiers(builder, containingType);
            builder
                .Append("partial ")
                .Append(GetTypeKeyword(containingType))
                .Append(' ')
                .Append(EscapeIdentifier(containingType.Name));
            AppendTypeParameters(builder, containingType);
            builder.AppendLine();
            AppendIndent(builder, indent);
            builder.AppendLine("{");
            indent++;
        }

        var valueTypeName = valueType.ToDisplayString(FullyQualifiedNullableFormat);
        var errorTypeName = errorType.ToDisplayString(FullyQualifiedNullableFormat);
        var selfTypeName = type.ToDisplayString(FullyQualifiedNullableFormat);

        AppendIndent(builder, indent);
        builder.Append(GetAccessibility(type.DeclaredAccessibility));
        AppendTypeModifiers(builder, type);
        builder.Append("partial union ").Append(EscapeIdentifier(type.Name));
        AppendTypeParameters(builder, type);
        builder
            .Append(" : global::Polyester.IResult<")
            .Append(valueTypeName)
            .Append(", ")
            .Append(errorTypeName)
            .Append(", ")
            .Append(selfTypeName)
            .Append('>');

        if (addNotNullConstraint && errorType is ITypeParameterSymbol errorTypeParameter)
        {
            builder.AppendLine();
            AppendIndent(builder, indent + 1);
            builder
                .Append("where ")
                .Append(EscapeIdentifier(errorTypeParameter.Name))
                .Append(" : notnull");
        }

        builder.AppendLine();
        AppendIndent(builder, indent);
        builder.AppendLine("{");

        AppendIndent(builder, indent + 1);
        builder
            .Append("public bool IsSuccessful => this is global::Polyester.Success<")
            .Append(valueTypeName)
            .AppendLine(">;");
        builder.AppendLine();

        AppendIndent(builder, indent + 1);
        builder
            .Append("public static ")
            .Append(selfTypeName)
            .Append(" Success(")
            .Append(valueTypeName)
            .Append(" value) => new global::Polyester.Success<")
            .Append(valueTypeName)
            .AppendLine(">(value);");
        builder.AppendLine();

        AppendIndent(builder, indent + 1);
        builder
            .Append("public static ")
            .Append(selfTypeName)
            .Append(" Failure(")
            .Append(errorTypeName)
            .AppendLine(" error)");
        AppendIndent(builder, indent + 1);
        builder.AppendLine("{");
        AppendIndent(builder, indent + 2);
        builder.AppendLine("global::System.ArgumentNullException.ThrowIfNull(error);");
        AppendIndent(builder, indent + 2);
        builder
            .Append("return new global::Polyester.Failure<")
            .Append(errorTypeName)
            .AppendLine(">(error);");
        AppendIndent(builder, indent + 1);
        builder.AppendLine("}");

        AppendIndent(builder, indent);
        builder.AppendLine("}");

        for (var index = containingTypes.Length - 1; index >= 0; index--)
        {
            indent--;
            AppendIndent(builder, indent);
            builder.AppendLine("}");
        }

        if (!type.ContainingNamespace.IsGlobalNamespace)
        {
            builder.AppendLine("}");
        }

        return builder.ToString();
    }

    private static ImmutableArray<INamedTypeSymbol> GetContainingTypes(INamedTypeSymbol type)
    {
        var containingTypes = new Stack<INamedTypeSymbol>();
        for (
            var current = type.ContainingType;
            current is not null;
            current = current.ContainingType
        )
        {
            containingTypes.Push(current);
        }

        return containingTypes.ToImmutableArray();
    }

    private static void AppendTypeModifiers(StringBuilder builder, INamedTypeSymbol type)
    {
        if (type.IsStatic)
        {
            builder.Append("static ");
            return;
        }

        if (type.IsRefLikeType)
        {
            builder.Append("ref ");
        }

        if (type.IsReadOnly)
        {
            builder.Append("readonly ");
        }

        if (type.IsAbstract && type.TypeKind == TypeKind.Class)
        {
            builder.Append("abstract ");
        }

        if (type.IsSealed && type.TypeKind == TypeKind.Class)
        {
            builder.Append("sealed ");
        }
    }

    private static void AppendTypeParameters(StringBuilder builder, INamedTypeSymbol type)
    {
        if (type.TypeParameters.IsDefaultOrEmpty)
        {
            return;
        }

        builder.Append('<');
        for (var index = 0; index < type.TypeParameters.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(", ");
            }

            builder.Append(EscapeIdentifier(type.TypeParameters[index].Name));
        }

        builder.Append('>');
    }

    private static string GetTypeKeyword(INamedTypeSymbol type)
    {
        if (type.IsUnion)
        {
            return "union";
        }

        if (type.IsRecord)
        {
            return type.TypeKind == TypeKind.Struct ? "record struct" : "record";
        }

        return type.TypeKind switch
        {
            TypeKind.Struct => "struct",
            TypeKind.Interface => "interface",
            _ => "class",
        };
    }

    private static string GetAccessibility(Accessibility accessibility) =>
        accessibility switch
        {
            Accessibility.Public => "public ",
            Accessibility.Internal => "internal ",
            Accessibility.Private => "private ",
            Accessibility.Protected => "protected ",
            Accessibility.ProtectedAndInternal => "private protected ",
            Accessibility.ProtectedOrInternal => "protected internal ",
            _ => string.Empty,
        };

    private static string EscapeIdentifier(string identifier) =>
        SyntaxFacts.GetKeywordKind(identifier) != SyntaxKind.None
        || SyntaxFacts.GetContextualKeywordKind(identifier) != SyntaxKind.None
            ? "@" + identifier
            : identifier;

    private static string CreateHintName(INamedTypeSymbol type)
    {
        var metadataName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var builder = new StringBuilder(metadataName.Length + 20);
        foreach (var character in metadataName)
        {
            builder.Append(char.IsLetterOrDigit(character) ? character : '_');
        }

        return builder.Append(".ResultImpl.g.cs").ToString();
    }

    private static void AppendIndent(StringBuilder builder, int indent) =>
        builder.Append(' ', indent * 4);

    private sealed class Candidate
    {
        private Candidate(
            INamedTypeSymbol? type,
            ITypeSymbol? valueType,
            ITypeSymbol? errorType,
            bool addNotNullConstraint,
            Diagnostic? diagnostic
        )
        {
            Type = type;
            ValueType = valueType;
            ErrorType = errorType;
            AddNotNullConstraint = addNotNullConstraint;
            Diagnostic = diagnostic;
        }

        public INamedTypeSymbol? Type { get; }
        public ITypeSymbol? ValueType { get; }
        public ITypeSymbol? ErrorType { get; }
        public bool AddNotNullConstraint { get; }
        public Diagnostic? Diagnostic { get; }

        public static Candidate Valid(
            INamedTypeSymbol type,
            ITypeSymbol valueType,
            ITypeSymbol errorType,
            bool addNotNullConstraint
        ) => new(type, valueType, errorType, addNotNullConstraint, null);

        public static Candidate Invalid(Diagnostic diagnostic) =>
            new(null, null, null, false, diagnostic);
    }
}
