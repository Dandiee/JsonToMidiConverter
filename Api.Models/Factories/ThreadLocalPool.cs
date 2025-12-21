namespace Api.Models.Mappers;

public static class ThreadLocalPool<T>
    where T : class, new()
{
    private const int Capacity = 1024 * 256;

    [ThreadStatic]
    private static Stack<T>? _pool;

    public static T Rent()
    {
        var pool = _pool ??= new Stack<T>(Capacity);
        if (pool.Count > 0)
        {
            return pool.Pop();
        }

        return new T();
    }

    public static void Return(T item)
    {
        _pool ??= new Stack<T>(Capacity);
        _pool.Push(item);
    }
}