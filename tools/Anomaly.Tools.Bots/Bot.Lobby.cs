using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Tools.Bots;

// M10 slice J: the bot in tg's lobby. The lobby is a tgui window (Limits.LobbyUiWindow, tgui-lobby's LobbyMenu): in the pregame the bot
// readies up and waits for the ticker's round start; after it, Join opens tg's late join menu (JobSelection) and the bot picks the
// station engineer, or with open jobs (D278) the first job the menu leaves open once the engineers are full. A bot does not walk
// while its lobby is open (its mob is tg's new player).

public sealed partial class Bot
{
    private const string LateJoinJob = "Station Engineer";
    private const int LobbyActPeriod = UpdatesPerSecond; // a press a second at most: the server's answer comes in the next updates

    private long _lobbyActedAt = -LobbyActPeriod;

    /// <summary>Acts in the lobby or the late join menu when one is open; false when neither is (the bot plays).</summary>
    private bool ActInLobby()
    {
        if (!Replica.UiWindows.TryGetValue(Limits.LobbyUiWindow, out UiWindowState lobby) || lobby.Interface != Limits.LobbyUiInterface)
            return false;
        if (UpdateCount - _lobbyActedAt < LobbyActPeriod) return true;

        UiValue data = lobby.Merged;
        UiWindowState menu = Replica.UiWindows.Values.FirstOrDefault(w => w.Interface == "JobSelection");
        if (menu != null)
            UiAct(menu.Window, "select_job", ("job", UiValue.Of(OpenJob(menu.Merged))));
        else if (data.Get("canJoin")?.Truthy == true)
            UiAct(lobby.Window, "join", ("ctrlClick", UiValue.Of(false)));
        else if (data.Get("canReady")?.Truthy == true && data.Get("isReady")?.Truthy != true)
            UiAct(lobby.Window, "ready_toggle");
        else
            return true; // ready, waiting for the round start
        _lobbyActedAt = UpdateCount;
        return true;
    }

    // JobSelection's data: departments, each with its jobs and their unavailable_reason (null while the job is open)
    private static string OpenJob(UiValue menu)
    {
        var jobs = (menu.Get("departments")?.Fields ?? []).SelectMany(d => d.Value.Get("jobs")?.Fields ?? []).ToList();
        bool Open(string title) => jobs.Any(j => j.Key == title && j.Value.Get("unavailable_reason") is not { Kind: not UiValueKind.Null });
        return Open(LateJoinJob) ? LateJoinJob : jobs.Select(j => j.Key).FirstOrDefault(Open) ?? LateJoinJob;
    }

    private void UiAct(int window, string action, params (string Key, UiValue Value)[] parameters)
    {
        Stats.LobbyActs++;
        SendAction(new ActionMessage(_frameSeq, ActionKind.UiAct, 0, ClickButton.None, ClickModifiers.None, null)
        {
            UiWindow = window,
            UiAction = action,
            UiParams = parameters.Length == 0 ? null : UiValue.ObjectOf(parameters),
        });
    }

    /// <summary>Sends a dev command (the runner's scheduled <c>startnow</c> or <c>shuttle call</c>); false when the bot has not joined.</summary>
    public bool SendDevCommand(string text)
    {
        if (State != BotState.Joined || _leave != Leave.None) return false;
        SendAction(new ActionMessage(_frameSeq, ActionKind.DevCommand, 0, ClickButton.None, ClickModifiers.None, text));
        return true;
    }
}
