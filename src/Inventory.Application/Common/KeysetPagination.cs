using System.Globalization;

namespace Inventory.Application.Common;

/// <summary>Task 5.13 - one page of a `company_id, created_at DESC, id DESC` keyset-paginated
/// list (docs/21 §1). <see cref="NextCursor"/> is null on the last page.</summary>
public sealed record KeysetPage<T>(IReadOnlyList<T> Items, string? NextCursor);

/// <summary>
/// The cursor-encoding half of keyset pagination, shared by every list endpoint. Each endpoint
/// still writes its own `OrderByDescending(CreatedAt).ThenByDescending(Id)` +
/// `Where(CreatedAt &lt; cursor.CreatedAt || (CreatedAt == cursor.CreatedAt &amp;&amp; Id &lt;
/// cursor.Id))` query - EF Core's LINQ-to-SQL translation of a genuinely generic keyset
/// predicate across unrelated entity types is not reliable enough to centralize safely - but
/// requesting one extra row, deciding whether a next page exists, and encoding/decoding the
/// resulting cursor is identical everywhere and lives here once.
/// </summary>
public static class KeysetPagination
{
    public const int DefaultLimit = 25;
    public const int MaxLimit = 100;

    public readonly record struct Cursor(DateTimeOffset CreatedAt, Guid Id);

    /// <summary>Clamps a client-requested page size into `[1, 100]`, defaulting to 25 when not
    /// supplied (docs/21 §1).</summary>
    public static int ClampLimit(int? requested) =>
        requested is null or <= 0 ? DefaultLimit : Math.Min(requested.Value, MaxLimit);

    /// <summary>Null for a missing/malformed cursor - callers should treat that as "first
    /// page", not an error, since a stale or hand-edited cursor is not the caller's fault to
    /// diagnose.</summary>
    public static Cursor? DecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }

        try
        {
            string decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            string[] parts = decoded.Split('|');
            if (parts.Length != 2)
            {
                return null;
            }

            long ticks = long.Parse(parts[0], CultureInfo.InvariantCulture);
            Guid id = Guid.Parse(parts[1]);
            return new Cursor(new DateTimeOffset(ticks, TimeSpan.Zero), id);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException)
        {
            return null;
        }
    }

    private static string EncodeCursor(Cursor cursor) =>
        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
            $"{cursor.CreatedAt.UtcTicks.ToString(CultureInfo.InvariantCulture)}|{cursor.Id}"));

    /// <summary>Builds one page from a list fetched with <c>Take(limit + 1)</c> - the extra row,
    /// if present, means another page exists and is trimmed off before returning.</summary>
    public static KeysetPage<T> BuildPage<T>(
        IReadOnlyList<T> overFetched, int limit, Func<T, DateTimeOffset> createdAtSelector, Func<T, Guid> idSelector)
    {
        bool hasMore = overFetched.Count > limit;
        IReadOnlyList<T> page = hasMore ? overFetched.Take(limit).ToList() : overFetched;
        string? nextCursor = hasMore && page.Count > 0
            ? EncodeCursor(new Cursor(createdAtSelector(page[^1]), idSelector(page[^1])))
            : null;

        return new KeysetPage<T>(page, nextCursor);
    }
}
