

using JsonToMidiConverter.Models.Song;
using System.Text.Json.Serialization;

namespace JsonToMidiConverter.Context
{
    public class MusicalElement<T> where T : MusicalElement<T>
    {
        [JsonIgnore] public Part Part { get; set; }
        [JsonIgnore] public int Index { get; set; }
        [JsonIgnore] public T? Next { get; set; }
        [JsonIgnore] public T? Previous { get; set; }
        [JsonIgnore] public bool IsLast { get; set; }
        [JsonIgnore] public bool IsFirst { get; set; }

        [JsonIgnore] public Time Start { get; set; }
        [JsonIgnore] public Time End { get; set; }
        [JsonIgnore] public virtual Time Duration { get; set; }

        public IEnumerable<T> Forward()
        {
            var current = this;
            while (current != null)
            {
                yield return (T)current;
                current = current.Next;
            }
        }

        public IEnumerable<T> Backward()
        {
            var current = this;
            while (current != null)
            {
                yield return (T)current;
                current = current.Previous;
            }
        }

        public bool Is(string name, string? filter = null)
        {
            if (string.IsNullOrEmpty(name)) return false;
            var trimmed = name.Trim().ToUpperInvariant();
            var isMatching = $"{this}".Contains(trimmed);
            return isMatching && (string.IsNullOrEmpty(filter) || Part.FullName.Contains(filter, StringComparison.OrdinalIgnoreCase));
        }
    }
}
