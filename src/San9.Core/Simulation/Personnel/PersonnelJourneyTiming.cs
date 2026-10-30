namespace San9.Core.Simulation;

/// <summary>Countdown consumption relative to a displayed date and its command stage.</summary>
public static class PersonnelJourneyTiming
{
    public static long? InitialDays(PersistentTask task) => task.Journey is not { } journey ? null
        : journey.ReturnLeg is { } leg ? leg.CountdownDays
        : task.Kind == TacticStudy.Kind ? task.Due.DayNumber - task.Started.DayNumber
        : task.Kind == PersonnelMissions.SearchKind || DiplomacyJourney.IsDiplomatic(task) ? journey.Route.XunRoundedDays
        : journey.Route.Days;

    public static int ElapsedDays(PersistentTask task, GameClock date, bool currentDateCommandsConsumed)
    {
        bool startedAfterCommands = task.Journey?.ReturnLeg?.AfterDailyCommands == true;
        int elapsed = Math.Max(0, task.ProgressDays - (startedAfterCommands ? 1 : 0));
        if (currentDateCommandsConsumed && !(startedAfterCommands && task.Started == date)) elapsed++;
        return elapsed;
    }
}
