using JsonToMidiConverter.Models.Song;

namespace JsonToMidiConverter.Context
{
    public abstract class MusicalElement<T, TParent> where T : MusicalElement<T, TParent>
    {
        public Part Part { get; }
        public TParent Parent { get; }
        public int Index { get; }
        public T? Next { get; private set; }
        public T? Previous { get; }

        public Time Start { get; set; }
        public Time End { get; set; }
        public virtual Time Duration { get; set; }

        protected MusicalElement(Part part, TParent parent, int index, object? state = null)
        {
            Part = part;
            Parent = parent;
            Index = index;

            Previous = GetPrevious(state);
            if (Previous != null)
            {
                Previous.Next = (T)this;
            }
        }

        protected abstract T? GetPrevious(object? state = null);

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
