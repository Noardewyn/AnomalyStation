// Translated from tgstation (snapshot 1d99bd7f): code/datums/keybinding/_keybindings.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/datums/keybinding/_keybindings.dm:1
[DmPath("/datum/keybinding")]
public partial class datum_keybinding : datum
{
    // tg: code/datums/keybinding/_keybindings.dm:2
    public DmList hotkey_keys;
    // tg: code/datums/keybinding/_keybindings.dm:3
    public DmList classic_keys;
    // tg: code/datums/keybinding/_keybindings.dm:4
    public object name;
    // tg: code/datums/keybinding/_keybindings.dm:5
    public object full_name;
    // tg: code/datums/keybinding/_keybindings.dm:6
    public string description = "";
    // tg: code/datums/keybinding/_keybindings.dm:7
    // (object, not string: the V3 grep matches typepath literals assigned to other vars named category,
    // modules/uplink/uplink_items.dm:237, and a typepath var cannot hold the text default, rules 3.2 and 3.5)
    // GAP: CATEGORY_MISC (code/datums/keybinding/_defines.dm:9, #define CATEGORY_MISC "MISC") is a global #define
    // outside code/__DEFINES, which the defines converter does not emit; its value is written here until it is.
    public object category = "MISC";
    // tg: code/datums/keybinding/_keybindings.dm:8
    // GAP: WEIGHT_LOWEST (code/datums/keybinding/_defines.dm:22, #define WEIGHT_LOWEST 999): not emitted, as
    // CATEGORY_MISC above.
    public float weight = 999;
    // tg: code/datums/keybinding/_keybindings.dm:9
    public object keybind_signal;
    ///Boolean on whether players are able to edit this keybinding. Used for BYOND built-in binds we wish to
    ///tell the player of its existence, but don't want it being edited because BYOND doesn't let us.
    // tg: code/datums/keybinding/_keybindings.dm:12
    public float can_edit = TRUE;
}

// tg: code/datums/keybinding/_keybindings.dm:14
public partial class datum_keybinding : datum
{
    // tg: code/datums/keybinding/_keybindings.dm:14
    public override async DmTask New(params object[] __extra)
    {
        if (!Dm.truthy(keybind_signal))
            throw Dm.CRASH(Dm.text("Keybind [] called unredefined down() without a keybind_signal.", this), "code/datums/keybinding/_keybindings.dm", 16);

        // Default keys to the master "hotkey_keys"
        if (Dm.truthy(LAZYLEN(hotkey_keys)) && !Dm.truthy(LAZYLEN(classic_keys)))
            classic_keys = hotkey_keys.Copy();
    }

    // tg: code/datums/keybinding/_keybindings.dm:22
    [ShouldCallParent]
    public virtual async DmTask<object> down(client user = null, turf target = null, object mousepos_x = null, object mousepos_y = null)
    {
        return Dm.bitand((!Dm.truthy(user.mob._listen_lookup?[keybind_signal]) ? NONE : user.mob._SendSignal(keybind_signal, Dm.list(user.mob, target))), COMSIG_KB_ACTIVATED);
    }

    // tg: code/datums/keybinding/_keybindings.dm:26
    [ShouldCallParent]
    public virtual async DmTask<object> up(client user = null, turf target = null)
    {
        if (Dm.truthy(user.mob._listen_lookup?[DEACTIVATE_KEYBIND(keybind_signal)]))
            user.mob._SendSignal(DEACTIVATE_KEYBIND(keybind_signal), Dm.list(user.mob, target));
        return FALSE;
    }

    // tg: code/datums/keybinding/_keybindings.dm:31
    public virtual object can_use(client user = null)
    {
        return TRUE;
    }
}
