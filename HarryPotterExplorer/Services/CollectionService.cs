using HarryPotterExplorer.Data;
using HarryPotterExplorer.Hubs;
using HarryPotterExplorer.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace HarryPotterExplorer.Services;

public sealed record CollectionResult(
    string CharacterId,
    string CharacterName,
    bool Collected,
    int CollectCount,
    int CollectedTotal,
    bool Changed);

public interface ICollectionService
{
    Task<CollectionResult?> ToggleAsync(
        string characterId, string visitorId, bool collect, CancellationToken ct = default);
}

/// <summary>
/// The bridge between the private, per-browser collection (localStorage) and the shared,
/// public tally in the database. The browser owns *which* cards you have; the server only
/// ever learns that "somebody" collected a card, keyed by an anonymous visitor id.
/// Every write is appended to the ledger and pushed to the /live page over SignalR.
/// </summary>
public sealed class CollectionService(
    HogwartsContext db,
    IHubContext<GreatHallHub> hub,
    ILogger<CollectionService> logger) : ICollectionService
{
    public async Task<CollectionResult?> ToggleAsync(
        string characterId, string visitorId, bool collect, CancellationToken ct = default)
    {
        var character = await db.Characters.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == characterId, ct);

        if (character is null)
        {
            return null;
        }

        var action = collect ? "collected" : "released";

        var lastAction = await db.LedgerEvents.AsNoTracking()
            .Where(l => l.VisitorId == visitorId && l.CharacterId == characterId)
            .OrderByDescending(l => l.Id)
            .Select(l => l.Action)
            .FirstOrDefaultAsync(ct);

        var alreadyInThatState = lastAction == action || (lastAction is null && !collect);

        var stat = await db.CharacterStats.FirstOrDefaultAsync(s => s.CharacterId == characterId, ct);

        if (stat is null)
        {
            stat = new CharacterStatEntity { CharacterId = characterId };
            db.CharacterStats.Add(stat);
        }

        stat.CharacterName = character.Name;
        stat.House = character.House;
        stat.ImageUrl = character.ImageUrl;
        stat.UpdatedUtc = DateTime.UtcNow;

        LedgerEventEntity? ledgerEvent = null;

        if (!alreadyInThatState)
        {
            stat.CollectCount = Math.Max(0, stat.CollectCount + (collect ? 1 : -1));

            ledgerEvent = new LedgerEventEntity
            {
                CharacterId = characterId,
                CharacterName = character.Name,
                House = character.House,
                Action = action,
                VisitorId = visitorId,
                CreatedUtc = DateTime.UtcNow
            };

            db.LedgerEvents.Add(ledgerEvent);
        }

        await db.SaveChangesAsync(ct);

        var collectedTotal = await db.CharacterStats.SumAsync(s => (int?)s.CollectCount, ct) ?? 0;

        var result = new CollectionResult(
            characterId, character.Name, collect, stat.CollectCount, collectedTotal,
            Changed: ledgerEvent is not null);

        if (ledgerEvent is not null)
        {
            logger.LogInformation("{Action}: {Character} (now held by {Count})",
                action, character.Name, stat.CollectCount);

            await hub.Clients.All.SendAsync(
                GreatHallHub.LedgerEvent,
                new LedgerEntry(
                    ledgerEvent.Id, ledgerEvent.CharacterId, ledgerEvent.CharacterName,
                    ledgerEvent.House, ledgerEvent.Action, ledgerEvent.CreatedUtc),
                ct);

            await hub.Clients.All.SendAsync(GreatHallHub.StatsEvent, result, ct);
        }

        return result;
    }
}
