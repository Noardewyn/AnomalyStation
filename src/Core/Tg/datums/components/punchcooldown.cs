// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/punchcooldown.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

///Your favourite Jojoke. Used for the gloves of the north star.
// tg: code/datums/components/punchcooldown.dm:2
public partial class datum_component_wearertargeting_punchcooldown : datum_component_wearertargeting
{
    // tg: code/datums/components/punchcooldown.dm:10
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object dot = await base.Initialize(__extra);
        if (Dm.eq(dot, COMPONENT_INCOMPATIBLE))
            return dot;
        RegisterSignal(parent, COMSIG_ITEM_ATTACK_SELF, nameof(changewarcry));
        return dot;
    }

    ///Called on COMSIG_LIVING_UNARMED_ATTACK. Yells the warcry and and reduces punch cooldown.
    // tg: code/datums/components/punchcooldown.dm:17
    public virtual async DmTask reducecooldown(mob_living_carbon M = null, atom target = null)
    {
        obj_item used_item = M.get_active_held_item() as obj_item;
        bool combat_hit = Dm.truthy(M.combat_mode) && isliving(target);
        if (!combat_hit && used_item != null)
            return; // a runtime in tg: `used_item & HAND_ITEM` on an item is a type mismatch
        if (combat_hit || Dm.truthy(Dm.bitand(used_item, HAND_ITEM)))
        {
            M.changeNext_move(CLICK_CD_RAPID);
            if (Dm.truthy(warcry))
                await M.say(warcry, ignore_spam: TRUE, forced: "north star warcry");
        }
    }

    ///Called on COMSIG_ITEM_ATTACK_SELF. Allows you to change the warcry.
    // tg: code/datums/components/punchcooldown.dm:25
    [SignalHandler]
    public virtual void changewarcry(datum source = null, mob user = null)
    {
        Waiting.Detach(do_changewarcry(user)); // INVOKE_ASYNC(src, PROC_REF(do_changewarcry), user)
    }

    // tg: code/datums/components/punchcooldown.dm:29
    public virtual async DmTask do_changewarcry(mob user = null)
    {
        object input = await tgui_input_text(user, I18n.T("What do you want your battlecry to be?"), I18n.T("Battle Cry"), max_length: 6);
        if (!QDELETED(this) && !QDELETED(user) && !Dm.truthy(await user.Adjacent(parent as atom)))
            return;
        if (Dm.truthy(input))
            warcry = input as string;
    }
}
