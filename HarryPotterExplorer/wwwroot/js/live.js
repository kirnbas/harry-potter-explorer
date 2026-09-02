/*
 * The /live page. Opens a SignalR connection to /hubs/great-hall and applies three
 * kinds of server push: "ledger" (a row was appended), "stats" (the tally moved) and
 * "sync" (the background mirror refresh changed state).
 *
 * The page is fully rendered by the server first, so it is correct before this script
 * runs and stays readable if the socket never connects.
 */
(function () {
    "use strict";

    if (!window.signalR) { return; }

    var dot = document.querySelector("[data-connection-dot]");
    var label = document.querySelector("[data-connection-label]");
    var ledger = document.querySelector("[data-ledger]");
    var syncLabel = document.querySelector("[data-sync-label]");
    var syncDetail = document.querySelector("[data-sync-detail]");
    var refreshButton = document.querySelector("[data-refresh-mirror]");

    var MAX_ROWS = 60;

    var SYNC_TEXT = {
        ready: "Mirror up to date",
        syncing: "Copying records from the public API…",
        stale: "Upstream unreachable — showing stored records",
        failed: "Upstream unreachable and nothing stored yet",
        idle: "Waiting for the first refresh"
    };

    function setConnection(state, text) {
        if (dot) { dot.className = "live-dot" + (state === "on" ? "" : " off"); }
        if (label) { label.textContent = text; }
    }

    function setStat(name, value) {
        var node = document.querySelector('[data-stat="' + name + '"]');
        if (node && typeof value === "number") { node.textContent = String(value); }
    }

    function escapeHtml(value) {
        return String(value == null ? "" : value)
            .replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;").replace(/'/g, "&#39;");
    }

    function prependLedgerRow(entry) {
        if (!ledger) { return; }

        var empty = ledger.querySelector(".notice.empty");
        if (empty) { empty.remove(); }

        var released = entry.action === "released";
        var when = new Date(entry.createdUtc);
        var stamp = isNaN(when.getTime())
            ? ""
            : when.toISOString().substring(11, 19) + " UTC";

        var row = document.createElement("div");
        row.className = "ledger-row is-new" + (released ? " released" : "");
        row.innerHTML =
            '<span aria-hidden="true">' + (released ? "🕊️" : "🐸") + "</span>" +
            "<span><strong>" + escapeHtml(entry.characterName) + "</strong> " +
            '<span class="verb">' + escapeHtml(entry.action) + "</span></span>" +
            '<span class="when">' + escapeHtml(stamp) + "</span>";

        ledger.insertBefore(row, ledger.firstChild);

        while (ledger.children.length > MAX_ROWS) {
            ledger.removeChild(ledger.lastChild);
        }
    }

    function applySync(status) {
        if (!status) { return; }

        if (syncLabel) {
            syncLabel.textContent = SYNC_TEXT[status.state] || status.state;
        }

        if (syncDetail) {
            if (status.error) {
                syncDetail.textContent = status.error;
            } else if (status.lastCompletedUtc) {
                syncDetail.textContent =
                    "Last refresh " + new Date(status.lastCompletedUtc).toISOString().replace("T", " ").substring(0, 19) +
                    " UTC · " + status.charactersUpserted + " characters, " + status.spellsUpserted + " spells.";
            }
        }
    }

    var connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/great-hall")
        .withAutomaticReconnect([0, 2000, 6000, 12000, 30000])
        .build();

    connection.on("ledger", prependLedgerRow);

    connection.on("stats", function (payload) {
        if (!payload) { return; }
        setStat("collected", payload.collectedTotal);
    });

    connection.on("sync", function (status) {
        applySync(status);

        // A finished refresh can change the row counts, so pull the fresh aggregates once.
        if (status && (status.state === "ready" || status.state === "stale")) {
            window.fetch("/api/stats")
                .then(function (r) { return r.json(); })
                .then(function (stats) {
                    setStat("characters", stats.characters);
                    setStat("spells", stats.spells);
                    setStat("artifacts", stats.artifacts);
                    setStat("collected", stats.collectedTotal);
                })
                .catch(function () { /* leave the server-rendered numbers alone */ });
        }
    });

    connection.onreconnecting(function () { setConnection("off", "Reconnecting…"); });
    connection.onreconnected(function () { setConnection("on", "Live"); });
    connection.onclose(function () { setConnection("off", "Disconnected — reload to reconnect"); });

    connection.start()
        .then(function () { setConnection("on", "Live"); })
        .catch(function () { setConnection("off", "Live updates unavailable — the page is still accurate as loaded"); });

    if (refreshButton) {
        refreshButton.addEventListener("click", function () {
            refreshButton.disabled = true;
            refreshButton.textContent = "Sending an owl…";

            window.fetch("/api/sync", { method: "POST" })
                .then(function (r) { return r.json(); })
                .then(applySync)
                .catch(function () { /* the sync push will report the real state */ })
                .then(function () {
                    refreshButton.disabled = false;
                    refreshButton.textContent = "Refresh from the public API";
                });
        });
    }
})();
