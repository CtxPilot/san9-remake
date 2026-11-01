using San9.Core.Domain;

namespace San9.Client.Godot.Settings;

/// <summary>One local companion document; visual preferences never enter simulation settings.</summary>
public sealed record CompanionConfiguration(CompanionSettings Rules, bool FormationAppearanceEnabled)
{
    public static CompanionConfiguration Default { get; } = new(CompanionSettings.Default, false);
}
