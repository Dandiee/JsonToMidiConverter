namespace Persistence.Models;

public static class ThreadLocalPool<T>
    where T : class, new()
{
    private const int Capacity = 1024*256;

    [ThreadStatic]
    private static Stack<T>? _pool;

    public static int Creates;
    public static int Pops;

    public static T Rent()
    {
        var pool = _pool ??= new Stack<T>(Capacity);
        if (pool.Count > 0)
        {
            Interlocked.Increment(ref Pops);
            return pool.Pop();
        }

        Interlocked.Increment(ref Creates);
        return new T();
    }

    public static void Return(T item)
    {
        _pool ??= new Stack<T>(Capacity);
        _pool.Push(item);
    }
}