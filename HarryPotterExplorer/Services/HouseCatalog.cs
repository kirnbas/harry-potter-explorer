using HarryPotterExplorer.Models;

namespace HarryPotterExplorer.Services;

public interface IHouseCatalog
{
    IReadOnlyList<HouseInfo> All { get; }
    HouseInfo? Find(string? slugOrName);
}

/// <summary>
/// House lore is not exposed by the upstream API, so it lives here as curated,
/// version-controlled content. Everything else in the app is mirrored data.
/// </summary>
public sealed class HouseCatalog : IHouseCatalog
{
    public IReadOnlyList<HouseInfo> All { get; } =
    [
        new HouseInfo(
            Slug: "gryffindor",
            Name: "Gryffindor",
            Founder: "Godric Gryffindor",
            Element: "Fire",
            AnimalSymbol: "Lion",
            Ghost: "Nearly Headless Nick",
            CommonRoom: "Gryffindor Tower, seventh floor, behind the portrait of the Fat Lady",
            HeadOfHouse: "Minerva McGonagall",
            Traits: ["Courage", "Bravery", "Nerve", "Chivalry"],
            PrimaryColour: "#7f0909",
            SecondaryColour: "#d3a625",
            Crest: "🦁",
            Motto: "Their daring, nerve and chivalry set Gryffindors apart.",
            Description: "Founded by Godric Gryffindor, the house prizes the kind of courage that " +
                         "shows up when it is inconvenient. Gryffindors are the first through the " +
                         "door and, occasionally, the first into detention. The house sword — " +
                         "goblin-made and impervious to rust — appears in the Sorting Hat for anyone " +
                         "who needs it badly enough."),

        new HouseInfo(
            Slug: "slytherin",
            Name: "Slytherin",
            Founder: "Salazar Slytherin",
            Element: "Water",
            AnimalSymbol: "Serpent",
            Ghost: "The Bloody Baron",
            CommonRoom: "The dungeons, beneath the Black Lake",
            HeadOfHouse: "Severus Snape",
            Traits: ["Ambition", "Cunning", "Resourcefulness", "Leadership"],
            PrimaryColour: "#1a472a",
            SecondaryColour: "#aaaaaa",
            Crest: "🐍",
            Motto: "Those cunning folk use any means to achieve their ends.",
            Description: "Salazar Slytherin looked for ambition and a certain flexibility of method. " +
                         "The common room sits below the Black Lake, so the light is green and the " +
                         "windows occasionally show a passing merperson. Slytherin loyalty runs deep " +
                         "— it is simply given to fewer people."),

        new HouseInfo(
            Slug: "hufflepuff",
            Name: "Hufflepuff",
            Founder: "Helga Hufflepuff",
            Element: "Earth",
            AnimalSymbol: "Badger",
            Ghost: "The Fat Friar",
            CommonRoom: "A basement near the kitchens, entered by tapping the right barrel",
            HeadOfHouse: "Pomona Sprout",
            Traits: ["Hard work", "Patience", "Loyalty", "Fair play"],
            PrimaryColour: "#ecb939",
            SecondaryColour: "#372e29",
            Crest: "🦡",
            Motto: "Those patient Hufflepuffs are true and unafraid of toil.",
            Description: "Helga Hufflepuff took the students the other three founders overlooked, " +
                         "and taught them all the same. The result is the house with the fewest dark " +
                         "wizards and the best access to the kitchens. Tap the barrel two from the " +
                         "bottom, middle of the second row, to the rhythm of \"Helga Hufflepuff\" — " +
                         "get it wrong and you are doused in vinegar."),

        new HouseInfo(
            Slug: "ravenclaw",
            Name: "Ravenclaw",
            Founder: "Rowena Ravenclaw",
            Element: "Air",
            AnimalSymbol: "Eagle",
            Ghost: "The Grey Lady",
            CommonRoom: "Ravenclaw Tower, entered by answering the riddle of the bronze knocker",
            HeadOfHouse: "Filius Flitwick",
            Traits: ["Intelligence", "Wit", "Wisdom", "Creativity"],
            PrimaryColour: "#0e1a40",
            SecondaryColour: "#946b2d",
            Crest: "🦅",
            Motto: "Wit beyond measure is man's greatest treasure.",
            Description: "Ravenclaw has no password. The door asks a riddle, and any student who can " +
                         "answer it may enter — which means a first-year occasionally lets in a " +
                         "puzzled seventh-year. Rowena Ravenclaw's lost diadem promised wisdom to its " +
                         "wearer; it took a rather long detour before anyone found it again.")
    ];

    public HouseInfo? Find(string? slugOrName)
    {
        if (string.IsNullOrWhiteSpace(slugOrName))
        {
            return null;
        }

        return All.FirstOrDefault(h =>
            h.Slug.Equals(slugOrName, StringComparison.OrdinalIgnoreCase) ||
            h.Name.Equals(slugOrName, StringComparison.OrdinalIgnoreCase));
    }
}
