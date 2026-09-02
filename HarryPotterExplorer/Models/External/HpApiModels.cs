using System.Text.Json.Serialization;

namespace HarryPotterExplorer.Models.External;

/// <summary>
/// Raw shape returned by https://hp-api.onrender.com/api/characters.
/// Kept deliberately separate from our own DTOs: the upstream contract is not ours to
/// control, so it is parsed here and never leaks to the browser.
/// </summary>
public sealed class HpApiCharacter
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("alternate_names")] public List<string>? AlternateNames { get; set; }
    [JsonPropertyName("species")] public string? Species { get; set; }
    [JsonPropertyName("gender")] public string? Gender { get; set; }
    [JsonPropertyName("house")] public string? House { get; set; }
    [JsonPropertyName("dateOfBirth")] public string? DateOfBirth { get; set; }
    [JsonPropertyName("yearOfBirth")] public int? YearOfBirth { get; set; }
    [JsonPropertyName("wizard")] public bool Wizard { get; set; }
    [JsonPropertyName("ancestry")] public string? Ancestry { get; set; }
    [JsonPropertyName("eyeColour")] public string? EyeColour { get; set; }
    [JsonPropertyName("hairColour")] public string? HairColour { get; set; }
    [JsonPropertyName("wand")] public HpApiWand? Wand { get; set; }
    [JsonPropertyName("patronus")] public string? Patronus { get; set; }
    [JsonPropertyName("hogwartsStudent")] public bool HogwartsStudent { get; set; }
    [JsonPropertyName("hogwartsStaff")] public bool HogwartsStaff { get; set; }
    [JsonPropertyName("actor")] public string? Actor { get; set; }
    [JsonPropertyName("alternate_actors")] public List<string>? AlternateActors { get; set; }
    [JsonPropertyName("alive")] public bool Alive { get; set; }
    [JsonPropertyName("image")] public string? Image { get; set; }
}

public sealed class HpApiWand
{
    [JsonPropertyName("wood")] public string? Wood { get; set; }
    [JsonPropertyName("core")] public string? Core { get; set; }
    [JsonPropertyName("length")] public double? Length { get; set; }
}

public sealed class HpApiSpell
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
}
