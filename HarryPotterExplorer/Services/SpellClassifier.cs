namespace HarryPotterExplorer.Services;

/// <summary>
/// The upstream spell endpoint returns only a name and a one-line description, which
/// makes for a flat, unfilterable list. We enrich each row locally with a category so
/// the spellbook can be browsed by type as well as searched.
/// </summary>
public static class SpellClassifier
{
    private static readonly (string Keyword, string Category)[] Rules =
    [
        ("counter-curse", "Counter-Spell"),
        ("countercurse", "Counter-Spell"),
        ("counter-charm", "Counter-Spell"),
        ("curse", "Curse"),
        ("unforgivable", "Curse"),
        ("jinx", "Jinx"),
        ("hex", "Hex"),
        ("charm", "Charm"),
        ("transform", "Transfiguration"),
        ("turns", "Transfiguration"),
        ("conjur", "Conjuration"),
        ("summon", "Conjuration"),
        ("heal", "Healing"),
        ("reveal", "Revealing"),
        ("detect", "Revealing")
    ];

    private static readonly HashSet<string> UnforgivableCurses =
        new(StringComparer.OrdinalIgnoreCase) { "Avada Kedavra", "Crucio", "Imperio" };

    public static string Classify(string? name, string? description)
    {
        if (name is not null && UnforgivableCurses.Contains(name))
        {
            return "Unforgivable Curse";
        }

        var haystack = $"{name} {description}".ToLowerInvariant();

        foreach (var (keyword, category) in Rules)
        {
            if (haystack.Contains(keyword, StringComparison.Ordinal))
            {
                return category;
            }
        }

        return "Spell";
    }
}
