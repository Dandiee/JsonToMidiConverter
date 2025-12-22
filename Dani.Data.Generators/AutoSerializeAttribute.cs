using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Dani.Data.Generators;

[Generator]
public class AutoSerializeAttributeGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(ctx => ctx.AddSource(
            "AutoSerializeAttribute.g.cs",
            SourceText.From(@"using System;
                                       namespace Dani.Data.Generators
                                       {
                                           [AttributeUsage(AttributeTargets.Class)]
                                           public class AutoSerializeAttribute : Attribute { }
                                       }", Encoding.UTF8)));
    }
}