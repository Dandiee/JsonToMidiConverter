using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Api.Models.Generator
{
    [Generator]
    public class IndexSerializationGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var provider = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: (s, _) => s is ClassDeclarationSyntax,
                    transform: (ctx, _) => GetClassModel(ctx))
                .Where(m => m != null);

            context.RegisterSourceOutput(provider, Execute);
        }

        private class ClassModel
        {
            public string Name;
            public string Namespace;
            public List<PropModel> IndexableProps;
            public List<PropModel> ReferenceProps;
        }

        private static ClassModel GetClassModel(GeneratorSyntaxContext context)
        {
            var classDeclaration = (ClassDeclarationSyntax)context.Node;

            var hasAttribute = classDeclaration.AttributeLists
                .SelectMany(a => a.Attributes)
                .Any(a => context.SemanticModel.GetSymbolInfo(a).Symbol?.ContainingType.ToDisplayString().EndsWith("AutoSerializeIndexAttribute") == true);

            if (!hasAttribute) return null;

            var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;
            if (classSymbol == null) return null;

            var model = new ClassModel
            {
                Name = classSymbol.Name,
                Namespace = classSymbol.ContainingNamespace.ToDisplayString(),
                IndexableProps = new List<PropModel>(),
                ReferenceProps = new List<PropModel>()
            };

            var properties = classSymbol.GetMembers()
                .OfType<IPropertySymbol>()
                .Where(p => !p.IsStatic && p.DeclaredAccessibility == Accessibility.Public && !p.IsReadOnly && p.SetMethod != null);

            foreach (var prop in properties)
            {
                // 1. Use Shared Analysis
                var pModel = GeneratorShared.AnalyzeProperty(prop);
                var type = prop.Type;

                // 2. Index Strategy Logic
                bool isNullableClass = type.IsReferenceType && type.SpecialType != SpecialType.System_String && prop.NullableAnnotation == NullableAnnotation.Annotated;
                bool isString = type.SpecialType == SpecialType.System_String;

                if (pModel.IsList || isNullableClass || isString)
                {
                    model.ReferenceProps.Add(pModel);
                }
                else
                {
                    // Calculate Bits for Packing into UInt128
                    if (pModel.IsBool) pModel.Bits = 1;
                    else if (pModel.IsEnum && type is INamedTypeSymbol enumSym)
                    {
                        pModel.Bits = GeneratorShared.CalculateEnumBits(enumSym);
                    }
                    else if (pModel.TypeName == "MusicalFraction")
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

                    model.IndexableProps.Add(pModel);
                }
            }

            return model;
        }

        private void Execute(SourceProductionContext context, ClassModel model)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Collections.Concurrent;");
            sb.AppendLine("using System.Buffers.Binary;");
            sb.AppendLine("using Api.Models;");
            sb.AppendLine("using Api.Models.Enums;");

            if (!string.IsNullOrEmpty(model.Namespace))
            {
                sb.AppendLine($"namespace {model.Namespace}");
                sb.AppendLine("{");
            }

            sb.AppendLine($"    public partial class {model.Name}");
            sb.AppendLine("    {");

            // Static Storage
            sb.AppendLine($"        private static readonly ConcurrentDictionary<UInt128, ushort> _indexMap = new();");
            sb.AppendLine($"        public static readonly List<UInt128> _valueList = new();");
            sb.AppendLine($"        private static readonly object _listLock = new();");

            // ---------------- WRITER ----------------
            sb.AppendLine("        public override void Write(Span<byte> buffer, ref int cursor)");
            sb.AppendLine("        {");

            sb.AppendLine("            UInt128 key = 0;");
            int currentBit = 0;

            var sortedIndexables = model.IndexableProps.OrderByDescending(p => p.Bits).ToList();

            foreach (var p in sortedIndexables)
            {
                string valExpr;
                if (p.IsBool)
                    valExpr = $"({p.Name} ? 1 : 0)";
                else if (p.TypeName == "MusicalFraction")
                    valExpr = $"(ulong)(({p.Name}.Nominator) | ({p.Name}.Denominator << 8))";
                else
                    valExpr = $"(ulong){p.CastForBitPacking}{p.Name}";

                sb.AppendLine($"            key |= ((UInt128){valExpr} << {currentBit});");
                currentBit += p.Bits;
            }

            // Double-Checked Locking (Thread Safe)
            sb.AppendLine("            if (!_indexMap.TryGetValue(key, out ushort index))");
            sb.AppendLine("            {");
            sb.AppendLine("                lock (_listLock)");
            sb.AppendLine("                {");
            sb.AppendLine("                    if (!_indexMap.TryGetValue(key, out index))");
            sb.AppendLine("                    {");
            sb.AppendLine("                        index = (ushort)_valueList.Count;");
            sb.AppendLine("                        _valueList.Add(key);");
            sb.AppendLine("                        _indexMap[key] = index;");
            sb.AppendLine("                    }");
            sb.AppendLine("                }");
            sb.AppendLine("            }");

            sb.AppendLine("            Write(buffer, ref cursor, (ushort)index);");

            // References
            foreach (var prop in model.ReferenceProps)
            {
                if (prop.TypeName.Contains("List") || prop.TypeName == "string")
                {
                    sb.AppendLine($"            Write(buffer, ref cursor, {prop.Name});");
                }
                else
                {
                    sb.AppendLine($"            if ({prop.Name} != null)");
                    sb.AppendLine("            {");
                    sb.AppendLine($"                Write(buffer, ref cursor, (byte)1);");
                    sb.AppendLine($"                {prop.Name}.Write(buffer, ref cursor);");
                    sb.AppendLine("            }");
                    sb.AppendLine("            else");
                    sb.AppendLine("            {");
                    sb.AppendLine($"                Write(buffer, ref cursor, (byte)0);");
                    sb.AppendLine("            }");
                }
            }
            sb.AppendLine("        }");

            // ---------------- READER ----------------
            sb.AppendLine();
            sb.AppendLine("        public override void Read(ReadOnlySpan<byte> buffer, ref int cursor)");
            sb.AppendLine("        {");

            sb.AppendLine("            ushort index = (ushort)ReadUInt16(buffer, ref cursor);");
            sb.AppendLine("            UInt128 key = _valueList[index];");

            currentBit = 0;
            foreach (var p in sortedIndexables)
            {
                // --- FIX APPLIED HERE ---
                // Added parentheses around the shift operation
                string mask = p.Bits == 64
                    ? "ulong.MaxValue"
                    : $"(((ulong)1 << {p.Bits}) - 1)"; // <--- FIXED: (((1 << 8) - 1))

                string extraction = $"(ulong)((key >> {currentBit}) & {mask})";

                if (p.IsBool)
                    sb.AppendLine($"            {p.Name} = ({extraction}) != 0;");
                else if (p.TypeName == "MusicalFraction")
                {
                    sb.AppendLine($"            ulong fracVal{p.Name} = {extraction};");
                    sb.AppendLine($"            {p.Name} = new MusicalFraction((byte)(fracVal{p.Name} & 0xFF), (byte)((fracVal{p.Name} >> 8) & 0xFF));");
                }
                else
                {
                    string cast;
                    if (p.TypeName == "sbyte") cast = "(sbyte)(byte)";
                    else if (p.TypeName == "short") cast = "(short)(ushort)";
                    else if (p.IsEnum) cast = $"({p.TypeName})";
                    else cast = $"({p.TypeName})";

                    sb.AppendLine($"            {p.Name} = {cast}({extraction});");
                }
                currentBit += p.Bits;
            }

            foreach (var prop in model.ReferenceProps)
            {
                if (prop.TypeName.Contains("List"))
                {
                    if (prop.TypeName.Contains("Note"))
                        sb.AppendLine($"            {prop.Name} = ReadList<Note>(buffer, ref cursor);");
                    else if (prop.TypeName.Contains("Beat"))
                        sb.AppendLine($"            {prop.Name} = ReadList<Beat>(buffer, ref cursor);");
                    else
                        sb.AppendLine($"            // TODO: Add generic list reader for {prop.TypeName}");
                }
                else if (prop.TypeName == "string")
                {
                    sb.AppendLine($"            {prop.Name} = ReadString(buffer, ref cursor);");
                }
                else
                {
                    sb.AppendLine($"            if (ReadByte(buffer, ref cursor) == 1)");
                    sb.AppendLine("            {");
                    sb.AppendLine($"                {prop.Name} = new {prop.CleanTypeName}();");
                    sb.AppendLine($"                {prop.Name}.Read(buffer, ref cursor);");
                    sb.AppendLine("            }");
                    sb.AppendLine("            else");
                    sb.AppendLine("            {");
                    sb.AppendLine($"                {prop.Name} = null;");
                    sb.AppendLine("            }");
                }
            }
            sb.AppendLine("        }");

            sb.AppendLine("        public static void ExportDictionary(Span<byte> buffer, ref int cursor)");
            sb.AppendLine("        {");
            sb.AppendLine("            // TODO: Serialize _valueList to the start of the file");
            sb.AppendLine("        }");

            sb.AppendLine("    }");
            if (!string.IsNullOrEmpty(model.Namespace)) sb.AppendLine("}");

            context.AddSource($"{model.Name}_Index.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        }
    }
}