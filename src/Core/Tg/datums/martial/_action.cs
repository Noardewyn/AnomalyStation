// Translated from tgstation (snapshot 1d99bd7f): code/datums/martial/_action.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X3a (D316): the martial arts' button (the move list, and the swap of styles with the right click), whole.

// tg: code/datums/martial/_action.dm:1
public partial class datum_action_swap_arts : datum_action
{
    // tg: code/datums/martial/_action.dm:10
    public override object Destroy(float? force = null)
    {
        current_used_art = null;
        return base.Destroy(force);
    }

    // tg: code/datums/martial/_action.dm:14
    [DmParams("Target", "starting_style")]
    public override async DmTask New(params object[] __extra)
    {
        current_used_art = Dm.arg<datum_martial_art>(__extra, 1);
        await base.New(__extra);
    }

    // tg: code/datums/martial/_action.dm:18
    public override void update_button_name(atom_movable_screen_movable_action_button button = null, float? force = null, params object[] __extra)
    {
        var new_art = Dm.arg<datum_martial_art>(__extra, 0);
        if (new_art != null)
            current_used_art = new_art;
        name = Dm.text("[]", current_used_art.help_verb);
        var living_owner = owner as mob_living;
        if (LAZYLEN(living_owner.martial_arts) >= 2)
            name += "/Swap Style";
        base.update_button_name(button, force, __extra);
    }

    // tg: code/datums/martial/_action.dm:26
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        await base.Trigger(clicker, trigger_flags, __extra);
        var living_owner = owner as mob_living;
        if (Dm.truthy(Dm.bitand(trigger_flags, TRIGGER_SECONDARY_ACTION)) && (LAZYLEN(living_owner.martial_arts) >= 2))
        {
            await ((mob_living)clicker).cycle_style();
            return TRUE;
        }
        object help_information = current_used_art.get_style_help();
        if (help_information == null)
        {
            to_chat(clicker, span_notice(I18n.T("[] doesn't have any combat information!", current_used_art.name)));
            return TRUE;
        }
        foreach (object info in Dm.each(help_information))
            to_chat(clicker, info);
        return TRUE;
    }
}
