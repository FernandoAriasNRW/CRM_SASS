namespace BuildingBlocks.Domain.Primitives;

public abstract class Enumeration<TValue>(TValue value, string name) : IComparable
    where TValue : IComparable
{
    public TValue Value { get; init; } = value;
    public string Name { get; init; } = name;

    public int CompareTo(object? obj)
    {
        if (obj is Enumeration<TValue> other)
            return Value.CompareTo(other.Value);
        return 0;
    }

    public override string ToString() => Name;

    public static IReadOnlyList<T> GetAll<T>() where T : Enumeration<TValue>
    {
        return typeof(T)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly)
            .Select(f => f.GetValue(null)!)
            .Cast<T>()
            .ToList()
            .AsReadOnly();
    }

    public static T? FromName<T>(string name) where T : Enumeration<TValue>
    {
        return GetAll<T>().FirstOrDefault(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public static T? FromValue<T>(TValue value) where T : Enumeration<TValue>
    {
        return GetAll<T>().FirstOrDefault(e => e.Value.CompareTo(value) == 0);
    }

    public override bool Equals(object? obj)
    {
        if (obj is not Enumeration<TValue> other) return false;
        return GetType() == other.GetType() && Value.CompareTo(other.Value) == 0;
    }

    public override int GetHashCode() => Value.GetHashCode();
}

// Backward-compatible alias for int-based enumerations (UserRole)
public abstract class Enumeration(int value, string name) : Enumeration<int>(value, name)
{
    public static new T FromValue<T>(int value) where T : Enumeration
    {
        return GetAll<T>().FirstOrDefault(e => e.Value == value)
            ?? throw new InvalidOperationException($"'{value}' is not a valid value for {typeof(T).Name}");
    }
}
