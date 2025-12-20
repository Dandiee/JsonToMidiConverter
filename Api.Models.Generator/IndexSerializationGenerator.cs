using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Text;

namespace Api.Models.Generator;

[Generator]
public class IndexSerializationGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var provider = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: (s, _) => s is ClassDeclarationSyntax,
                transform: (ctx, _) => SerializationAnalyzer.GetClassModel(ctx, SerializationMode.Index))
            .Where(m => m != null);

        context.RegisterSourceOutput(provider, Execute);
    }
    
    private static string GetHashBuilder(ClassModel model)
    {
        var sb = new StringBuilder();

        int currentBit = 0;
        foreach (var p in model.GetIndexableProps())
        {
            string valExpr = p switch
            {
                { IsBool: true } => $"({p.Name} ? 1 : 0)",
                { TypeName: "MusicalFraction" } => $"(ulong)(({p.Name}.Nominator) | ({p.Name}.Denominator << 8))",
                _ => $"(ulong){p.CastForBitPacking}{p.Name}"
            };

            sb.AppendLine($"key |= ((UInt128){valExpr} << {currentBit});");
            currentBit += p.Bits;
        }

        return sb.ToString();
    }

    private static string GetReferenceWriter(ClassModel model)
    {
        var sb = new StringBuilder();
        foreach (var prop in model.GetReferenceProps())
        {
            if (prop.TypeName.Contains("List") || prop.TypeName == "string")
            {
                sb.AppendLine($"Write(buffer, ref cursor, {prop.Name});");
            }
            else
            {
                sb.AppendLine(
                    $@"if ({prop.Name} != null)
                        {{
                            Write(buffer, ref cursor, (byte)1);
                            {prop.Name}.Write(buffer, ref cursor);
                        }}
                        else
                        {{
                             Write(buffer, ref cursor, (byte)0);
                        }}");
            }
        }

        return sb.ToString();
    }

    private static string GetPrimitiveReader(ClassModel model)
    {
        int currentBit = 0;
        var sb = new StringBuilder();
        foreach (var p in model.GetIndexableProps())
        {
            var mask = p.Bits == 64
                ? "ulong.MaxValue"
                : $"(((ulong)1 << {p.Bits}) - 1)";

            var extraction = $"(ulong)((key >> {currentBit}) & {mask})";

            if (p.IsBool)
            {
                sb.AppendLine($"{p.Name} = ({extraction}) != 0;");
            }
            else if (p.TypeName == "MusicalFraction")
            {
                sb.AppendLine($"ulong fracVal{p.Name} = {extraction};");
                sb.AppendLine($"{p.Name} = new MusicalFraction((byte)(fracVal{p.Name} & 0xFF), (byte)((fracVal{p.Name} >> 8) & 0xFF));");
            }
            else
            {
                var cast = p switch
                {
                    { TypeName: "sbyte" } => "(sbyte)(byte)",
                    { TypeName: "short" } => "(short)(ushort)",
                    _ => $"({p.TypeName})"
                };
                    
                sb.AppendLine($" {p.Name} = {cast}({extraction});");
            }

            currentBit += p.Bits;
        }

        return sb.ToString();
    }

    private static string GetReferenceReader(ClassModel model)
    {
        var sb = new StringBuilder();

        foreach (var prop in model.GetReferenceProps())
        {
            if (prop.TypeName.Contains("List"))
            {
                sb.AppendLine($"{prop.Name} = ReadList<{prop.ListInnerType}>(buffer, ref cursor);");
            }
            else if (prop.TypeName == "string")
            {
                sb.AppendLine($"{prop.Name} = ReadString(buffer, ref cursor);");
            }
            else
            {
                sb.AppendLine(
                    @$"if (ReadByte(buffer, ref cursor) == 1)
                        {{
                             {prop.Name} = new {prop.CleanTypeName}();
                             {prop.Name}.Read(buffer, ref cursor);
                        }}
                        else
                        {{
                             {prop.Name} = null;
                        }}");
            }
        }

        return sb.ToString();
    }

    private void Execute(SourceProductionContext context, ClassModel model)
    {
        var str =
            $@"using System;
            using System.Collections.Generic;
            using System.Collections.Concurrent;
            using System.Buffers.Binary;
            using Api.Models;
            using Api.Models.Enums;
            
            namespace {model.Namespace}
            {{
                public partial class {model.Name}
                {{
                    private static readonly ConcurrentDictionary<UInt128, ushort> _indexMap = new();
                    private static List<UInt128> _valueList = new();
                    private static readonly object _listLock = new();
                    
                    public override void Write(Span<byte> buffer, ref int cursor)
                    {{
                        UInt128 key = 0;
            
                        {GetHashBuilder(model)}
            
                        if (!_indexMap.TryGetValue(key, out ushort index))
                        {{
                            lock (_listLock)
                            {{
                                if (!_indexMap.TryGetValue(key, out index))
                                {{
                                    index = (ushort)_valueList.Count;
                                    _valueList.Add(key);
                                    _indexMap[key] = index;
                                }}
                            }}
                        }}
                        Write(buffer, ref cursor, (ushort)index);
            
                        {GetReferenceWriter(model)}
                    }}
            
                    public override void Read(ReadOnlySpan<byte> buffer, ref int cursor)
                    {{
                        ushort index = (ushort)ReadUInt16(buffer, ref cursor);
                        UInt128 key = _valueList[index];
                        
                        {GetPrimitiveReader(model)}
                        {GetReferenceReader(model)}
                    }}

                    public static List<UInt128> GetHeaders() => _valueList.ToList();
                    public static void LoadHeaders(List<UInt128> headers) => _valueList = headers.ToList();
                }}
            }}";


        context.AddSource($"{model.Name}_Index.g.cs", SourceText.From(FormatCode(str), Encoding.UTF8));
    }

    public static string FormatCode(string sourceCode)
    {
        var tree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = tree.GetRoot();
        var formattedRoot = root.NormalizeWhitespace();
        return formattedRoot.ToFullString();
    }
}