namespace Anomaly.Protocol;

// Version 13 (MessagesV13.cs, protocol.md section 22): tg's sounds for this session. The replica keeps no sound state (what
// plays is the audio layer's); it keeps the session's path table (protocol state: a play names its file by index), checks
// each message against the table and the map, and raises SoundsReceived with the events, their files resolved. A MapGrid
// restarts the table (the server restarts it with every join).

public sealed partial class ReplicaWorld
{
    private readonly List<string> _soundPaths = new();
    private Action<IReadOnlyList<SoundEvent>> _soundsReceivedHandlers;
    private readonly List<SoundEvent> _unheardSounds = new();

    /// <summary>Events kept while nothing listens (the newest; a join's sounds reach the game scene before its sound player).</summary>
    public const int MaxUnheardSounds = 256;

    /// <summary>Entries of the session's sound path table.</summary>
    public int SoundPathCount => _soundPaths.Count;

    /// <summary>The path of a table index (1 based), or null outside the table.</summary>
    public string SoundPath(uint index) => index >= 1 && index <= _soundPaths.Count ? _soundPaths[(int)index - 1] : null;

    /// <summary>Raised after each applied <see cref="Sounds"/> message with its events in tg's order, a play's <see cref="SoundEvent.File"/> resolved.</summary>
    public event Action<IReadOnlyList<SoundEvent>> SoundsReceived
    {
        add
        {
            _soundsReceivedHandlers += value;
            // the events applied while nothing listened (the join tick's, applied by the connect screen's poll before the game
            // scene's sound player exists: the area's buzz, sent once at Login, was lost)
            if (_unheardSounds.Count == 0 || value == null) return;
            SoundEvent[] kept = _unheardSounds.ToArray();
            _unheardSounds.Clear();
            value(kept);
        }
        remove => _soundsReceivedHandlers -= value;
    }

    /// <summary>
    /// Applies a <see cref="Sounds"/> message: throws <see cref="ProtocolException"/> (and changes nothing) for one before a MapGrid,
    /// new paths that do not continue the table, a play's index past the table, or a source tile outside the map.
    /// </summary>
    public void Apply(Sounds sounds)
    {
        ArgumentNullException.ThrowIfNull(sounds);
        if (_map == null)
            throw Bad("a Sounds message arrives before any MapGrid");
        if (sounds.NewPaths.Count > 0 && sounds.FirstPath != _soundPaths.Count + 1)
            throw Bad("a Sounds message adds paths from index " + sounds.FirstPath + " to a table of " + _soundPaths.Count);
        int table = _soundPaths.Count + sounds.NewPaths.Count;
        if (table > Limits.MaxSoundTableEntries)
            throw Bad("the sound path table grows past " + Limits.MaxSoundTableEntries);
        foreach (SoundEvent e in sounds.Events)
        {
            if (e.Kind == SoundKind.Play && e.Path > table)
                throw Bad("a sound play names path " + e.Path + " of a table of " + table);
            if (e.Positional && !InMap(e.X, e.Y, e.Z))
                throw Bad("a sound at " + e.X + "," + e.Y + "," + e.Z + " is outside the map");
        }

        _soundPaths.AddRange(sounds.NewPaths);
        if (sounds.Events.Count == 0) return;
        var events = new SoundEvent[sounds.Events.Count];
        for (int i = 0; i < events.Length; i++)
        {
            SoundEvent e = sounds.Events[i];
            events[i] = e.Kind == SoundKind.Play ? e with { File = _soundPaths[(int)e.Path - 1] } : e;
        }
        if (_soundsReceivedHandlers == null)
        {
            _unheardSounds.AddRange(events);
            if (_unheardSounds.Count > MaxUnheardSounds)
                _unheardSounds.RemoveRange(0, _unheardSounds.Count - MaxUnheardSounds);
            return;
        }
        _soundsReceivedHandlers.Invoke(events);
    }

    private void TakeMapCellsV13()
    {
        _soundPaths.Clear();
        _unheardSounds.Clear();
    }
}
