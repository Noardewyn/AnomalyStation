// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/guns/ballistic/launchers.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N2a, D316: the operatives' break-action grenade launcher, the M-90gl's underbarrel launcher and the Dardo-RE
// rocket launcher with its backblast). The var blocks are generated (D111).

//KEEP IN MIND: These are different from gun/grenadelauncher. These are designed to shoot premade rocket and grenade projectiles, not flashbangs or chemistry casings etc.
//Put handheld rocket launchers here if someone ever decides to make something so hilarious ~Paprika

// tg: code/modules/projectiles/guns/ballistic/launchers.dm:4
public partial class obj_item_gun_ballistic_revolver_grenadelauncher : obj_item_gun_ballistic_revolver
{
    // tg: code/modules/projectiles/guns/ballistic/launchers.dm:24
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.item_interaction(user, tool, modifiers, __extra);
        // tg assigns in the test (`. = ITEM_INTERACT_SUCCESS`), so the parent's result is always replaced
        dot = ITEM_INTERACT_SUCCESS;
        if (istype(tool, typeof(obj_item_ammo_box)) || isammocasing(tool))
            chamber_round();
        return dot;
    }
}

// tg: code/modules/projectiles/guns/ballistic/launchers.dm:29
public partial class obj_item_gun_ballistic_revolver_grenadelauncher_underbarrel : obj_item_gun_ballistic_revolver_grenadelauncher
{
    // tg: code/modules/projectiles/guns/ballistic/launchers.dm:34
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        var gun = loc as obj_item_gun;
        if (gun == null)
            return INITIALIZE_HINT_QDEL;
        pin = gun.pin;
        RegisterSignal(gun, COMSIG_GUN_PIN_INSERTED, nameof(on_pin_inserted));
        RegisterSignal(gun, COMSIG_GUN_PIN_REMOVED, nameof(on_pin_removed));
        return dot;
    }

    // tg: code/modules/projectiles/guns/ballistic/launchers.dm:43
    [SignalHandler]
    public virtual void on_pin_inserted(obj_item_gun source = null, obj_item_firing_pin new_pin = null, mob_living user = null)
    {
        pin = new_pin;
    }

    // tg: code/modules/projectiles/guns/ballistic/launchers.dm:47
    [SignalHandler]
    public virtual void on_pin_removed(obj_item_gun source = null, obj_item_firing_pin old_pin = null, mob_living user = null)
    {
        pin = null;
    }
}

// tg: code/modules/projectiles/guns/ballistic/launchers.dm:51
public partial class obj_item_gun_ballistic_revolver_grenadelauncher_cyborg : obj_item_gun_ballistic_revolver_grenadelauncher
{
    // tg: code/modules/projectiles/guns/ballistic/launchers.dm:59
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        return null;
    }
}

// tg: code/modules/projectiles/guns/ballistic/launchers.dm:73
public partial class obj_item_gun_ballistic_rocketlauncher : obj_item_gun_ballistic
{
    // tg: code/modules/projectiles/guns/ballistic/launchers.dm:103
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(backblast))
            Waiting.NoSleep(AddElement(typeof(datum_element_backblast)));
        return dot;
    }

    // tg: code/modules/projectiles/guns/ballistic/launchers.dm:121
    public override async DmTask<object> try_fire_gun(atom target = null, mob_living user = null, object @params = null)
    {
        object dot = await base.try_fire_gun(target, user, @params);
        if (!Dm.truthy(dot))
            return dot;
        (magazine as obj_item_ammo_box)?.get_round(); //Hack to clear the mag after it's fired
        return dot;
    }

    // tg: code/modules/projectiles/guns/ballistic/launchers.dm:127
    public override async DmTask<object> attack_self_tk(mob user = null)
    {
        return null; //too difficult to remove the rocket with TK
    }

    // tg: code/modules/projectiles/guns/ballistic/launchers.dm:130
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (get_ammo() != 0)
            Dm.append(ref dot, "rocketlauncher_loaded");
        return dot;
    }

    // tg: code/modules/projectiles/guns/ballistic/launchers.dm:135
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_warning(I18n.T("[] aims [] at the ground! It looks like [] performing a sick rocket jump!", user, this, user.p_theyre())),
            span_userdanger(I18n.T("You aim [] at the ground to perform a bisnasty rocket jump...", this)));
        if (Dm.truthy(can_shoot()))
        {
            ADD_TRAIT(user, TRAIT_NO_TRANSFORM, REF(this));
            playsound(this, "sound/vehicles/rocketlaunch.ogg", 80, TRUE, 5);
            Dm.animate(user, Dm.named("pixel_z", 300), Dm.named("time", 30), Dm.named("flags", ANIMATION_RELATIVE), Dm.named("easing", LINEAR_EASING));
            await sleep(7 * SECONDS);
            Dm.animate(user, Dm.named("pixel_z", -300), Dm.named("time", 5), Dm.named("flags", ANIMATION_RELATIVE), Dm.named("easing", LINEAR_EASING));
            await sleep(0.5f * SECONDS);
            REMOVE_TRAIT(user, TRAIT_NO_TRANSFORM, REF(this));
            await process_fire(user, user, TRUE);
            if (!QDELETED(user)) //if they weren't gibbed by the explosion, take care of them for good.
                await user.gib(DROP_ALL_REMAINS);
            return MANUAL_SUICIDE;
        }
        await sleep(0.5f * SECONDS);
        await shoot_with_empty_chamber(user);
        await sleep(2 * SECONDS);
        user.visible_message(span_warning(I18n.T("[] looks about the room realizing [] still there. [] proceed to shove [] down their throat and choke []self with it!", user, user.p_theyre(), user.p_They(), this, user.p_them())),
            span_userdanger(I18n.T("You look around after realizing you're still here, then proceed to choke yourself to death with []!", this)));
        await sleep(2 * SECONDS);
        return OXYLOSS;
    }
}
