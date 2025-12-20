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
    public class SerializationGenerator : IIncrementalGenerator
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
            public List<PropModel> Properties;
        }

        private static ClassModel GetClassModel(GeneratorSyntaxContext context)
        {
            var classDeclaration = (ClassDeclarationSyntax)context.Node;

            var hasAttribute = classDeclaration.AttributeLists
                .SelectMany(a => a.Attributes)
                .Any(a => context.SemanticModel.GetSymbolInfo(a).Symbol?.ContainingType.ToDisplayString().EndsWith("AutoSerializeAttribute") == true);

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
                // 1. Use Shared Analysis
                var pModel = GeneratorShared.AnalyzeProperty(prop);
                var type = prop.Type;

                // 2. Apply Standard Serialization Rules
                if (pModel.IsBool)
                {
                    pModel.Bits = 1;
                }
                else if (pModel.IsEnum && type is INamedTypeSymbol enumSym)
                {
                    pModel.Bits = GeneratorShared.CalculateEnumBits(enumSym);
                }
                // Nullable packing logic (only for nullable references that aren't strings/lists)
                else if (type.IsReferenceType &&
                         type.SpecialType != SpecialType.System_String &&
                         pModel.IsList == false &&
                         prop.NullableAnnotation == NullableAnnotation.Annotated)
                {
                    pModel.Bits = 1;
                    pModel.IsPackedNull = true;
                }

                model.Properties.Add(pModel);
            }

            return model;
        }

        private void Execute(SourceProductionContext context, ClassModel model)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
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

            var packables = model.Properties.Where(p => p.Bits > 0).OrderByDescending(p => p.Bits).ToList();
            var payloadProps = model.Properties.Where(p => p.Bits == 0 || p.IsPackedNull).ToList();

            int totalBits = packables.Sum(p => p.Bits);
            int totalBytes = (totalBits + 7) / 8;

            // ---------------- WRITER ----------------
            sb.AppendLine("        public override void Write(Span<byte> buffer, ref int cursor)");
            sb.AppendLine("        {");

            for (int i = 0; i < totalBytes; i++)
            {
                sb.AppendLine($"            byte packed{i} = 0;");
            }

            int currentGlobalBit = 0;

            foreach (var p in packables)
            {
                string valExpr;
                if (p.IsBool) valExpr = $"({p.Name} ? 1 : 0)";
                else if (p.IsPackedNull) valExpr = $"({p.Name} != null ? 1 : 0)";
                else valExpr = $"(byte){p.Name}";

                int bitsRemaining = p.Bits;
                int valOffset = 0;

                while (bitsRemaining > 0)
                {
                    int byteIndex = currentGlobalBit / 8;
                    int bitIndexInByte = currentGlobalBit % 8;
                    int spaceInByte = 8 - bitIndexInByte;
                    int bitsToWrite = Math.Min(bitsRemaining, spaceInByte);
                    int mask = (1 << bitsToWrite) - 1;

                    sb.AppendLine($"            packed{byteIndex} |= (byte)((({valExpr} >> {valOffset}) & {mask}) << {bitIndexInByte});");

                    bitsRemaining -= bitsToWrite;
                    valOffset += bitsToWrite;
                    currentGlobalBit += bitsToWrite;
                }
            }

            for (int i = 0; i < totalBytes; i++)
            {
                sb.AppendLine($"            Write(buffer, ref cursor, packed{i});");
            }

            foreach (var prop in payloadProps)
            {
                if (prop.IsPackedNull)
                {
                    sb.AppendLine($"            if ({prop.Name} != null)");
                    sb.AppendLine("            {");
                    sb.AppendLine($"                {prop.Name}.Write(buffer, ref cursor);");
                    sb.AppendLine("            }");
                }
                else
                {
                    bool isPrimitive = GeneratorShared.IsPrimitive(prop.TypeName);
                    if (isPrimitive || prop.IsList)
                    {
                        string cast = GetWriteCast(prop);
                        sb.AppendLine($"            Write(buffer, ref cursor, {cast}{prop.Name});");
                    }
                    else
                    {
                        sb.AppendLine($"            {prop.Name}.Write(buffer, ref cursor);");
                    }
                }
            }
            sb.AppendLine("        }");

            // ---------------- READER ----------------
            sb.AppendLine();
            sb.AppendLine("        public override void Read(ReadOnlySpan<byte> buffer, ref int cursor)");
            sb.AppendLine("        {");

            for (int i = 0; i < totalBytes; i++)
            {
                sb.AppendLine($"            byte packed{i} = ReadByte(buffer, ref cursor);");
            }

            currentGlobalBit = 0;

            foreach (var p in packables)
            {
                List<string> parts = new List<string>();
                int bitsRemaining = p.Bits;
                int resultOffset = 0;

                while (bitsRemaining > 0)
                {
                    int byteIndex = currentGlobalBit / 8;
                    int bitIndexInByte = currentGlobalBit % 8;
                    int spaceInByte = 8 - bitIndexInByte;
                    int bitsToRead = Math.Min(bitsRemaining, spaceInByte);
                    int mask = (1 << bitsToRead) - 1;

                    string part = $"((packed{byteIndex} >> {bitIndexInByte}) & {mask})";
                    if (resultOffset > 0) part = $"({part} << {resultOffset})";
                    parts.Add(part);

                    bitsRemaining -= bitsToRead;
                    resultOffset += bitsToRead;
                    currentGlobalBit += bitsToRead;
                }

                string combined = string.Join(" | ", parts);
                if (parts.Count > 1) combined = $"({combined})";

                if (p.IsBool) sb.AppendLine($"            {p.Name} = ({combined}) != 0;");
                else if (p.IsPackedNull) sb.AppendLine($"            bool has{p.Name} = ({combined}) != 0;");
                else
                {
                    string cast = p.IsEnum ? $"({p.TypeName})" : $"({p.TypeName})";
                    sb.AppendLine($"            {p.Name} = {cast}{combined};");
                }
            }

            foreach (var prop in payloadProps)
            {
                if (prop.IsPackedNull)
                {
                    sb.AppendLine($"            if (has{prop.Name})");
                    sb.AppendLine("            {");
                    sb.AppendLine($"                {prop.Name} = new {prop.CleanTypeName}();");
                    sb.AppendLine($"                {prop.Name}.Read(buffer, ref cursor);");
                    sb.AppendLine("            }");
                    sb.AppendLine("            else");
                    sb.AppendLine("            {");
                    sb.AppendLine($"                {prop.Name} = null;");
                    sb.AppendLine("            }");
                }
                else
                {
                    bool isPrimitive = GeneratorShared.IsPrimitive(prop.TypeName);
                    if (isPrimitive || prop.IsList)
                    {
                        string readCall = GetReadCall(prop);
                        string cast = GetReadCast(prop);
                        sb.AppendLine($"            {prop.Name} = {cast}{readCall};");
                    }
                    else
                    {
                        sb.AppendLine($"            {prop.Name} = new {prop.CleanTypeName}();");
                        sb.AppendLine($"            {prop.Name}.Read(buffer, ref cursor);");
                    }
                }
            }
            sb.AppendLine("        }");
            sb.AppendLine("    }");

            if (!string.IsNullOrEmpty(model.Namespace)) sb.AppendLine("}");

            context.AddSource($"{model.Name}.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        private static string GetWriteCast(PropModel p)
        {
            if (p.IsEnum) return "(byte)";
            if (p.TypeName == "sbyte") return "(byte)";
            if (p.TypeName == "short") return "(ushort)";
            if (p.TypeName == "int") return "(uint)";
            if (p.TypeName == "long") return "(ulong)";
            return "";
        }

        private static string GetReadCall(PropModel p)
        {
            if (p.IsEnum) return "ReadByte(buffer, ref cursor)";
            if (p.TypeName == "sbyte") return "ReadByte(buffer, ref cursor)";
            if (p.TypeName == "byte") return "ReadByte(buffer, ref cursor)";
            if (p.TypeName == "short") return "ReadUInt16(buffer, ref cursor)";
            if (p.TypeName == "ushort") return "ReadUInt16(buffer, ref cursor)";
            if (p.TypeName == "int") return "ReadUInt32(buffer, ref cursor)";
            if (p.TypeName == "uint") return "ReadUInt32(buffer, ref cursor)";
            if (p.TypeName == "long") return "ReadUInt64(buffer, ref cursor)";
            if (p.TypeName == "ulong") return "ReadUInt64(buffer, ref cursor)";
            if (p.TypeName == "float") return "ReadSingle(buffer, ref cursor)";
            if (p.TypeName == "string") return "ReadString(buffer, ref cursor)";
            if (p.IsList)
            {
                if (p.ListInnerType == "byte") return "ReadList(buffer, ref cursor)";
                if (p.ListInnerType == "sbyte") return "ReadSByteList(buffer, ref cursor)";
                return $"ReadList<{p.ListInnerType}>(buffer, ref cursor)";
            }
            return $"Read<{p.TypeName}>(buffer, ref cursor)";
        }

        private static string GetReadCast(PropModel p)
        {
            if (p.IsEnum) return $"({p.TypeName})";
            if (p.TypeName == "sbyte") return "(sbyte)";
            if (p.TypeName == "short") return "(short)";
            if (p.TypeName == "int") return "(int)";
            if (p.TypeName == "long") return "(long)";
            return "";
        }
    }
}