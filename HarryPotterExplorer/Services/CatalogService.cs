using System.Text.Json;
using HarryPotterExplorer.Data;
using HarryPotterExplorer.Models;
using Microsoft.EntityFrameworkCore;

namespace HarryPotterExplorer.Services;

/// <summary>Everything the character catalogue can be narrowed by, in one object.</summary>
public sealed record CharacterQuery
{
    public string? Search { get; init; }
    public string? House { get; init; }
    public string? Species { get; init; }

    /// <summary>"student", "staff" or null for everyone.</summary>
    public string? Role { get; init; }

    /// <summary>"alive", "deceased" or null.</summary>
    public string? Status { get; init; }

    /// <summary>When true, only characters the upstream API has a portrait for.</summary>
    public bool WithImage { get; init; }

    /// <summary>"name", "house" or "popular".</summary>
    public string Sort { get; init; } = "name";

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 24;
}

public sealed record CatalogFacets(
    IReadOnlyList<string> Houses,
    IReadOnlyList<string> Species,
    IReadOnlyList<string> SpellCategories,
    IReadOnlyList<string> ArtifactCategories);

public interface ICatalogService
{
    Task<PagedResult<CharacterSummary>> SearchCharactersAsync(CharacterQuery query, CancellationToken ct = default);
    Task<CharacterDetail?> GetCharacterAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<CharacterSummary>> GetCharactersByIdsAsync(IReadOnlyCollection<string> ids, CancellationToken ct = default);
    Task<IReadOnlyList<CharacterSummary>> GetCharactersByNamesAsync(IReadOnlyList<string> names, CancellationToken ct = default);
    Task<IReadOnlyList<CharacterSummary>> GetHouseMembersAsync(string house, int take, CancellationToken ct = default);
    Task<PagedResult<SpellSummary>> SearchSpellsAsync(string? search, string? category, int page, int pageSize, CancellationToken ct = default);
    Task<IReadOnlyList<ArtifactSummary>> GetArtifactsAsync(string? search, string? category, CancellationToken ct = default);
    Task<CatalogFacets> GetFacetsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<HouseWithCount>> GetHousesAsync(CancellationToken ct = default);
    Task<LiveStats> GetLiveStatsAsync(CancellationToken ct = default);
}

