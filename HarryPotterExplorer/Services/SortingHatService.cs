namespace HarryPotterExplorer.Services;

public sealed record SortingOption(string Id, string Text, IReadOnlyDictionary<string, int> Weights);

public sealed record SortingQuestion(string Id, string Prompt, IReadOnlyList<SortingOption> Options);

public sealed record SortingVerdict(
    string HouseSlug,
    string HouseName,
    string Crest,
    string PrimaryColour,
    string SecondaryColour,
    string Verdict,
    IReadOnlyDictionary<string, int> Scores,
    string? RunnerUp);

public interface ISortingHatService
{
    IReadOnlyList<SortingQuestion> Questions { get; }
    SortingVerdict Sort(IReadOnlyDictionary<string, string> answers);
}

/// <summary>
/// A small scoring engine that lives on the server rather than in the browser. Keeping it
/// here means the question weights are not visible in devtools, so the quiz cannot be
/// reverse-engineered into "always answer C for Slytherin" - and the same endpoint could
/// later persist results without changing the client at all.
/// </summary>
public sealed class SortingHatService(IHouseCatalog houses) : ISortingHatService
{
    private const string G = "gryffindor";
    private const string S = "slytherin";
    private const string H = "hufflepuff";
    private const string R = "ravenclaw";

    public IReadOnlyList<SortingQuestion> Questions { get; } =
    [
        new SortingQuestion("q1", "A corridor you have never seen appears at midnight. You...",
        [
            new SortingOption("a", "Walk straight in. Corridors do not appear for no reason.", new Dictionary<string, int> { [G] = 3, [R] = 1 }),
            new SortingOption("b", "Map it first, then walk in tomorrow with better light.", new Dictionary<string, int> { [R] = 3, [H] = 1 }),
            new SortingOption("c", "Find out who else knows about it before anyone else does.", new Dictionary<string, int> { [S] = 3, [R] = 1 }),
            new SortingOption("d", "Fetch a friend. Nobody explores a new corridor alone.", new Dictionary<string, int> { [H] = 3, [G] = 1 })
        ]),

        new SortingQuestion("q2", "Which would you least like to be remembered as?",
        [
            new SortingOption("a", "A coward.", new Dictionary<string, int> { [G] = 3 }),
            new SortingOption("b", "A fool.", new Dictionary<string, int> { [R] = 3 }),
            new SortingOption("c", "A traitor.", new Dictionary<string, int> { [H] = 3 }),
            new SortingOption("d", "A nobody.", new Dictionary<string, int> { [S] = 3 })
        ]),

        new SortingQuestion("q3", "The Room of Requirement gives you exactly what you need. It contains...",
        [
            new SortingOption("a", "A duelling floor and someone worth practising against.", new Dictionary<string, int> { [G] = 3, [S] = 1 }),
            new SortingOption("b", "A library with the restricted section unlocked.", new Dictionary<string, int> { [R] = 3 }),
            new SortingOption("c", "A long table, a warm fire, and everyone you like.", new Dictionary<string, int> { [H] = 3 }),
            new SortingOption("d", "A door to every other room in the castle.", new Dictionary<string, int> { [S] = 3, [R] = 1 })
        ]),

        new SortingQuestion("q4", "You are handed a secret that is not yours. You...",
        [
            new SortingOption("a", "Keep it. You gave your word.", new Dictionary<string, int> { [H] = 3, [G] = 1 }),
            new SortingOption("b", "Keep it, and remember precisely who owes you for it.", new Dictionary<string, int> { [S] = 3 }),
            new SortingOption("c", "Work out whether keeping it does more harm than telling.", new Dictionary<string, int> { [R] = 3 }),
            new SortingOption("d", "Tell the person it hurts most to keep it from.", new Dictionary<string, int> { [G] = 3 })
        ]),

        new SortingQuestion("q5", "Pick a Patronus you would be glad to see.",
        [
            new SortingOption("a", "A stag, bright enough to light a field.", new Dictionary<string, int> { [G] = 3 }),
            new SortingOption("b", "A raven that has clearly been listening.", new Dictionary<string, int> { [R] = 3 }),
            new SortingOption("c", "A badger that refuses to be moved.", new Dictionary<string, int> { [H] = 3 }),
            new SortingOption("d", "A serpent, silver and unhurried.", new Dictionary<string, int> { [S] = 3 })
        ]),

        new SortingQuestion("q6", "Four hourglasses. Which would you rather fill?",
        [
            new SortingOption("a", "Points won loudly, in front of everyone.", new Dictionary<string, int> { [G] = 2, [S] = 2 }),
            new SortingOption("b", "Points won quietly, over a whole term.", new Dictionary<string, int> { [H] = 3 }),
            new SortingOption("c", "Points won for an answer nobody else had.", new Dictionary<string, int> { [R] = 3 }),
            new SortingOption("d", "Points are a scoreboard. I want the trophy.", new Dictionary<string, int> { [S] = 3 })
        ]),

        new SortingQuestion("q7", "Finally: what would you like the Hat to say?",
        [
            new SortingOption("a", "\"Plenty of courage, I see.\"", new Dictionary<string, int> { [G] = 4 }),
            new SortingOption("b", "\"A ready mind - and it knows it.\"", new Dictionary<string, int> { [R] = 4 }),
            new SortingOption("c", "\"Loyal, and worth being loyal to.\"", new Dictionary<string, int> { [H] = 4 }),
            new SortingOption("d", "\"You could be great, you know.\"", new Dictionary<string, int> { [S] = 4 })
        ])
    ];

    public SortingVerdict Sort(IReadOnlyDictionary<string, string> answers)
    {
        var scores = new Dictionary<string, int> { [G] = 0, [S] = 0, [H] = 0, [R] = 0 };

        foreach (var question in Questions)
        {
            if (!answers.TryGetValue(question.Id, out var optionId))
            {
                continue;
            }

            var option = question.Options.FirstOrDefault(o => o.Id == optionId);

            if (option is null)
            {
                continue;
            }

            foreach (var (house, weight) in option.Weights)
            {
                scores[house] = scores.GetValueOrDefault(house) + weight;
            }
        }

        var ranked = scores.OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key)
            .ToList();

        var winnerSlug = ranked[0].Value == 0 ? H : ranked[0].Key;
        var winner = houses.Find(winnerSlug)!;
        var runnerUp = ranked.Count > 1 ? houses.Find(ranked[1].Key)?.Name : null;

        var decisive = ranked[0].Value - (ranked.Count > 1 ? ranked[1].Value : 0);

        var verdict = decisive switch
        {
            0 => $"The Hat hesitates for a long moment... but settles on {winner.Name}.",
            <= 2 => $"Difficult. Very difficult. But better be... {winner.Name.ToUpperInvariant()}!",
            <= 6 => $"No doubt about it at all - {winner.Name.ToUpperInvariant()}!",
            _ => $"The Hat barely touches your head. {winner.Name.ToUpperInvariant()}!"
        };

        return new SortingVerdict(
            winner.Slug, winner.Name, winner.Crest,
            winner.PrimaryColour, winner.SecondaryColour,
            verdict, scores, runnerUp);
    }
}
