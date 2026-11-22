// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/devices/forcefieldprojector.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps DV2 (D333): the file whole. The forcefield projector: up to three glowing forcefields (tg's "forcefield" effect) projected
// a second after a click within seven tiles, each a dense, indestructible barrier whose hits drain the projector's shield
// integrity, which the fields also wear down slowly and which recovers while none stand; a field too far or with the integrity gone
// flickers out with the resonator's blast. The var blocks are generated (D111).

// tg: code/game/objects/items/devices/forcefieldprojector.dm:1
public partial class obj_item_forcefield_projector : obj_item
{
    // tg: code/game/objects/items/devices/forcefieldprojector.dm:24
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        return await ranged_interact_with_atom(interacting_with, user, modifiers);
    }

    // tg: code/game/objects/items/devices/forcefieldprojector.dm:27
    public override async DmTask<object> ranged_interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!Dm.truthy(check_allowed_items(interacting_with, not_inside: TRUE)))
            return NONE;
        if (interacting_with is obj_structure_projected_forcefield F__1)
        {
            if (F__1.generator == this)
            {
                to_chat(user, span_notice(I18n.T("You deactivate [].", F__1)));
                qdel(F__1);
                return ITEM_INTERACT_BLOCKING;
            }
        }
        turf T = get_turf(interacting_with);
        obj_structure_projected_forcefield found_field = Dm.locate_type(typeof(obj_structure_projected_forcefield), T) as obj_structure_projected_forcefield;
        if (Dm.truthy(found_field))
        {
            to_chat(user, span_warning(I18n.T("There is already a forcefield in that location!")));
            return ITEM_INTERACT_BLOCKING;
        }
        if (Dm.truthy(T.density))
            return ITEM_INTERACT_BLOCKING;
        if (get_dist(T, this) > field_distance_limit)
            return ITEM_INTERACT_BLOCKING;
        if (get_turf(this) == T)
        {
            to_chat(user, span_warning(I18n.T("Target is too close, aborting!")));
            return ITEM_INTERACT_BLOCKING;
        }
        if (LAZYLEN(current_fields) >= max_fields)
        {
            to_chat(user, span_warning(I18n.T("[] cannot sustain any more forcefields!", this)));
            return ITEM_INTERACT_BLOCKING;
        }
        if (Dm.truthy(force_proj_busy))
        {
            to_chat(user, span_notice(I18n.T("[] is busy creating a forcefield.", this)));
            return ITEM_INTERACT_BLOCKING;
        }
        playsound(loc, Dm.resource("sound/machines/click.ogg"), 20, TRUE);
        if (Dm.truthy(creation_time))
        {
            force_proj_busy = TRUE;
            if (!Dm.truthy(await do_after(user, creation_time, target: interacting_with)))
            {
                force_proj_busy = FALSE;
                return ITEM_INTERACT_BLOCKING;
            }
            force_proj_busy = FALSE;
        }

        playsound(this, Dm.resource("sound/items/weapons/resonator_fire.ogg"), 50, TRUE);
        user.visible_message(span_warning(I18n.T("[] projects a forcefield!", user)), span_notice(I18n.T("You project a forcefield.")));
        obj_structure_projected_forcefield F = Dm.@new<obj_structure_projected_forcefield>(T, this);
        Dm.append(ref current_fields, F);
        user.changeNext_move(CLICK_CD_MELEE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/devices/forcefieldprojector.dm:69
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(LAZYLEN(current_fields)))
        {
            to_chat(user, span_notice(I18n.T("You deactivate [], disabling all active forcefields.", this)));
            foreach (obj_structure_projected_forcefield F in Dm.each<obj_structure_projected_forcefield>(current_fields.Copy()))
                qdel(F);
        }
        return null;
    }

    // tg: code/game/objects/items/devices/forcefieldprojector.dm:75
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("It is currently sustaining []/[] fields, and it's []% charged.", LAZYLEN(current_fields), max_fields, round((shield_integrity / max_shield_integrity) * 100))));
        return dot;
    }

    // tg: code/game/objects/items/devices/forcefieldprojector.dm:79
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        current_fields = Dm.list();
        START_PROCESSING(SSobj, this);
        return dot;
    }

    // tg: code/game/objects/items/devices/forcefieldprojector.dm:84
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSobj, this);
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/devices/forcefieldprojector.dm:88
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!Dm.truthy(LAZYLEN(current_fields)))
            shield_integrity = min(shield_integrity + Dm.num(seconds_per_tick) * 2, max_shield_integrity);
        else
            shield_integrity = max(shield_integrity - LAZYLEN(current_fields) * Dm.num(seconds_per_tick) * 0.5f, 0); //fields degrade slowly over time
        foreach (obj_structure_projected_forcefield F in Dm.each<obj_structure_projected_forcefield>(current_fields.Copy()))
        {
            if (shield_integrity <= 0 || get_dist(F, this) > field_distance_limit)
                qdel(F);
        }
        return null;
    }
}

// tg: code/game/objects/items/devices/forcefieldprojector.dm:97
public partial class obj_structure_projected_forcefield : obj_structure
{
    // tg: code/game/objects/items/devices/forcefieldprojector.dm:121
    [DmParams("mapload", "origin")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        obj_item_forcefield_projector origin = Dm.arg<obj_item_forcefield_projector>(__extra, 0);
        object dot = base.Initialize(mapload, Dm.forward(__extra, origin));
        generator = origin;
        return dot;
    }

    // tg: code/game/objects/items/devices/forcefieldprojector.dm:125
    public override object Destroy(float? force = null)
    {
        visible_message(span_warning(I18n.T("[] flickers and disappears!", this)));
        playsound(this, Dm.resource("sound/items/weapons/resonator_blast.ogg"), 25, TRUE);
        if (Dm.truthy(generator))
        {
            Dm.remove(ref generator.current_fields, this);
            generator = null;
        }
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/devices/forcefieldprojector.dm:133
    public override object play_attack_sound(object damage_amount = null, string damage_type = null, object damage_flag = null, params object[] __extra)
    {
        damage_type ??= BRUTE;
        damage_flag ??= 0;
        playsound(loc, Dm.resource("sound/items/weapons/egloves.ogg"), 80, TRUE);
        return null;
    }

    // tg: code/game/objects/items/devices/forcefieldprojector.dm:136
    public override async DmTask<object> take_damage(object damage_amount = null, string damage_type = null, object damage_flag = null, float? sound_effect = null, object attack_dir = null, float? armour_penetration = null)
    {
        damage_type ??= BRUTE;
        damage_flag ??= 0;
        sound_effect ??= 1;
        if (Dm.truthy(sound_effect))
            play_attack_sound(damage_amount, damage_type, damage_flag);
        if (Dm.truthy(generator))
            generator.shield_integrity = max(generator.shield_integrity - Dm.num(damage_amount), 0);
        return null;
    }
}
