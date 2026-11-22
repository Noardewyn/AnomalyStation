// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/dna_infuser/infuser_actions.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P3b, D327): the inky tongue's Spit Ink, aimed with a right click. The var block is generated (D111).

///Action from the inky tongue, from fish with the ink production trait.
// tg: code/game/machinery/dna_infuser/infuser_actions.dm:2
public partial class datum_action_cooldown_ink_spit : datum_action_cooldown
{
    // tg: code/game/machinery/dna_infuser/infuser_actions.dm:14
    public override async DmTask<object> IsAvailable(float? feedback = null)
    {
        mob_living_carbon as_carbon = owner as mob_living_carbon;
        if (as_carbon != null && Dm.truthy(as_carbon.is_mouth_covered(ITEM_SLOT_MASK)))
            return FALSE;
        if (!isturf(owner.loc))
            return FALSE;
        return await base.IsAvailable(feedback);
    }

    // tg: code/game/machinery/dna_infuser/infuser_actions.dm:22
    public override async DmTask<object> set_click_ability(mob on_who = null)
    {
        object dot = await base.set_click_ability(on_who);
        if (!Dm.truthy(dot))
            return dot;

        to_chat(on_who, span_notice(I18n.T("You prepare your ink glands. <B>Right-click to fire at a target!</B>")));
        await build_all_button_icons();
        return dot;
    }

    // tg: code/game/machinery/dna_infuser/infuser_actions.dm:30
    public override async DmTask<object> unset_click_ability(mob on_who = null, float? refund_cooldown = null)
    {
        refund_cooldown ??= TRUE;
        object dot = await base.unset_click_ability(on_who, refund_cooldown);
        if (!Dm.truthy(dot))
            return dot;

        await build_all_button_icons();
        return dot;
    }

    // We do this in InterceptClickOn() instead of Activate()
    // because we use the click parameters for aiming the projectile
    // (or something like that)
    // tg: code/game/machinery/dna_infuser/infuser_actions.dm:40
    public override async DmTask<object> InterceptClickOn(mob_living clicker = null, object @params = null, atom target = null)
    {
        if (!Dm.truthy(LAZYACCESS(params2list(@params), RIGHT_CLICK)))
            return null;
        object dot = await base.InterceptClickOn(clicker, @params, target);
        if (!Dm.truthy(dot))
            return dot;

        DmList modifiers = params2list(@params) as DmList;
        clicker.visible_message(
            span_danger(I18n.T("[] spits ink!", clicker)),
            span_bold(I18n.T("You spit ink.")));
        obj_projectile_ink_spit ink = Dm.@new<obj_projectile_ink_spit>(clicker.loc);
        await ink.aim_projectile(target, clicker, modifiers);
        ink.firer = clicker;
        await ink.fire();
        playsound(clicker, "sound/items/weapons/pierce.ogg", 20, TRUE, -1);
        await clicker.newtonian_move(get_angle(target, clicker));
        StartCooldown();
        return TRUE;
    }

    // Has to return TRUE, otherwise is skipped.
    // tg: code/game/machinery/dna_infuser/infuser_actions.dm:62
    public override async DmTask<object> Activate(atom target = null)
    {
        await DmTask.CompletedTask;
        return TRUE;
    }
}
