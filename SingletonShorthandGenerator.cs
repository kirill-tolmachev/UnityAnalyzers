using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace UnityAnalyzers
{
    [Generator]
    public class SingletonShorthandGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Register the attribute source
            context.RegisterPostInitializationOutput(ctx => RegisterAttributes(ctx));

            // Create a provider that gets all classes with the GenerateStaticShorthandClassAttribute
            IncrementalValuesProvider<ClassDeclarationSyntax> classDeclarations = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (s, _) => IsSyntaxTargetForGeneration(s),
                    transform: static (ctx, _) => GetSemanticTargetForGeneration(ctx))
                .Where(static m => m is not null)!;

            // Combine the selected classes with the compilation
            IncrementalValueProvider<(Compilation, ImmutableArray<ClassDeclarationSyntax>)> compilationAndClasses =
                context.CompilationProvider.Combine(classDeclarations.Collect());

            // Generate the source code
            context.RegisterSourceOutput(compilationAndClasses, 
                static (spc, source) => Execute(source.Item1, source.Item2, spc));
        }

        private static bool IsSyntaxTargetForGeneration(SyntaxNode node) =>
            node is ClassDeclarationSyntax classSyntax && 
            classSyntax.AttributeLists.Count > 0;

        private static ClassDeclarationSyntax? GetSemanticTargetForGeneration(GeneratorSyntaxContext context)
        {
            // Get the class declaration node
            var classDeclarationSyntax = (ClassDeclarationSyntax)context.Node;
            
            // Get the semantic model
            var semanticModel = context.SemanticModel;
            
            // Check if the class has the GenerateStaticShorthandClassAttribute
            foreach (var attributeList in classDeclarationSyntax.AttributeLists)
            {
                foreach (var attribute in attributeList.Attributes)
                {
                    if (semanticModel.GetSymbolInfo(attribute).Symbol is not IMethodSymbol attributeSymbol)
                    {
                        continue;
                    }

                    var attributeName = attributeSymbol.ContainingType.ToDisplayString();
                    if (attributeName == "Game.Scripts.GenerateStaticShorthandClassAttribute")
                    {
                        return classDeclarationSyntax;
                    }
                }
            }
            
            return null;
        }

        private static void Execute(Compilation compilation, ImmutableArray<ClassDeclarationSyntax> classes, SourceProductionContext context)
        {
            if (classes.IsDefaultOrEmpty)
            {
                return;
            }

            // Process each class with the attribute
            foreach (var classDeclaration in classes)
            {
                var semanticModel = compilation.GetSemanticModel(classDeclaration.SyntaxTree);
                var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;
                
                if (classSymbol == null)
                {
                    continue;
                }

                var generateAttribute = classSymbol.GetAttributes()
                    .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "Game.Scripts.GenerateStaticShorthandClassAttribute");
                
                if (generateAttribute == null)
                {
                    continue;
                }

                // Get the className parameter from the attribute
                var shorthandClassName = generateAttribute.ConstructorArguments[0].Value as string;
                if (string.IsNullOrEmpty(shorthandClassName))
                {
                    continue;
                }

                // Get all fields with StaticPropAttribute
                var staticProps = GetStaticProps(classSymbol);
                
                // Generate the static class
                var sourceCode = GenerateStaticClass(classSymbol, shorthandClassName, staticProps);
                
                // Add the source code
                context.AddSource($"{shorthandClassName}.g.cs", SourceText.From(sourceCode, Encoding.UTF8));
            }
        }

        private static List<(string Name, ITypeSymbol Type)> GetStaticProps(INamedTypeSymbol classSymbol)
        {
            var staticProps = new List<(string, ITypeSymbol)>();
            
            foreach (var member in classSymbol.GetMembers())
            {
                if (member is IFieldSymbol fieldSymbol && 
                    fieldSymbol.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "Game.Scripts.StaticPropAttribute"))
                {
                    staticProps.Add((fieldSymbol.Name, fieldSymbol.Type));
                }
            }
            
            return staticProps;
        }

        private static string GenerateStaticClass(
            INamedTypeSymbol originalClass, 
            string shorthandClassName, 
            List<(string Name, ITypeSymbol Type)> staticProps)
        {
            var namespaceName = originalClass.ContainingNamespace.ToDisplayString();
            var originalClassName = originalClass.Name;
            
            var sb = new StringBuilder();
            
            sb.AppendLine($"// <auto-generated />");
            sb.AppendLine($"using System;");
            sb.AppendLine();
            sb.AppendLine($"namespace {namespaceName}");
            sb.AppendLine("{");
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// Static shorthand for {originalClassName}");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    public static class {shorthandClassName}");
            sb.AppendLine("    {");
            
            foreach (var prop in staticProps)
            {
                sb.AppendLine($"        /// <summary>");
                sb.AppendLine($"        /// Gets {prop.Name} from {originalClassName}.Instance");
                sb.AppendLine($"        /// </summary>");
                sb.AppendLine($"        public static {prop.Type.ToDisplayString()} {prop.Name} => {originalClassName}.Instance.{prop.Name};");
                sb.AppendLine();
            }
            
            sb.AppendLine("    }");
            sb.AppendLine("}");
            
            return sb.ToString();
        }

        private static void RegisterAttributes(IncrementalGeneratorPostInitializationContext context)
        {
            // Add the attribute source
            string attributesSource = @"
using System;

namespace Game.Scripts
{
    [AttributeUsage(AttributeTargets.Class)]
    public class GenerateStaticShorthandClassAttribute : Attribute
    {
        public string ClassName { get; }

        public GenerateStaticShorthandClassAttribute(string className)
        {
            ClassName = className;
        }
    }
    
    [AttributeUsage(AttributeTargets.Field)]
    public class StaticPropAttribute : Attribute
    {
    }
}
";
            context.AddSource("GenerateStaticShorthandClassAttribute.g.cs", SourceText.From(attributesSource, Encoding.UTF8));
        }
    }
}