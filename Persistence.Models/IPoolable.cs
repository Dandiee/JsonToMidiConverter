namespace Persistence.Models;

public interface IPoolable
{
    void Return();
}

public abstract class Poolable<T> : IPoolable
    where T : Poolable<T>, new()
{
    public void Return()
    {
        ThreadLocalPool<T>.Return((T)this);
    }
}