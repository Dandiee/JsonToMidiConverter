using Microsoft.CodeAnalysis;
using System;
using System.Text;
using Microsoft.CodeAnalysis.Text;

namespace Api.Models.Generator
{

    [Generator]
    public class AutoSerializeAttributeGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterPostInitializationOutput(ctx => ctx.AddSource(
                "AutoSerializeAttribute.g.cs",
                SourceText.From(@"using System;
                                       namespace Api.Generators
                                       {
                                           [AttributeUsage(AttributeTargets.Class)]
                                           public class AutoSerializeAttribute : Attribute { }
                                       }", Encoding.UTF8)));
        }
    }
}