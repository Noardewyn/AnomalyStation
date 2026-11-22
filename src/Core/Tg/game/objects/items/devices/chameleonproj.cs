// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/devices/chameleonproj.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign G1d (D316): the chameleon projector (scans an object's look and hides its holder inside a dummy that wears
// it; the dummy creeps a tile at a time, slower in the cold, and anything that touches it breaks the disguise), whole. The var
// blocks are generated (D111).

public partial class obj_item_chameleon : obj_item
{
    /// DEVIATION(DV376): the type of the thing whose look was scanned, which the dummy is drawn as (IShownAsType)
    public Type saved_type;

    // tg: code/game/objects/items/devices/chameleonproj.dm:20
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        saved_appearance = Dm.initial(typeof(obj_item_cigbutt), (obj_item_cigbutt butt) => butt.appearance);
        saved_type = typeof(obj_item_cigbutt);
        return dot;
    }

    // tg: code/game/objects/items/devices/chameleonproj.dm:25
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        await base.dropped(user, silent, __extra);
        await disrupt();
        return null;
    }

    // tg: code/game/objects/items/devices/chameleonproj.dm:29
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        await disrupt();
    }

    // tg: code/game/objects/items/devices/chameleonproj.dm:33
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (isturf(user.loc) || user.loc is obj_structure || active_dummy != null)
            await toggle(user);
        else
            to_chat(user, span_warning(I18n.T("You can't use [] while inside something!", this)));
        return null;
    }

    // tg: code/game/objects/items/devices/chameleonproj.dm:39
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!Dm.truthy(can_copy(interacting_with)) || (HAS_TRAIT(interacting_with, TRAIT_COMBAT_MODE_SKIP_INTERACTION) && Dm.truthy(user.combat_mode))) // SHOULD_SKIP_INTERACTION (__DEFINES/tools.dm:62), inline
            return NONE;
        make_copy(interacting_with, user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/devices/chameleonproj.dm:45
    public override async DmTask<object> interact_with_atom_secondary(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!Dm.truthy(can_copy(interacting_with))) // RMB scan works on storage items, LMB scan does not
            return NONE;
        make_copy(interacting_with, user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/devices/chameleonproj.dm:51
    public virtual object can_copy(atom target = null)
    {
        if (!Dm.truthy(check_sprite(target)))
            return FALSE;
        if (active_dummy != null) //I now present you the blackli(f)st
            return FALSE;
        if (isturf(target))
            return FALSE;
        if (ismob(target))
            return FALSE;
        if (target is obj_structure_falsewall)
            return FALSE;
        if (Dm.num(target.alpha) != 255)
            return FALSE;
        if (Dm.num(target.invisibility) != 0)
            return FALSE;
        if (iseffect(target) && target is not obj_effect_decal) //be a footprint
            return FALSE;
        return TRUE;
    }

    // tg: code/game/objects/items/devices/chameleonproj.dm:70
    public virtual void make_copy(atom target = null, mob user = null)
    {
        playsound(get_turf(this), "sound/items/weapons/flash.ogg", 100, TRUE, -6);
        to_chat(user, span_notice(I18n.T("Scanned [].", target)));
        var temp = Dm.@new<obj>();
        temp.appearance = target.appearance;
        temp.layer = Dm.num(Dm.initial(target, "layer")); // scanning things in your inventory
        SET_PLANE_EXPLICIT(temp, Dm.num(Dm.initial(this, "plane")), this);
        saved_appearance = temp.appearance;
        saved_type = target.GetType();
    }

    // tg: code/game/objects/items/devices/chameleonproj.dm:79
    public virtual object check_sprite(atom target = null)
    {
        // DEVIATION(DV375): tg asks rust-g whether the look's state is in its .dmi (icon_exists); the server reads no .dmi (D104) and a GAGS
        // item's icon is made at runtime, so a thing with an icon and an icon state is taken to have it (icon_exists' own null check kept)
        return target.icon != null && target.icon_state != null ? TRUE : FALSE;
    }

    // tg: code/game/objects/items/devices/chameleonproj.dm:82
    public virtual async DmTask toggle(mob user = null)
    {
        if (!Dm.truthy(can_use) || saved_appearance == null)
            return;
        if (active_dummy != null)
        {
            await eject_all();
            playsound(get_turf(this), "sound/effects/pop.ogg", 100, TRUE, -6);
            qdel(active_dummy);
            active_dummy = null;
            to_chat(user, span_notice(I18n.T("You deactivate \\the [].", this)));
            Dm.@new<obj_effect_temp_visual_emp_pulse>(get_turf(this));
        }
        else
        {
            playsound(get_turf(this), "sound/effects/pop.ogg", 100, TRUE, -6);
            var C = Dm.@new<obj_effect_dummy_chameleon>(user.drop_location());
            await C.activate(user, saved_appearance, this);
            to_chat(user, span_notice(I18n.T("You activate \\the [].", this)));
            Dm.@new<obj_effect_temp_visual_emp_pulse>(get_turf(this));
        }
        await user.cancel_camera();
    }

    // tg: code/game/objects/items/devices/chameleonproj.dm:100
    public virtual async DmTask disrupt(float delete_dummy = 1)
    {
        if (active_dummy == null)
            return;
        foreach (mob M in Dm.each<mob>(active_dummy.contents))
            to_chat(M, span_danger(I18n.T("Your chameleon projector deactivates.")));
        do_sparks(5, FALSE, this, this);
        await eject_all();
        if (Dm.truthy(delete_dummy))
            qdel(active_dummy);
        active_dummy = null;
        can_use = FALSE;
        _addtimer(Dm.@new<datum_callback>(this, nameof(reset_can_use)), 5 * SECONDS, file: "code/game/objects/items/devices/chameleonproj.dm", line: 110);
    }

    // tg: chameleonproj.dm:110  VARSET_CALLBACK(src, can_use, TRUE)
    public virtual void reset_can_use() => can_use = TRUE;

    // tg: code/game/objects/items/devices/chameleonproj.dm:112
    public virtual async DmTask eject_all()
    {
        foreach (atom_movable A in Dm.each<atom_movable>(active_dummy.contents.Copy()))
        {
            await A.forceMove(active_dummy.loc as atom);
            if (A is mob M)
                await M.reset_perspective(null);
        }
    }
}

public partial class obj_effect_dummy_chameleon : obj_effect_dummy, IShownAsType
{
    /// DEVIATION(DV376): the clients draw the dummy as the thing its projector scanned
    public Type shown_type;

    Type IShownAsType.ShownType => shown_type;

    // tg: code/game/objects/items/devices/chameleonproj.dm:126
    public virtual async DmTask activate(mob M = null, object saved_appearance = null, obj_item_chameleon C = null)
    {
        appearance = saved_appearance;
        if (M.buckled is obj_vehicle V)
            await V.unbuckle_mob(M as mob_living, force: TRUE);
        await M.forceMove(this);
        master = C;
        master.active_dummy = this;
        shown_type = C.saved_type;
    }

    // tg: code/game/objects/items/devices/chameleonproj.dm:135
    public override async DmTask<object> attackby(obj_item attacking_item = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        await master.disrupt();
        return null;
    }

    //ATTACK HAND IGNORING PARENT RETURN VALUE
    // tg: code/game/objects/items/devices/chameleonproj.dm:139
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        await master.disrupt();
        return null;
    }

    // tg: code/game/objects/items/devices/chameleonproj.dm:142
    public override async DmTask<object> attack_animal(mob user = null, DmList modifiers = null)
    {
        await master.disrupt();
        return null;
    }

    // tg: code/game/objects/items/devices/chameleonproj.dm:145
    public override async DmTask<object> attack_alien(mob_living_carbon user = null, DmList modifiers = null)
    {
        await master.disrupt();
        return null;
    }

    // tg: code/game/objects/items/devices/chameleonproj.dm:148
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        Waiting.NoSleep(master.disrupt());
        return TRUE;
    }

    // tg: code/game/objects/items/devices/chameleonproj.dm:152
    public override async DmTask<object> bullet_act(obj_projectile proj = null, object def_zone = null, float? piercing_hit = null, float? blocked = null)
    {
        object dot = await base.bullet_act(proj, def_zone, piercing_hit, blocked);
        await master.disrupt();
        return dot;
    }

    // tg: code/game/objects/items/devices/chameleonproj.dm:156
    public override async DmTask<object> relaymove(mob_living user = null, object direction = null)
    {
        if (!isturf(loc) || isspaceturf(loc) || !Dm.truthy(direction))
            return null; //No magical movement! Trust me, this bad boy can do things like leap out of pipes if you're not careful

        if (can_move < Dm.world.time)
        {
            float amount;
            float temperature = Dm.num(user.bodytemperature);
            if (temperature >= 300)
                amount = 10;
            else if (temperature >= 295)
                amount = 13;
            else if (temperature >= 280)
                amount = 16;
            else if (temperature >= 260)
                amount = 20;
            else
                amount = 25;

            can_move = (float)Dm.world.time + amount;
            await try_step_multiz(direction);
        }
        return null;
    }

    // tg: code/game/objects/items/devices/chameleonproj.dm:178
    public override object Destroy(float? force = null)
    {
        if (master != null)
        {
            Waiting.NoSleep(master.disrupt(0));
            master = null;
        }
        return base.Destroy(force);
    }
}
