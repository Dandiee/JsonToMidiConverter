using Microsoft.CodeAnalysis;
using System;

namespace Api.Models.Generator
{
    internal static class GeneratorShared
    {
        public static PropModel AnalyzeProperty(IPropertySymbol prop)
        {
            var type = prop.Type;
            var model = new PropModel
            {
                Name = prop.Name,
                TypeName = type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                CleanTypeName = type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat).TrimEnd('?'),
                IsBool = type.SpecialType == SpecialType.System_Boolean,
                IsEnum = type.TypeKind == TypeKind.Enum,
                IsList = (type.Name == "List" || type.Name == "IList") && type is INamedTypeSymbol g && g.IsGenericType
            };

            if (model.IsList && type is INamedTypeSymbol listType)
            {
                model.ListInnerType = listType.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            }

            return model;
        }

        public static int CalculateEnumBits(INamedTypeSymbol enumSym)
        {
            long maxVal = 0;
            foreach (var member in enumSym.GetMembers().OfType<IFieldSymbol>())
            {
                if (member.HasConstantValue && member.ConstantValue is IConvertible c)
                {
                    long val = c.ToInt64(null);
                    if (val > maxVal) maxVal = val;
                }
            }
            return maxVal == 0 ? 1 : (int)Math.Floor(Math.Log(maxVal, 2)) + 1;
        }

        public static bool IsPrimitive(string typeName)
        {
            return typeName == "byte" || typeName == "sbyte" ||
                   typeName == "short" || typeName == "ushort" ||
                   typeName == "int" || typeName == "uint" ||
                   typeName == "long" || typeName == "ulong" ||
                   typeName == "float" || typeName == "double" ||
                   typeName == "string";
        }
    }
}