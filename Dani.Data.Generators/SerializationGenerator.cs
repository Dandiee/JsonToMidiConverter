using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Dani.Data.Generators;

[Generator]
public class SerializationGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var provider = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: (s, _) => s is ClassDeclarationSyntax,
                transform: (ctx, _) => SerializationAnalyzer.GetClassModel(ctx, SerializationMode.Standard))
            .Where(m => m != null);

        context.RegisterSourceOutput(provider, Execute);
    }


    private static string GetWriter(ClassModel model)
    {
        var sb = new StringBuilder();
        var packables = model.Properties.Where(p => p.Bits > 0).OrderBy(p => p.Name).ToList();
        var payloadProps = model.Properties.Where(p => p.Bits == 0 || p.IsPackedNull).OrderBy(e => e.Name).ToList();

        int totalBits = packables.Sum(p => p.Bits);
        int totalBytes = (totalBits + 7) / 8;

        // 1. Declare Packed Bytes
        for (int i = 0; i < totalBytes; i++)
        {
            sb.AppendLine($"byte packed{i} = 0;");
        }

        // 2. Perform Bit Packing
        int currentGlobalBit = 0;
        foreach (var p in packables)
        {
            string valExpr = p switch
            {
                { IsBool: true } => $"({p.Name} ? 1 : 0)",
                { IsPackedNull: true } => $"({p.Name} != null ? 1 : 0)",
                _ => $"(byte){p.Name}"
            };

            int bitsRemaining = p.Bits;
            int valOffset = 0;

            while (bitsRemaining > 0)
            {
                int byteIndex = currentGlobalBit / 8;
                int bitIndexInByte = currentGlobalBit % 8;
                int spaceInByte = 8 - bitIndexInByte;
                int bitsToWrite = Math.Min(bitsRemaining, spaceInByte);
                int mask = (1 << bitsToWrite) - 1;

                sb.AppendLine($"packed{byteIndex} |= (byte)((({valExpr} >> {valOffset}) & {mask}) << {bitIndexInByte});");

                bitsRemaining -= bitsToWrite;
                valOffset += bitsToWrite;
                currentGlobalBit += bitsToWrite;
            }
        }

        // 3. Write Packed Bytes
        for (int i = 0; i < totalBytes; i++)
        {
            sb.AppendLine($"Write(buffer, ref cursor, packed{i});");
        }

        // 4. Write Payload
        foreach (var prop in payloadProps)
        {
            if (prop.IsPackedNull)
            {
                sb.AppendLine($@"if ({prop.Name} != null)
                                {{
                                    {prop.Name}.Write(buffer, ref cursor);
                                }}");
            }
            else
            {
                bool isPrimitive = GeneratorShared.IsPrimitive(prop.TypeName);
                if (isPrimitive || prop.IsList)
                {
                    string cast = GetWriteCast(prop);
                    sb.AppendLine($"Write(buffer, ref cursor, {cast}{prop.Name});");
                }
                else
                {
                    sb.AppendLine($"{prop.Name}.Write(buffer, ref cursor);");
                }
            }
        }

        return sb.ToString();
    }

    private static string GetReader(ClassModel model)
    {
        var sb = new StringBuilder();
        var packables = model.Properties.Where(p => p.Bits > 0).OrderBy(p => p.Name).ToList();
        var payloadProps = model.Properties.Where(p => p.Bits == 0 || p.IsPackedNull).OrderBy(e => e.Name).ToList();

        int totalBits = packables.Sum(p => p.Bits);
        int totalBytes = (totalBits + 7) / 8;

        // 1. Read Packed Bytes
        for (int i = 0; i < totalBytes; i++)
        {
            sb.AppendLine($"byte packed{i} = ReadByte(buffer, ref cursor);");
        }

        // 2. Unpack Bits
        int currentGlobalBit = 0;
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

            if (p.IsBool)
            {
                sb.AppendLine($"{p.Name} = ({combined}) != 0;");
            }
            else if (p.IsPackedNull)
            {
                sb.AppendLine($"bool has{p.Name} = ({combined}) != 0;");
            }
            else
            {
                string cast = $"({p.TypeName})";
                sb.AppendLine($"{p.Name} = {cast}{combined};");
            }
        }

        // 3. Read Payload
        foreach (var prop in payloadProps)
        {
            if (prop.IsPackedNull)
            {
                sb.AppendLine($@"if (has{prop.Name})
                                {{
                                    {prop.Name} = new {prop.CleanTypeName}();
                                    {prop.Name}.Read(buffer, ref cursor);
                                }}
                                else
                                {{
                                    {prop.Name} = null;
                                }}");
            }
            else
            {
                bool isPrimitive = GeneratorShared.IsPrimitive(prop.TypeName);
                if (isPrimitive || prop.IsList)
                {
                    string readCall = GetReadCall(prop);
                    string cast = GetReadCast(prop);
                    sb.AppendLine($"{prop.Name} = {cast}{readCall};");
                }
                else
                {
                    sb.AppendLine($"{prop.Name} = new {prop.CleanTypeName}();");
                    sb.AppendLine($"{prop.Name}.Read(buffer, ref cursor);");
                }
            }
        }

        return sb.ToString();
    }

    public static string FormatCode(string sourceCode)
    {
        var tree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = tree.GetRoot();
        var formattedRoot = root.NormalizeWhitespace();
        return formattedRoot.ToFullString();
    }

    private void Execute(SourceProductionContext context, ClassModel model)
    {
        var str =
            $@"using System;
            using System.Collections.Generic;
            using System.Buffers.Binary;
            using Dani.Data.Models;
            using Dani.Data.Models.Enums;
            using Dani.Data.Models.Parts;
            using Dani.Data.Models.Songs;

            namespace {model.Namespace}
            {{
                public partial class {model.Name}
                {{
                    public override void Write(Span<byte> buffer, ref int cursor)
                    {{
                        {GetWriter(model)}
                    }}

                    public override void Read(ReadOnlySpan<byte> buffer, ref int cursor)
                    {{
                        {GetReader(model)}
                    }}
                }}
            }}";

        context.AddSource($"{model.Name}.g.cs", SourceText.From(FormatCode(str), Encoding.UTF8));
    }

    // --- Helpers ---

    private static string GetWriteCast(PropModel p) => p.TypeName switch
    {
        "sbyte" or "byte" when p.IsEnum => "(byte)",
        "sbyte" => "(byte)",
        "short" => "(ushort)",
        "int" => "(uint)",
        "long" => "(ulong)",
        _ => ""
    };

    private static string GetReadCast(PropModel p) => p.TypeName switch
    {
        _ when p.IsEnum => $"({p.TypeName})",
        "sbyte" => "(sbyte)",
        "short" => "(short)",
        "int" => "(int)",
        "long" => "(long)",
        _ => ""
    };

    private static string GetReadCall(PropModel p)
    {
        if (p.IsEnum) return "ReadByte(buffer, ref cursor)";

        return p.TypeName switch
        {
            "sbyte" => "ReadByte(buffer, ref cursor)",
            "byte" => "ReadByte(buffer, ref cursor)",
            "short" => "ReadUInt16(buffer, ref cursor)",
            "ushort" => "ReadUInt16(buffer, ref cursor)",
            "int" => "ReadUInt32(buffer, ref cursor)",
            "uint" => "ReadUInt32(buffer, ref cursor)",
            "long" => "ReadUInt64(buffer, ref cursor)",
            "ulong" => "ReadUInt64(buffer, ref cursor)",
            "float" => "ReadSingle(buffer, ref cursor)",
            "string" => "ReadString(buffer, ref cursor)",
            "DateTime" => "ReadDateTime(buffer, ref cursor)",
            _ when p.IsList => GetListReadCall(p),
            _ => $"Read<{p.TypeName}>(buffer, ref cursor)"
        };
    }

    private static string GetListReadCall(PropModel p)
    {
        return p.ListInnerType switch
        {
            "byte" => "ReadList(buffer, ref cursor)",
            "sbyte" => "ReadSByteList(buffer, ref cursor)",
            "string" => "ReadStringList(buffer, ref cursor)",
            _ => $"ReadList<{p.ListInnerType}>(buffer, ref cursor)"
        };
    }
}