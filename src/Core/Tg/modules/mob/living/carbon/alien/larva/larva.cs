// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/alien/larva/larva.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps X5 (D333): the whole file, the larva. The var block is generated (D111).

// tg: code/modules/mob/living/carbon/alien/larva/larva.dm:1
public partial class mob_living_carbon_alien_larva : mob_living_carbon_alien
{
    // tg: code/modules/mob/living/carbon/alien/larva/larva.dm:43  var/static/list/innate_actions (a proc-level static, rules 10.6)
    private static ref DmList Initialize__innate_actions => ref TgState.Current.mob_living_carbon_alien_larva__Initialize__innate_actions;

    //This is fine right now, if we're adding organ specific damage this needs to be updated
    // tg: code/modules/mob/living/carbon/alien/larva/larva.dm:42
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        grant_actions_by_list(Initialize__innate_actions);
        return base.Initialize(mapload, __extra);
    }

    // tg: code/modules/mob/living/carbon/alien/larva/larva.dm:50
    public override async DmTask<object> Login()
    {
        object dot = await base.Login();
        if (!Dm.truthy(dot) || client == null)
            return FALSE;
        to_chat(this, I18n.T("<b>You are an alien larva. Hide from danger until you can evolve.<br>Use say :a to communicate with the hivemind.</b>"));
        return dot;
    }

    // tg: code/modules/mob/living/carbon/alien/larva/larva.dm:56
    public override async DmTask<object> adjustPlasma(object amount = null)
    {
        if (stat != DEAD && Dm.num(amount) > 0)
            amount_grown = min(amount_grown + 1, XENOMORPH_MAX_GROWTH);
        await base.adjustPlasma(amount);
        return null;
    }

    //can't equip anything
    // tg: code/modules/mob/living/carbon/alien/larva/larva.dm:62
    [DmParams("slot_id", "params")]
    public override async DmTask<object> attack_ui(object slot = null, object @params = null)
    {
        return null;
    }

    // new damage icon system
    // now constructs damage icon for each organ from mask * damage field

    // tg: code/modules/mob/living/carbon/alien/larva/larva.dm:69
    public override void toggle_throw_mode()
    {
        return;
    }

    // tg: code/modules/mob/living/carbon/alien/larva/larva.dm:72
    [DmParams("AM", "state", "force", "supress_message")]
    public override async DmTask<object> start_pulling(atom_movable pulled_atom = null, object state = null, object force = null, float? supress_message = null)
    {
        force ??= move_force;
        supress_message ??= FALSE;
        return null;
    }

    // tg: code/modules/mob/living/carbon/alien/larva/larva.dm:75
    public override object canBeHandcuffed()
    {
        return TRUE;
    }

    /// Don't scramble a larva's body parts, it doesn't have any
    // tg: code/modules/mob/living/carbon/alien/larva/larva.dm:79
    public override async DmTask<object> bioscramble(object scramble_source = null)
    {
        return TRUE;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/carbon/alien/larva/larva.dm:43
    internal DmList mob_living_carbon_alien_larva__Initialize__innate_actions = Dm.list(
        typeof(datum_action_cooldown_alien_hide),
        typeof(datum_action_cooldown_alien_larva_evolve));
}
