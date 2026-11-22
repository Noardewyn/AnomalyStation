namespace Anomaly.Protocol;

// Version 39 (M9 slice B, docs/ai/specs/social.md, docs/ai/specs/protocol.md section 48): emotes.
// - ActionKind.Emote (client to server, Control): the emote wheel's pick (D256), tg's emote keybinding
//   (/datum/keybinding/emote/down: user.mob.emote(emote_key, intentional = TRUE)), with the emote key as the action's text.
// - KeybindingCode.VerbEmotePanel: the hidden verb /client/verb/emote_panel (tg's emote panel, the wheel's "More..."), sent as a
//   keybinding action as the hidden verbs Sleep and Succumb are (version 37).

/// <summary>Version 39: the keys an <see cref="ActionKind.Emote"/> may carry.</summary>
public static class EmoteKeys
{
    /// <summary>A tg emote key: 1 to <see cref="Limits.MaxEmoteKeyBytes"/> characters, lowercase ASCII letters, digits and underscores
    /// (tg's keys are such words: "wave", "blink_r"); the server's <c>/mob/proc/emote</c> answers an unknown one.</summary>
    public static bool IsValid(string key)
    {
        if (string.IsNullOrEmpty(key) || key.Length > Limits.MaxEmoteKeyBytes)
            return false;
        foreach (char c in key)
        {
            if (c is not ((>= 'a' and <= 'z') or (>= '0' and <= '9') or '_'))
                return false;
        }
        return true;
    }
}
