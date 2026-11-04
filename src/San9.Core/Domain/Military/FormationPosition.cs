using System.Text.Json.Serialization;

namespace San9.Core.Domain;

/// <summary>A member's complete formation position, independent of source grid codes.</summary>
public readonly record struct FormationPosition(
    [property: JsonRequired] int Column,
    [property: JsonRequired] BattleRank Rank)
{
    [JsonIgnore] public int Row => (int)Rank;
    [JsonIgnore] public bool IsValid => Column >= 0 && Enum.IsDefined(Rank);
}
