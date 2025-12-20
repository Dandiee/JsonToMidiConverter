using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Api.Models.Generator;

internal static class SerializationAnalyzer
{
    internal static ClassModel GetClassModel(GeneratorSyntaxContext context, SerializationMode mode)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;

        // 1. Determine which attribute we are looking for based on the mode
        string targetAttribute = mode == SerializationMode.Index
            ? "AutoSerializeIndexAttribute"
            : "AutoSerializeAttribute";

        var hasAttribute = classDeclaration.AttributeLists
            .SelectMany(a => a.Attributes)
            .Any(a => context.SemanticModel.GetSymbolInfo(a).Symbol?.ContainingType.ToDisplayString().EndsWith(targetAttribute) == true);

        if (!hasAttribute) return null;

        var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;
        if (classSymbol == null) return null;

        var model = new ClassModel
        {
            Name = classSymbol.Name,
            Namespace = classSymbol.ContainingNamespace.ToDisplayString(),
            Properties = new List<PropModel>()
        };

        var properties = classSymbol.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p => !p.IsStatic && p.DeclaredAccessibility == Accessibility.Public && !p.IsReadOnly && p.SetMethod != null);

        foreach (var prop in properties)
        {
            var pModel = GeneratorShared.AnalyzeProperty(prop);
            var type = prop.Type;

            // --- Shared Logic (Bools and Enums are always packed) ---
            if (pModel.IsBool)
            {
                pModel.Bits = 1;
            }
            else if (pModel.IsEnum && type is INamedTypeSymbol enumSym)
            {
                pModel.Bits = GeneratorShared.CalculateEnumBits(enumSym);
            }
            else
            {
                // --- Divergent Logic ---
                if (mode == SerializationMode.Index)
                {
                    ApplyIndexStrategy(pModel, prop);
                }
                else
                {
                    ApplyStandardStrategy(pModel, prop);
                }
            }

            model.Properties.Add(pModel);
        }

        return model;
    }

    private static void ApplyIndexStrategy(PropModel pModel, IPropertySymbol prop)
    {
        var type = prop.Type;
        bool isNullableClass = type.IsReferenceType && type.SpecialType != SpecialType.System_String && prop.NullableAnnotation == NullableAnnotation.Annotated;
        bool isString = type.SpecialType == SpecialType.System_String;

        // In Index mode, Lists, Strings, and Nullable Classes are "References" (Payload)
        // They stay at Bits = 0.
        if (pModel.IsList || isNullableClass || isString)
        {
            pModel.Bits = 0;
        }
        else
        {
            // Primitives are packed into the UInt128 key
            if (pModel.TypeName == "MusicalFraction")
            {
                pModel.Bits = 16;
            }
            else if (pModel.TypeName == "sbyte")
            {
                pModel.Bits = 8;
                pModel.CastForBitPacking = "(byte)";
            }
            else if (pModel.TypeName == "short")
            {
                pModel.Bits = 16;
                pModel.CastForBitPacking = "(ushort)";
            }
            else if (pModel.TypeName == "int")
            {
                pModel.Bits = 32;
                pModel.CastForBitPacking = "(uint)";
            }
            else if (pModel.TypeName == "byte") pModel.Bits = 8;
            else if (pModel.TypeName == "ushort") pModel.Bits = 16;
            else if (pModel.TypeName == "uint") pModel.Bits = 32;
            else
            {
                pModel.Bits = 8; // Fallback
            }
        }
    }

    private static void ApplyStandardStrategy(PropModel pModel, IPropertySymbol prop)
    {
        var type = prop.Type;

        // In Standard mode, only specific Nullable References are packed (bit flag)
        if (type.IsReferenceType &&
            type.SpecialType != SpecialType.System_String &&
            pModel.IsList == false &&
            prop.NullableAnnotation == NullableAnnotation.Annotated)
        {
            pModel.Bits = 1;
            pModel.IsPackedNull = true;
        }

        // Note: Primitives (int, short, etc.) remain Bits = 0 here, 
        // because Standard mode writes them as full payload values, not bit-packed.
    }
}