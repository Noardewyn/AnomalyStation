// Translated from tgstation (snapshot 1d99bd7f): code/modules/keybindings/setup.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M1 P1, the client layer). The skin's macros (winget and winset: the BYOND skin, not ported) are
// Dm.Deferred (docs/ai/specs/not-translated.md); the 3D client sends its key events itself.

// Set a client's focus to an object and override these procs on that object to let it handle keypresses

// tg: code/modules/keybindings/setup.dm:3
public partial class datum
{
    // Called when a key is pressed down initially
    // tg: code/modules/keybindings/setup.dm:3
    [ShouldCallParent]
    public virtual void key_down(object key = null, client user = null, object full_key = null)
    {
        return;
    }

    // Called when a key is released
    // tg: code/modules/keybindings/setup.dm:6
    public virtual void key_up(object key = null, client user = null)
    {
        return;
    }

    // Called once every frame
    // tg: code/modules/keybindings/setup.dm:8 (set waitfor = FALSE: this definition cannot sleep, so it is a plain method,
    // waiting-model.md 5.4)
    public virtual object keyLoop(client user = null)
    {
        return null;
    }
}

// tg: code/modules/keybindings/setup.dm:13 (interleaved types, rules 1.9)
public partial class client : datum
{
    // removes all the existing macros
    // tg: code/modules/keybindings/setup.dm:13
    public virtual async DmTask erase_all_macros()
    {
        // tg: :14-20  var/erase_output = ""; var/list/macro_set = params2list(winget(src, "default.*", "command")); for each
        // macro: erase_output = "[erase_output];[macro_name].parent=null"; winset(src, null, erase_output)
        Dm.Deferred("code/modules/keybindings/setup.dm:14", "erase_all_macros: winget and winset of the skin's macros (the BYOND skin, not ported; the 3D client sends its key events itself)");
    }

    // tg: code/modules/keybindings/setup.dm:22 (set waitfor = FALSE: the wrapper and its body, waiting-model.md 5.4)
    public virtual void set_macros()
    {
        Waiting.WaitforFalse(set_macros__body());
    }

    private async DmTask set_macros__body()
    {
        //Reset the buffer
        await reset_held_keys();

        await erase_all_macros();

        DmList macro_set = SSinput.macro_set;
        for (float k = 1, k__end = length(macro_set); k <= k__end; k++)
        {
            object key = macro_set[k];
            object command = macro_set[key];
            // tg: :34  winset(src, "default-[REF(key)]", "parent=default;name=[key];command=[command]")
            Dm.Deferred("code/modules/keybindings/setup.dm:34", "set_macros: winset of one skin macro (the BYOND skin, not ported; the 3D client sends its key events itself)");
        }

        update_special_keybinds();
    }

    /// Manually clears any held keys, in case due to lag or other undefined behavior a key gets stuck.
    // tg: code/modules/keybindings/setup.dm:39
    public virtual async DmTask reset_held_keys()
    {
        foreach (object key in Dm.each(keys_held))
            await keyUp(key);

        //In case one got stuck and the previous loop didn't clean it, somehow.
        foreach (object key in Dm.each(key_combos_held))
            await keyUp(key_combos_held[key]);
    }
}
