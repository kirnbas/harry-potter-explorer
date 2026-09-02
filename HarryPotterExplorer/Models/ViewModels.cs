using HarryPotterExplorer.Services;

namespace HarryPotterExplorer.Models;

public sealed record HomeViewModel(
    IReadOnlyList<HouseWithCount> Houses,
    LiveStats Stats,
    IReadOnlyList<CharacterSummary> Featured,
    IReadOnlyList<ArtifactSummary> FeaturedArtifacts);

public sealed record HousesIndexViewModel(IReadOnlyList<HouseWithCount> Houses);

public sealed record HouseDetailViewModel(
    HouseInfo House,
    int MemberCount,
    IReadOnlyList<CharacterSummary> Members);

public sealed record CharactersIndexViewModel(
    CharacterQuery Query,
    PagedResult<CharacterSummary> FirstPage,
    CatalogFacets Facets,
    SyncStatus Sync);

public sealed record CharacterDetailViewModel(
    CharacterDetail Character,
    HouseInfo? House,
    IReadOnlyList<CharacterSummary> HouseMates);

public sealed record SpellsIndexViewModel(
    PagedResult<SpellSummary> FirstPage,
    IReadOnlyList<string> Categories,
    string? Search,
    string? Category);

public sealed record ArtifactsIndexViewModel(
    IReadOnlyList<ArtifactSummary> Artifacts,
    IReadOnlyList<string> Categories,
    string? Search,
    string? Category);

public sealed record LiveViewModel(LiveStats Stats);

public sealed record SortingHatViewModel(IReadOnlyList<SortingQuestion> Questions);

public sealed record ErrorViewModel(int StatusCode, string Title, string Message, string? RequestId)
{
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}
