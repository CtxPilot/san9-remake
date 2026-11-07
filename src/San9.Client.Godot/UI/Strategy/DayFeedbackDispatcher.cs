using San9.Application.Presentation;

namespace San9.Client.Godot;

/// <summary>Consumes each day's dialogue, speech and record cues once, independently of view sampling.</summary>
internal sealed class DayFeedbackDispatcher(
    Action<IReadOnlyList<CommandMessage>, StrategySnapshot, Action?> Narrate,
    Func<MapMessageRecord, bool> Record, Func<MapSpeechMessage, bool> Speech, Action<string> Sound)
{
    private TurnDayPresentation? _battleMessagesDay;
    private readonly HashSet<string> _battleMessagesPlayed = [];
    public void Publish(TurnDayPresentation day)
    {
        if (!ReferenceEquals(_battleMessagesDay, day))
        {
            _battleMessagesDay = day; _battleMessagesPlayed.Clear();
            _dialogueSnapshot = null; _speechSnapshot = null; _recordSnapshot = null;
        }
        if (day.Battle is not { } plan) return;
        var pending = day.PendingDialogues;
        var dialogues = ReferenceEquals(_dialogueSnapshot, pending) ? [] : pending.Where(c => !_battleMessagesPlayed.Contains(c.Id)).ToArray();
        if (dialogues.Length > 0)
        {
            Narrate(dialogues.Select(c => c.Message).ToArray(), day.Before,
                () => { foreach (var cue in dialogues) day.CompleteDialogue(cue.Id); });
            foreach (var cue in dialogues) _battleMessagesPlayed.Add(cue.Id);
        }
        _dialogueSnapshot = pending;
        var speeches = day.ReadyMapSpeeches;
        if (!ReferenceEquals(_speechSnapshot, speeches))
        {
            foreach (var cue in speeches)
            {
                if (!_battleMessagesPlayed.Add(cue.Id)) continue;
                var message = cue.Message;
                if (message.MapSpeech is { } speech) Speech(speech);
                if (message.SoundRole is { } sound) Sound(sound);
            }
        }
        _speechSnapshot = speeches;
        var records = day.ReadyRecords;
        if (!ReferenceEquals(_recordSnapshot, records))
        {
            foreach (var cue in records)
            {
                if (!_battleMessagesPlayed.Add(cue.Record.Id)) continue;
                Record(cue.Record);
                if (cue.SoundRole is { } sound) Sound(sound);
            }
        }
        _recordSnapshot = records;
    }

    private IReadOnlyList<BattleMessageCue>? _dialogueSnapshot;
    private IReadOnlyList<BattleMessageCue>? _speechSnapshot;
    private IReadOnlyList<BattleRecordCue>? _recordSnapshot;

    public void Reset()
    {
        _battleMessagesDay = null; _battleMessagesPlayed.Clear();
        _dialogueSnapshot = null; _speechSnapshot = null; _recordSnapshot = null;
    }
}
