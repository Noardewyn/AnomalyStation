// Translated from tgstation (snapshot 1d99bd7f): code/datums/keybinding/emote.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M9 slice B (D256): the whole file. tg makes one per emote key at init (init_emote_keybinds, a deferred leaf with the keybinding
// preferences); the port makes the one of a key when the emote wheel first sends it (ClientSeam.Emote, protocol version 39).

// tg: code/datums/keybinding/emote.dm:1
public partial class datum_keybinding_emote : datum_keybinding
{
    // tg: code/datums/keybinding/emote.dm:7
    public virtual void link_to_emote(Type faketype = null) // a typepath (rules 3.4)
    {
        hotkey_keys = Dm.list(UNBOUND_KEY);
        classic_keys = Dm.list(UNBOUND_KEY);
        emote_key = Dm.initial(faketype, (datum_emote p) => p.key);
        name = Dm.initial(faketype, (datum_emote p) => p.key);
        full_name = capitalize(Dm.initial(faketype, (datum_emote p) => p.key));
    }

    // tg: code/datums/keybinding/emote.dm:14
    public override async DmTask<object> down(client user = null, turf target = null, object mousepos_x = null, object mousepos_y = null)
    {
        object dot = await base.down(user, target, mousepos_x, mousepos_y);
        if (Dm.truthy(dot))
            return dot;
        return await user.mob.emote(emote_key, intentional: TRUE);
    }
}
