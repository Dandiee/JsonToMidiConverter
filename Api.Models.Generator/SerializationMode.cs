namespace Api.Models.Generator;

internal enum SerializationMode
{
    Standard, // Uses AutoSerializeAttribute
    Index     // Uses AutoSerializeIndexAttribute
}