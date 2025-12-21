using Api.Models;
using System.Text.Json;

namespace Serializer;

public static class DbBuilder
{
    public static void Asd(string metaFolder)
    {
        var files = Directory.GetFiles(metaFolder, "*.json");

        foreach (var file in files)
        {
            using var inputStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            var metData = JsonSerializer.Deserialize(inputStream, JsonContext.Default.MetaData);
        }
    }

}
