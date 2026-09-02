namespace HarryPotterExplorer.Models;

/// <summary>Generic page envelope returned by every list endpoint.</summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasMore => Page < TotalPages;
}

/// <summary>What a character card needs — nothing more, so the list payload stays small.</summary>
public sealed record CharacterSummary(
    string Id,
    string Name,
    string? House,
    string? Patronus,
    string? Species,
    string? ImageUrl,
    bool Alive,
    bool HogwartsStaff,
    int CollectCount);

/// <summary>Everything the detail page shows.</summary>
public sealed record CharacterDetail(
    string Id,
    string Name,
    IReadOnlyList<string> AlternateNames,
    string? House,
    string? Patronus,
    string? Species,
    string? Gender,
    string? Ancestry,
    string? DateOfBirth,
    int? YearOfBirth,
    string? EyeColour,
    string? HairColour,
    string? WandWood,
    string? WandCore,
    double? WandLength,
    bool Wizard,
    bool HogwartsStudent,
    bool HogwartsStaff,
    bool Alive,
    string? Actor,
    IReadOnlyList<string> AlternateActors,
    string? ImageUrl,
    int CollectCount);

public sealed record SpellSummary(string Id, string Name, string? Description, string Category);

public sealed record ArtifactSummary(
    string Id,
    string Name,
    string Category,
    string Description,
    string? Owner,
    string? FirstAppearance,
    string? Lore,
    string Glyph,
    int Rarity);

/// <summary>Static lore about one of the four houses. Not in the upstream API — curated here.</summary>
public sealed record HouseInfo(
    string Slug,
    string Name,
    string Founder,
    string Element,
    string AnimalSymbol,
    string Ghost,
    string CommonRoom,
    string HeadOfHouse,
    IReadOnlyList<string> Traits,
    string PrimaryColour,
    string SecondaryColour,
    string Crest,
    string Motto,
    string Description);

public sealed record HouseWithCount(HouseInfo House, int MemberCount);

public sealed record LedgerEntry(
    int Id,
    string CharacterId,
    string CharacterName,
    string? House,
    string Action,
    DateTime CreatedUtc);

public sealed record LiveStats(
    int Characters,
    int Spells,
    int Artifacts,
    int CollectedTotal,
    IReadOnlyDictionary<string, int> ByHouse,
    IReadOnlyList<CharacterSummary> TopCollected,
    IReadOnlyList<LedgerEntry> RecentEvents,
    SyncStatus Sync);

public sealed record SyncStatus(
    string State,
    DateTime? LastCompletedUtc,
    int CharactersUpserted,
    int SpellsUpserted,
    string? Error);
