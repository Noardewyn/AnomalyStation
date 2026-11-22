namespace Anomaly.Protocol;

// Version 16 (MessagesV16.cs, protocol.md section 25, DV44): the tgui windows of this session. The replica keeps each open window's
// state as tgui's frontend store keeps it: the config of the last update, and the data and static data merged update over update
// (newer fields over older, tgui's update handler); a close forgets the window. A MapGrid (the start of every join) forgets them; the
// server sends the open ones again, whole, after the join.

/// <summary>One open tgui window as the client knows it.</summary>
public sealed class UiWindowState
{
    public UiWindowState(int window, string @interface)
    {
        Window = window;
        Interface = @interface;
    }

    /// <summary>The window's pool index (tg's <c>"tgui-window-N"</c>).</summary>
    public int Window { get; }

    /// <summary>The interface it shows.</summary>
    public string Interface { get; }

    public string Title { get; internal set; } = "";

    /// <summary>tg's ui status (-1 close, 0 disabled, 1 update, 2 interactive).</summary>
    public int Status { get; internal set; }

    public bool Refreshing { get; internal set; }

    /// <summary>The merged ui_data (an object; empty before any).</summary>
    public UiValue Data { get; internal set; } = UiValue.ObjectOf();

    /// <summary>The merged ui_static_data (an object; empty before any).</summary>
    public UiValue StaticData { get; internal set; } = UiValue.ObjectOf();

    /// <summary>What tgui's views read (<c>useBackend().data</c>): the static data with the data over it.</summary>
    public UiValue Merged => StaticData.MergedWith(Data);

    /// <summary>Counts the updates applied (a view redraws when it changes).</summary>
    public int Serial { get; internal set; }
}

public sealed partial class ReplicaWorld
{
    private readonly SortedDictionary<int, UiWindowState> _uiWindows = new();
    private Action<int, UiWindowState> _uiWindowChangedHandlers;

    /// <summary>The open tgui windows by pool index.</summary>
    public IReadOnlyDictionary<int, UiWindowState> UiWindows => _uiWindows;

    /// <summary>Raised after each applied <see cref="UiWindowMessage"/>: the window index, and its state (null once closed).</summary>
    public event Action<int, UiWindowState> UiWindowChanged
    {
        add => _uiWindowChangedHandlers += value;
        remove => _uiWindowChangedHandlers -= value;
    }

    /// <summary>
    /// Applies a <see cref="UiWindowMessage"/>: throws <see cref="ProtocolException"/> (and changes nothing) for one before a Snapshot, a
    /// close of a window that is not open, or data or static data that is not an object.
    /// </summary>
    public void Apply(UiWindowMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (!_hasSnapshot)
            throw Bad("a UiWindow message arrives before any Snapshot");
        if (message.Kind == UiWindowKind.Close)
        {
            if (!_uiWindows.Remove(message.Window))
                throw Bad("a UI window " + message.Window + " closes but is not open");
            _uiWindowChangedHandlers?.Invoke(message.Window, null);
            return;
        }
        if (message.Data != null && message.Data.Kind != UiValueKind.Object)
            throw Bad("a UI window's data is not an object");
        if (message.StaticData != null && message.StaticData.Kind != UiValueKind.Object)
            throw Bad("a UI window's static data is not an object");
        if (!_uiWindows.TryGetValue(message.Window, out UiWindowState state) || state.Interface != message.Interface)
        {
            state = new UiWindowState(message.Window, message.Interface);
            _uiWindows[message.Window] = state;
        }
        state.Title = message.Title ?? "";
        state.Status = message.Status;
        state.Refreshing = message.Refreshing;
        if (message.StaticData != null)
            state.StaticData = state.StaticData.MergedWith(message.StaticData);
        if (message.Data != null)
            state.Data = state.Data.MergedWith(message.Data);
        state.Serial++;
        _uiWindowChangedHandlers?.Invoke(message.Window, state);
    }

    private void TakeMapCellsV16() => _uiWindows.Clear();
}
