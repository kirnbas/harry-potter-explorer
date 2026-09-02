using Microsoft.AspNetCore.SignalR;

namespace HarryPotterExplorer.Hubs;

/// <summary>
/// Push-only hub behind the /live page. Clients subscribe and receive three kinds of
/// message: "ledger" (a new row was written), "stats" (aggregates changed) and
/// "sync" (the mirror-refresh state machine moved).
/// </summary>
public sealed class GreatHallHub : Hub
{
    public const string LedgerEvent = "ledger";
    public const string StatsEvent = "stats";
    public const string SyncEvent = "sync";
}
