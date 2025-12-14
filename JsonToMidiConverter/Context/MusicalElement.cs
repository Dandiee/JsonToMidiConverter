

using JsonToMidiConverter.Models.Song;

namespace JsonToMidiConverter.Context
{
    public interface IMusicalElement<T> where T : IMusicalElement<T>
    {
        public Part Part { get; set; }
        public int Index { get; set; }
        public T? Next { get; set; }
        public T? Previous { get; set; }

        public Time Start { get; set; }
        public Time End { get; set; }
        //public abstract Time Duration { get; set; }

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
