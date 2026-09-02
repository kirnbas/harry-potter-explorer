using System.Text.Json;
using System.Text.Json.Serialization;
using HarryPotterExplorer.Data;
using Microsoft.EntityFrameworkCore;

namespace HarryPotterExplorer.Services;

/// <summary>
/// Creates the SQLite file on first run and loads the curated artefact dataset.
/// Artefacts are the one collection with no upstream source, so they ship with the app
/// and are re-applied on every start (idempotent upsert), which doubles as a way to
/// edit lore by editing JSON.
/// </summary>
public sealed class DatabaseInitializer(
    IWebHostEnvironment environment,
    ILogger<DatabaseInitializer> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task InitialiseAsync(HogwartsContext db, CancellationToken ct = default)
    {
        await db.Database.EnsureCreatedAsync(ct);
        await SeedArtifactsAsync(db, ct);
    }

    private async Task SeedArtifactsAsync(HogwartsContext db, CancellationToken ct)
    {
        var path = Path.Combine(environment.ContentRootPath, "SeedData", "artifacts.json");

        if (!File.Exists(path))
        {
            logger.LogWarning("Artefact seed file not found at {Path}; skipping", path);
            return;
        }

        await using var stream = File.OpenRead(path);
        var seeds = await JsonSerializer.DeserializeAsync<List<ArtifactSeed>>(stream, JsonOptions, ct)
                    ?? [];

        var existing = await db.Artifacts.ToDictionaryAsync(a => a.Id, ct);

        foreach (var seed in seeds)
        {
            if (string.IsNullOrWhiteSpace(seed.Id))
            {
                continue;
            }

            if (!existing.TryGetValue(seed.Id, out var entity))
            {
                entity = new ArtifactEntity { Id = seed.Id };
                db.Artifacts.Add(entity);
            }

            entity.Name = seed.Name;
            entity.Category = seed.Category;
            entity.Description = seed.Description;
            entity.Owner = seed.Owner;
            entity.FirstAppearance = seed.FirstAppearance;
            entity.Lore = seed.Lore;
            entity.Glyph = string.IsNullOrWhiteSpace(seed.Glyph) ? "✦" : seed.Glyph;
            entity.Rarity = Math.Clamp(seed.Rarity, 1, 5);
            entity.SearchIndex = $"{seed.Name} {seed.Category} {seed.Description} {seed.Owner}"
                .ToLowerInvariant();
        }

        var written = await db.SaveChangesAsync(ct);
        logger.LogInformation("Artefact vault ready ({Total} entries, {Written} row change(s))",
            seeds.Count, written);
    }

    private sealed class ArtifactSeed
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("category")] public string Category { get; set; } = string.Empty;
        [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
        [JsonPropertyName("owner")] public string? Owner { get; set; }
        [JsonPropertyName("firstAppearance")] public string? FirstAppearance { get; set; }
        [JsonPropertyName("lore")] public string? Lore { get; set; }
        [JsonPropertyName("glyph")] public string? Glyph { get; set; }
        [JsonPropertyName("rarity")] public int Rarity { get; set; } = 3;
    }
}
