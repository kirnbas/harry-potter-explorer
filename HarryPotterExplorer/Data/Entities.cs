using System.ComponentModel.DataAnnotations;

namespace HarryPotterExplorer.Data;

/// <summary>
/// A wizarding-world character, mirrored from the public Harry Potter API into
/// our own store so that the site keeps working when the upstream API is asleep.
/// </summary>
public class CharacterEntity
{
    [Key]
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Lower-cased name + alternate names, used for case-insensitive search on SQLite.</summary>
    public string SearchIndex { get; set; } = string.Empty;

    public string? AlternateNames { get; set; }
    public string? Species { get; set; }
    public string? Gender { get; set; }
    public string? House { get; set; }
    public string? DateOfBirth { get; set; }
    public int? YearOfBirth { get; set; }
    public bool Wizard { get; set; }
    public string? Ancestry { get; set; }
    public string? EyeColour { get; set; }
    public string? HairColour { get; set; }
    public string? WandWood { get; set; }
    public string? WandCore { get; set; }
    public double? WandLength { get; set; }
    public string? Patronus { get; set; }
    public bool HogwartsStudent { get; set; }
    public bool HogwartsStaff { get; set; }
    public string? Actor { get; set; }
    public string? AlternateActors { get; set; }
    public bool Alive { get; set; }
    public string? ImageUrl { get; set; }

    public DateTime LastSyncedUtc { get; set; }
}

/// <summary>A spell or charm, mirrored from the upstream API.</summary>
public class SpellEntity
{
    [Key]
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string SearchIndex { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Derived locally: Charm / Curse / Jinx / Hex / Transfiguration / Spell.</summary>
    public string Category { get; set; } = "Spell";

    public DateTime LastSyncedUtc { get; set; }
}

/// <summary>
/// A magical artefact. The upstream API has no artefact endpoint, so this table is
/// seeded from a curated dataset shipped with the app (SeedData/artifacts.json).
/// </summary>
public class ArtifactEntity
{
    [Key]
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string SearchIndex { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Owner { get; set; }
    public string? FirstAppearance { get; set; }
    public string? Lore { get; set; }
    public string Glyph { get; set; } = "✦";
    public int Rarity { get; set; } = 3;
}

/// <summary>Running tally of how many visitors keep a character in their collection.</summary>
public class CharacterStatEntity
{
    [Key]
    public string CharacterId { get; set; } = string.Empty;

    public string CharacterName { get; set; } = string.Empty;
    public string? House { get; set; }
    public string? ImageUrl { get; set; }
    public int CollectCount { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

/// <summary>
/// Append-only ledger of collection events. This is what the /live page streams,
/// so visitors can watch the database change in real time.
/// </summary>
public class LedgerEventEntity
{
    [Key]
    public int Id { get; set; }

    public string CharacterId { get; set; } = string.Empty;
    public string CharacterName { get; set; } = string.Empty;
    public string? House { get; set; }

    /// <summary>"collected" or "released".</summary>
    public string Action { get; set; } = "collected";

    /// <summary>Anonymous per-browser id, so we can de-duplicate without any accounts.</summary>
    public string VisitorId { get; set; } = string.Empty;

    public DateTime CreatedUtc { get; set; }
}

/// <summary>One row per attempt to refresh our mirror from the upstream API.</summary>
public class SyncRunEntity
{
    [Key]
    public int Id { get; set; }

    public DateTime StartedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public string Status { get; set; } = "running";
    public int CharactersUpserted { get; set; }
    public int SpellsUpserted { get; set; }
    public string? Error { get; set; }
}
