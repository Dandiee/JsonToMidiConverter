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

        private class PropModel
        {
            public string Name;
            public string TypeName;
            public bool IsBool;
            public bool IsEnum;
            public bool IsList;
            public string ListInnerType;
            public int Bits; // 0 = Not packed, 1 = Bool, >1 = Small Enum
        }

        // --- 1. ANALYSIS PHASE ---
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
                var type = prop.Type;
                var pModel = new PropModel
                {
                    Name = prop.Name,
                    TypeName = type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                    IsBool = type.SpecialType == SpecialType.System_Boolean,
                    IsEnum = type.TypeKind == TypeKind.Enum,
                    IsList = (type.Name == "List" || type.Name == "IList") && type is INamedTypeSymbol g && g.IsGenericType
                };

                if (pModel.IsList && type is INamedTypeSymbol listType)
                {
                    pModel.ListInnerType = listType.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                }

                // --- BIT CALCULATION LOGIC ---
                if (pModel.IsBool)
                {
                    pModel.Bits = 1;
                }
                else if (pModel.IsEnum && type is INamedTypeSymbol enumSym)
                {
                    // Calculate bits based on Max Enum Value
                    long maxVal = 0;
                    bool complexEnum = false;

                    foreach (var member in enumSym.GetMembers().OfType<IFieldSymbol>())
                    {
                        if (member.HasConstantValue && member.ConstantValue is IConvertible c)
                        {
                            long val = c.ToInt64(null);
                            if (val < 0) { complexEnum = true; break; } // Don't pack negative enums
                            if (val > maxVal) maxVal = val;
                        }
                    }

                    if (!complexEnum)
                    {
                        // Log2(0) is undef, so maxVal 0 needs 1 bit
                        int bitsNeeded = maxVal == 0 ? 1 : (int)Math.Floor(Math.Log(maxVal, 2)) + 1;

                        // Only pack if it fits comfortably in a byte (< 8 bits)
                        if (bitsNeeded <= 7)
                        {
                            pModel.Bits = bitsNeeded;
                        }
                    }
                }

                model.Properties.Add(pModel);
            }

            return model;
        }

        // --- 2. GENERATION PHASE ---
        private void Execute(SourceProductionContext context, ClassModel model)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Buffers.Binary;");
            sb.AppendLine("using Api.Models;"); // Adjust if your base class is elsewhere
            sb.AppendLine("using Api.Models.Enums;"); // Adjust if your base class is elsewhere

            if (!string.IsNullOrEmpty(model.Namespace))
            {
                sb.AppendLine($"namespace {model.Namespace}");
                sb.AppendLine("{");
            }

            sb.AppendLine($"    public partial class {model.Name}");
            sb.AppendLine("    {");

            // Split into "Packables" (Bools & Small Enums) vs "Others"
            var packables = model.Properties.Where(p => p.Bits > 0).ToList();
            var others = model.Properties.Where(p => p.Bits == 0).ToList();

            // ---------------------------------------------------------
            // WRITE METHOD
            // ---------------------------------------------------------
            sb.AppendLine("        public override void Write(Span<byte> buffer, ref int cursor)");
            sb.AppendLine("        {");

            // --- PACKING LOOP ---
            int currentByteIndex = 0;
            int currentBitPos = 0;
            bool byteStarted = false;

            for (int i = 0; i < packables.Count; i++)
            {
                var p = packables[i];

                // If this prop doesn't fit in the remaining bits, flush and start new byte
                if (currentBitPos + p.Bits > 8)
                {
                    sb.AppendLine($"            Write(buffer, ref cursor, packed{currentByteIndex});");
                    currentByteIndex++;
                    currentBitPos = 0;
                    byteStarted = false;
                }

                if (!byteStarted)
                {
                    sb.AppendLine($"            byte packed{currentByteIndex} = 0;");
                    byteStarted = true;
                }

                // Pack Logic: (byte)((Value & Mask) << Shift)
                string valueRead = p.IsBool ? $"({p.Name} ? 1 : 0)" : $"(byte){p.Name}";
                sb.AppendLine($"            packed{currentByteIndex} |= (byte)({valueRead} << {currentBitPos});");

                currentBitPos += p.Bits;
            }

            // Write the final pending byte
            if (byteStarted)
            {
                sb.AppendLine($"            Write(buffer, ref cursor, packed{currentByteIndex});");
            }

            // --- STANDARD WRITES ---
            foreach (var prop in others)
            {
                string cast = GetWriteCast(prop);
                sb.AppendLine($"            Write(buffer, ref cursor, {cast}{prop.Name});");
            }
            sb.AppendLine("        }");

            // ---------------------------------------------------------
            // READ METHOD
            // ---------------------------------------------------------
            sb.AppendLine();
            sb.AppendLine("        public override void Read(ReadOnlySpan<byte> buffer, ref int cursor)");
            sb.AppendLine("        {");

            // --- UNPACKING LOOP ---
            currentByteIndex = 0;
            currentBitPos = 0;
            byteStarted = false;

            for (int i = 0; i < packables.Count; i++)
            {
                var p = packables[i];

                if (currentBitPos + p.Bits > 8)
                {
                    currentByteIndex++;
                    currentBitPos = 0;
                    byteStarted = false;
                }

                if (!byteStarted)
                {
                    sb.AppendLine($"            byte packed{currentByteIndex} = ReadByte(buffer, ref cursor);");
                    byteStarted = true;
                }

                // Unpack Logic: (Type)((packed >> Shift) & Mask)
                int mask = (1 << p.Bits) - 1;
                string cast = p.IsBool ? "" : $"({p.TypeName})";
                string extraction = $"(packed{currentByteIndex} >> {currentBitPos}) & {mask}";

                if (p.IsBool)
                    sb.AppendLine($"            {p.Name} = ({extraction}) != 0;");
                else
                    sb.AppendLine($"            {p.Name} = {cast}({extraction});");

                currentBitPos += p.Bits;
            }

            // --- STANDARD READS ---
            foreach (var prop in others)
            {
                string readCall = GetReadCall(prop);
                string cast = GetReadCast(prop);
                sb.AppendLine($"            {prop.Name} = {cast}{readCall};");
            }
            sb.AppendLine("        }");
            sb.AppendLine("    }");

            if (!string.IsNullOrEmpty(model.Namespace)) sb.AppendLine("}");

            context.AddSource($"{model.Name}.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        // --- HELPERS ---

        private static string GetWriteCast(PropModel p)
        {
            if (p.IsEnum) return "(byte)"; // Default for non-packed enums
            if (p.TypeName == "sbyte") return "(byte)";
            if (p.TypeName == "short") return "(ushort)";
            if (p.TypeName == "int") return "(uint)";
            if (p.TypeName == "long") return "(ulong)";
            return "";
        }

        private static string GetReadCall(PropModel p)
        {
            if (p.IsEnum) return "ReadByte(buffer, ref cursor)";

            // Signed
            if (p.TypeName == "sbyte") return "ReadByte(buffer, ref cursor)";
            if (p.TypeName == "short") return "ReadUInt16(buffer, ref cursor)";
            if (p.TypeName == "int") return "ReadUInt32(buffer, ref cursor)";
            if (p.TypeName == "long") return "ReadUInt64(buffer, ref cursor)";

            // Unsigned
            if (p.TypeName == "byte") return "ReadByte(buffer, ref cursor)";
            if (p.TypeName == "ushort") return "ReadUInt16(buffer, ref cursor)";
            if (p.TypeName == "uint") return "ReadUInt32(buffer, ref cursor)";
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
            if (!p.IsList && char.IsUpper(p.TypeName[0]) && p.TypeName != "String" && p.TypeName != "Byte" && !p.TypeName.StartsWith("List") && !p.TypeName.EndsWith("?"))
                return $"({p.TypeName})"; // Fallback cast for Objects
            return "";
        }
    }
}