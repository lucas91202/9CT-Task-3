using System.Collections.Generic;

 public interface IResettable
{
    void ResetToStart();
}

public static class ResetManager
{
    static List<IResettable> items = new List<IResettable>();

    public static void Register(IResettable r)
    {
        if (!items.Contains(r)) items.Add(r);
    }

    public static void Unregister(IResettable r)
    {
        items.Remove(r);
    }

    public static void ResetAll()
    {
        foreach(IResettable r in items.ToArray()) r.ResetToStart();
    }
}
