using System.Text.Json.Serialization;

namespace San9.Core.Domain;

public enum FormationGrantCondition { Always, Barbarian }

public sealed record FormationGrant(
    [property: JsonRequired] int FormationId,
    [property: JsonRequired] FormationGrantCondition Condition)
{
    public bool AppliesTo(ForceState force) => Condition switch
    {
        FormationGrantCondition.Always => true,
        FormationGrantCondition.Barbarian => force.IsBarbarian,
        _ => false
    };
}