public sealed class CatalogService(
    HogwartsContext db,
    IHouseCatalog houses,
    ICatalogSyncCoordinator sync) : ICatalogService
{
    public const int MaxPageSize = 60;

    /// <summary>The whole spellbook is under a hundred short rows, so it may be asked for at once.</summary>
    public const int MaxSpellPageSize = 200;

    public async Task<PagedResult<CharacterSummary>> SearchCharactersAsync(
        CharacterQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        var filtered = ApplyFilters(db.Characters.AsNoTracking(), query);
        var total = await filtered.CountAsync(ct);

        var ordered = query.Sort switch
        {
            "house" => filtered.OrderBy(c => c.House ?? "zzz").ThenBy(c => c.Name),
            "popular" => from c in filtered
                         join s in db.CharacterStats.AsNoTracking()
                             on c.Id equals s.CharacterId into stats
                         from s in stats.DefaultIfEmpty()
                         orderby (s == null ? 0 : s.CollectCount) descending, c.Name
                         select c,
            _ => filtered.OrderBy(c => c.Name)
        };

        var rows = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = await ToSummariesAsync(rows, ct);
        return new PagedResult<CharacterSummary>(items, page, pageSize, total);
    }

    public async Task<CharacterDetail?> GetCharacterAsync(string id, CancellationToken ct = default)
    {
        var entity = await db.Characters.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);

        if (entity is null)
        {
            return null;
        }

        var stat = await db.CharacterStats.AsNoTracking()
            .FirstOrDefaultAsync(s => s.CharacterId == id, ct);

        return new CharacterDetail(
            entity.Id,
            entity.Name,
            Deserialise(entity.AlternateNames),
            entity.House,
            entity.Patronus,
            entity.Species,
            entity.Gender,
            entity.Ancestry,
            entity.DateOfBirth,
            entity.YearOfBirth,
            entity.EyeColour,
            entity.HairColour,
            entity.WandWood,
            entity.WandCore,
            entity.WandLength,
            entity.Wizard,
            entity.HogwartsStudent,
            entity.HogwartsStaff,
            entity.Alive,
            entity.Actor,
            Deserialise(entity.AlternateActors),
            entity.ImageUrl,
            stat?.CollectCount ?? 0);
    }

    public async Task<IReadOnlyList<CharacterSummary>> GetCharactersByIdsAsync(
        IReadOnlyCollection<string> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var rows = await db.Characters.AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .ToListAsync(ct);

        // Preserve the order the caller asked for: a collection should stay in the
        // order the visitor built it, not in database order.
        var order = ids.Select((id, index) => (id, index))
            .ToDictionary(x => x.id, x => x.index);

        var ordered = rows.OrderBy(c => order.TryGetValue(c.Id, out var i) ? i : int.MaxValue).ToList();
        return await ToSummariesAsync(ordered, ct);
    }

    public async Task<IReadOnlyList<CharacterSummary>> GetCharactersByNamesAsync(
        IReadOnlyList<string> names, CancellationToken ct = default)
    {
        if (names.Count == 0)
        {
            return [];
        }

        var rows = await db.Characters.AsNoTracking()
            .Where(c => names.Contains(c.Name))
            .ToListAsync(ct);

        var order = names.Select((name, index) => (name, index))
            .ToDictionary(x => x.name, x => x.index);

        var ordered = rows
            .OrderBy(c => order.TryGetValue(c.Name, out var i) ? i : int.MaxValue)
            .ToList();

        return await ToSummariesAsync(ordered, ct);
    }

    public async Task<IReadOnlyList<CharacterSummary>> GetHouseMembersAsync(
        string house, int take, CancellationToken ct = default)
    {
        var rows = await db.Characters.AsNoTracking()
            .Where(c => c.House == house)
            // Named characters with a portrait first: they make the far better preview.
            .OrderByDescending(c => c.ImageUrl != null && c.ImageUrl != "")
            .ThenByDescending(c => c.HogwartsStaff)
            .ThenBy(c => c.Name)
            .Take(take)
            .ToListAsync(ct);

        return await ToSummariesAsync(rows, ct);
    }

    public async Task<PagedResult<SpellSummary>> SearchSpellsAsync(
        string? search, string? category, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxSpellPageSize);

        var query = db.Spells.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(s => s.SearchIndex.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(s => s.Category == category);
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderBy(s => s.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new SpellSummary(s.Id, s.Name, s.Description, s.Category))
            .ToListAsync(ct);

        return new PagedResult<SpellSummary>(items, page, pageSize, total);
    }

    public async Task<IReadOnlyList<ArtifactSummary>> GetArtifactsAsync(
        string? search, string? category, CancellationToken ct = default)
    {
        var query = db.Artifacts.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(a => a.SearchIndex.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(a => a.Category == category);
        }

        return await query
            .OrderByDescending(a => a.Rarity)
            .ThenBy(a => a.Name)
            .Select(a => new ArtifactSummary(
                a.Id, a.Name, a.Category, a.Description, a.Owner,
                a.FirstAppearance, a.Lore, a.Glyph, a.Rarity))
            .ToListAsync(ct);
    }

    public async Task<CatalogFacets> GetFacetsAsync(CancellationToken ct = default)
    {
        var houseNames = await db.Characters.AsNoTracking()
            .Where(c => c.House != null && c.House != "")
            .Select(c => c.House!)
            .Distinct()
            .OrderBy(h => h)
            .ToListAsync(ct);

        var species = await db.Characters.AsNoTracking()
            .Where(c => c.Species != null && c.Species != "")
            .Select(c => c.Species!)
            .Distinct()
            .OrderBy(s => s)
            .ToListAsync(ct);

        var spellCategories = await db.Spells.AsNoTracking()
            .Select(s => s.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(ct);

        var artifactCategories = await db.Artifacts.AsNoTracking()
            .Select(a => a.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(ct);

        return new CatalogFacets(houseNames, species, spellCategories, artifactCategories);
    }

    public async Task<IReadOnlyList<HouseWithCount>> GetHousesAsync(CancellationToken ct = default)
    {
        var counts = await db.Characters.AsNoTracking()
            .Where(c => c.House != null && c.House != "")
            .GroupBy(c => c.House!)
            .Select(g => new { House = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.House, x => x.Count, ct);

        return houses.All
            .Select(h => new HouseWithCount(h, counts.GetValueOrDefault(h.Name, 0)))
            .ToList();
    }

    public async Task<LiveStats> GetLiveStatsAsync(CancellationToken ct = default)
    {
        var characterCount = await db.Characters.CountAsync(ct);
        var spellCount = await db.Spells.CountAsync(ct);
        var artifactCount = await db.Artifacts.CountAsync(ct);

        var collectedTotal = await db.CharacterStats.AsNoTracking()
            .SumAsync(s => (int?)s.CollectCount, ct) ?? 0;

        var byHouse = await db.Characters.AsNoTracking()
            .Where(c => c.House != null && c.House != "")
            .GroupBy(c => c.House!)
            .Select(g => new { House = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.House, x => x.Count, ct);

        var topStats = await db.CharacterStats.AsNoTracking()
            .Where(s => s.CollectCount > 0)
            .OrderByDescending(s => s.CollectCount)
            .ThenBy(s => s.CharacterName)
            .Take(8)
            .ToListAsync(ct);

        var top = topStats
            .Select(s => new CharacterSummary(
                s.CharacterId, s.CharacterName, s.House, null, null, s.ImageUrl,
                true, false, s.CollectCount))
            .ToList();

        var recent = await db.LedgerEvents.AsNoTracking()
            .OrderByDescending(l => l.Id)
            .Take(25)
            .Select(l => new LedgerEntry(
                l.Id, l.CharacterId, l.CharacterName, l.House, l.Action, l.CreatedUtc))
            .ToListAsync(ct);

        return new LiveStats(
            characterCount, spellCount, artifactCount, collectedTotal,
            byHouse, top, recent, sync.Current);
    }

    private static IQueryable<CharacterEntity> ApplyFilters(
        IQueryable<CharacterEntity> query, CharacterQuery filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            // SearchIndex is pre-lowered at sync time so this stays a plain LIKE on SQLite.
            var term = filter.Search.Trim().ToLowerInvariant();
            query = query.Where(c => c.SearchIndex.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(filter.House))
        {
            query = filter.House.Equals("unsorted", StringComparison.OrdinalIgnoreCase)
                ? query.Where(c => c.House == null || c.House == "")
                : query.Where(c => c.House == filter.House);
        }

        if (!string.IsNullOrWhiteSpace(filter.Species))
        {
            query = query.Where(c => c.Species == filter.Species);
        }

        query = filter.Role switch
        {
            "student" => query.Where(c => c.HogwartsStudent),
            "staff" => query.Where(c => c.HogwartsStaff),
            _ => query
        };

        query = filter.Status switch
        {
            "alive" => query.Where(c => c.Alive),
            "deceased" => query.Where(c => !c.Alive),
            _ => query
        };

        if (filter.WithImage)
        {
            query = query.Where(c => c.ImageUrl != null && c.ImageUrl != "");
        }

        return query;
    }

    private async Task<IReadOnlyList<CharacterSummary>> ToSummariesAsync(
        List<CharacterEntity> rows, CancellationToken ct)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        var ids = rows.Select(r => r.Id).ToList();

        var counts = await db.CharacterStats.AsNoTracking()
            .Where(s => ids.Contains(s.CharacterId))
            .ToDictionaryAsync(s => s.CharacterId, s => s.CollectCount, ct);

        return rows.Select(r => new CharacterSummary(
            r.Id, r.Name, r.House, r.Patronus, r.Species, r.ImageUrl,
            r.Alive, r.HogwartsStaff, counts.GetValueOrDefault(r.Id, 0))).ToList();
    }

    private static IReadOnlyList<string> Deserialise(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
