using System.Text.Json;
using HarryPotterExplorer.Data;
using HarryPotterExplorer.Hubs;
using HarryPotterExplorer.Models;
using HarryPotterExplorer.Models.External;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace HarryPotterExplorer.Services;

public interface ICatalogSyncCoordinator
{
    SyncStatus Current { get; }

    /// <summary>Refreshes the local mirror from the upstream API. Only one run at a time.</summary>
    Task<SyncStatus> SyncAsync(CancellationToken ct = default);
}

/// <summary>
/// Owns the "mirror, do not proxy" strategy: the upstream API is read in bulk on a
/// schedule and written into SQLite, and every page the visitor sees is served from
/// SQLite. If the upstream host is cold, asleep or gone, the site still works - it just
/// reports a stale sync state.
/// </summary>
public sealed class CatalogSyncCoordinator(
    IServiceScopeFactory scopeFactory,
    IHubContext<GreatHallHub> hub,
    ILogger<CatalogSyncCoordinator> logger) : ICatalogSyncCoordinator
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    private SyncStatus _current = new("idle", null, 0, 0, null);

    public SyncStatus Current => _current;

    public async Task<SyncStatus> SyncAsync(CancellationToken ct = default)
    {
        if (!await _gate.WaitAsync(TimeSpan.Zero, ct))
        {
            logger.LogInformation("A sync is already in flight; returning its current state");
            return _current;
        }

        try
        {
            await PublishAsync(_current with { State = "syncing", Error = null });

            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<HogwartsContext>();

            // Resolved per run rather than injected: a typed HttpClient captured by a
            // singleton never rotates its handler, and this coordinator is a singleton.
            var apiClient = scope.ServiceProvider.GetRequiredService<IHarryPotterApiClient>();

            var run = new SyncRunEntity { StartedUtc = DateTime.UtcNow, Status = "running" };
            db.SyncRuns.Add(run);
            await db.SaveChangesAsync(ct);

            try
            {
                var charactersTask = apiClient.GetCharactersAsync(ct);
                var spellsTask = apiClient.GetSpellsAsync(ct);
                await Task.WhenAll(charactersTask, spellsTask);

                var characterCount = await UpsertCharactersAsync(db, await charactersTask, ct);
                var spellCount = await UpsertSpellsAsync(db, await spellsTask, ct);

                run.CharactersUpserted = characterCount;
                run.SpellsUpserted = spellCount;
                run.Status = "succeeded";
                run.CompletedUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);

                logger.LogInformation(
                    "Mirror refreshed: {Characters} character(s), {Spells} spell(s)",
                    characterCount, spellCount);

                await PublishAsync(new SyncStatus(
                    "ready", run.CompletedUtc, characterCount, spellCount, null));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                run.Status = "failed";
                run.CompletedUtc = DateTime.UtcNow;
                run.Error = ex.Message;
                await db.SaveChangesAsync(CancellationToken.None);

                logger.LogError(ex, "Mirror refresh failed; serving whatever is already stored");

                var hasData = await db.Characters.AnyAsync(CancellationToken.None);
                await PublishAsync(_current with
                {
                    State = hasData ? "stale" : "failed",
                    Error = ex.Message
                });
            }

            return _current;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task PublishAsync(SyncStatus status)
    {
        _current = status;
        await hub.Clients.All.SendAsync(GreatHallHub.SyncEvent, status);
    }

    private static async Task<int> UpsertCharactersAsync(
        HogwartsContext db, IReadOnlyList<HpApiCharacter> incoming, CancellationToken ct)
    {
        var existing = await db.Characters.ToDictionaryAsync(c => c.Id, ct);
        var now = DateTime.UtcNow;
        var touched = 0;

        foreach (var source in incoming)
        {
            // A few upstream rows have no id; skip them rather than inventing one.
            if (string.IsNullOrWhiteSpace(source.Id) || string.IsNullOrWhiteSpace(source.Name))
            {
                continue;
            }

            if (!existing.TryGetValue(source.Id, out var entity))
            {
                entity = new CharacterEntity { Id = source.Id };
                db.Characters.Add(entity);
                existing[source.Id] = entity;
            }

            var alternateNames = source.AlternateNames ?? [];

            entity.Name = source.Name;
            entity.AlternateNames = JsonSerializer.Serialize(alternateNames);
            entity.SearchIndex = BuildSearchIndex(source.Name, alternateNames, source.Actor);
            entity.Species = Normalise(source.Species);
            entity.Gender = Normalise(source.Gender);
            entity.House = Normalise(source.House);
            entity.DateOfBirth = Normalise(source.DateOfBirth);
            entity.YearOfBirth = source.YearOfBirth;
            entity.Wizard = source.Wizard;
            entity.Ancestry = Normalise(source.Ancestry);
            entity.EyeColour = Normalise(source.EyeColour);
            entity.HairColour = Normalise(source.HairColour);
            entity.WandWood = Normalise(source.Wand?.Wood);
            entity.WandCore = Normalise(source.Wand?.Core);
            entity.WandLength = source.Wand?.Length;
            entity.Patronus = Normalise(source.Patronus);
            entity.HogwartsStudent = source.HogwartsStudent;
            entity.HogwartsStaff = source.HogwartsStaff;
            entity.Actor = Normalise(source.Actor);
            entity.AlternateActors = JsonSerializer.Serialize(source.AlternateActors ?? []);
            entity.Alive = source.Alive;
            entity.ImageUrl = Normalise(source.Image);
            entity.LastSyncedUtc = now;

            touched++;
        }

        await db.SaveChangesAsync(ct);
        return touched;
    }

    private static async Task<int> UpsertSpellsAsync(
        HogwartsContext db, IReadOnlyList<HpApiSpell> incoming, CancellationToken ct)
    {
        var existing = await db.Spells.ToDictionaryAsync(s => s.Id, ct);
        var now = DateTime.UtcNow;
        var touched = 0;

        foreach (var source in incoming)
        {
            if (string.IsNullOrWhiteSpace(source.Id) || string.IsNullOrWhiteSpace(source.Name))
            {
                continue;
            }

            if (!existing.TryGetValue(source.Id, out var entity))
            {
                entity = new SpellEntity { Id = source.Id };
                db.Spells.Add(entity);
                existing[source.Id] = entity;
            }

            entity.Name = source.Name;
            entity.Description = Normalise(source.Description);
            entity.SearchIndex = $"{source.Name} {source.Description}".ToLowerInvariant();
            entity.Category = SpellClassifier.Classify(source.Name, source.Description);
            entity.LastSyncedUtc = now;

            touched++;
        }

        await db.SaveChangesAsync(ct);
        return touched;
    }

    private static string BuildSearchIndex(string name, IEnumerable<string> alternateNames, string? actor)
        => string.Join(' ', new[] { name, actor ?? string.Empty }.Concat(alternateNames))
            .ToLowerInvariant()
            .Trim();

    /// <summary>Upstream uses empty strings and null interchangeably for "unknown". Collapse both to null.</summary>
    private static string? Normalise(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
