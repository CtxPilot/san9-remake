using San9.Core.Simulation;

namespace San9.Core.Domain;

/// <summary>Execution prohibitions by the executing force's current controller; rulers remain exempt.</summary>
public static class PrisonerExecutionRules
{
    public static bool IsProhibited(CompanionSettings settings, SimulationState simulation, int executingForceId, bool isRuler)
    {
        if (isRuler) return false;
        bool player = simulation.Controls.Any(c => c.ForceId == executingForceId && c.Kind == ControllerKind.Player);
        return player ? settings.DisablePlayerPrisonerExecution : settings.DisableComputerPrisonerExecution;
    }
}
