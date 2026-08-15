namespace WorldBuilder.Core;

/// <summary>Which table an <see cref="EntityId"/> indexes into.</summary>
public enum EntityKind : byte
{
    None = 0,
    Actor = 1,
    Place = 2,
    Faction = 3,
    Arc = 4,
}

/// <summary>
/// A reference to any simulation entity. Kind-tagged so a single type can flow through
/// events, relations and goals without a discriminated union at every call site.
/// Renders as the short form used throughout the log: <c>a:112</c>, <c>p:14</c>, <c>f:2</c>, <c>w:3</c>.
/// </summary>
public readonly record struct EntityId(EntityKind Kind, int Index) : IComparable<EntityId>
{
    public static readonly EntityId None = new(EntityKind.None, 0);

    public static EntityId Actor(int i) => new(EntityKind.Actor, i);
    public static EntityId Place(int i) => new(EntityKind.Place, i);
    public static EntityId Faction(int i) => new(EntityKind.Faction, i);
    public static EntityId Arc(int i) => new(EntityKind.Arc, i);

    public bool IsNone => Kind == EntityKind.None;

    /// <summary>Stable 64-bit projection, used to seed derived RNG streams.</summary>
    public ulong Bits => ((ulong)Kind << 32) | (uint)Index;

    public int CompareTo(EntityId other)
    {
        int k = Kind.CompareTo(other.Kind);
        return k != 0 ? k : Index.CompareTo(other.Index);
    }

    public static bool operator <(EntityId a, EntityId b) => a.CompareTo(b) < 0;
    public static bool operator >(EntityId a, EntityId b) => a.CompareTo(b) > 0;
    public static bool operator <=(EntityId a, EntityId b) => a.CompareTo(b) <= 0;
    public static bool operator >=(EntityId a, EntityId b) => a.CompareTo(b) >= 0;

    public static char Sigil(EntityKind kind) => kind switch
    {
        EntityKind.Actor => 'a',
        EntityKind.Place => 'p',
        EntityKind.Faction => 'f',
        EntityKind.Arc => 'w',
        _ => '-',
    };

    public override string ToString() =>
        Kind == EntityKind.None ? "-" : $"{Sigil(Kind)}:{Index}";

    public static bool TryParse(string? text, out EntityId id)
    {
        id = None;
        if (string.IsNullOrWhiteSpace(text)) return false;

        ReadOnlySpan<char> s = text.Trim();
        int colon = s.IndexOf(':');
        if (colon != 1) return false;

        EntityKind kind = s[0] switch
        {
            'a' or 'A' => EntityKind.Actor,
            'p' or 'P' => EntityKind.Place,
            'f' or 'F' => EntityKind.Faction,
            'w' or 'W' => EntityKind.Arc,
            _ => EntityKind.None,
        };
        if (kind == EntityKind.None) return false;
        if (!int.TryParse(s[(colon + 1)..], out int index)) return false;

        id = new EntityId(kind, index);
        return true;
    }
}

/// <summary>Position of an event in the log. Monotonic within a run; renders as <c>e:1188</c>.</summary>
public readonly record struct EventId(int Value) : IComparable<EventId>
{
    public static readonly EventId None = new(0);

    /// <summary>Event ids are 1-based so that default(EventId) reads as "no cause".</summary>
    public bool IsNone => Value <= 0;

    public int CompareTo(EventId other) => Value.CompareTo(other.Value);

    public override string ToString() => IsNone ? "-" : $"e:{Value}";

    public static bool TryParse(string? text, out EventId id)
    {
        id = None;
        if (string.IsNullOrWhiteSpace(text)) return false;

        ReadOnlySpan<char> s = text.Trim();
        if (s.StartsWith("e:", StringComparison.OrdinalIgnoreCase)) s = s[2..];
        if (!int.TryParse(s, out int value) || value <= 0) return false;

        id = new EventId(value);
        return true;
    }
}
