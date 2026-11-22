// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/ruin_defender/mimic/mimic.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The base mimic and the copy mimic with the machine (gaps package C1). Left inert: the crate mimic (its storage and lock action), the gun mimic (the guns' firing through a mob), xenobio: rows in the report; animatable_blacklist is the staff of animation's list.

public partial class datum_controller_global_vars : datum_controller
{
    //TODO: Better solution for these?
    // tg: code/modules/mob/living/basic/ruin_defender/mimic/mimic.dm:3
    public DmList animatable_blacklist;

    public virtual void InitGlobalanimatable_blacklist()
    {
        animatable_blacklist = typecacheof(Dm.list(typeof(obj_structure_table), typeof(obj_structure_cable), typeof(obj_structure_window), typeof(obj_structure_blob))) as DmList;
        Dm.append(ref gvars_datum_init_order, "animatable_blacklist");
    }
}

// tg: code/modules/mob/living/basic/ruin_defender/mimic/mimic.dm:10
[DmPath("/mob/living/basic/mimic")]
public partial class mob_living_basic_mimic : mob_living_basic
{
    // tg: code/modules/mob/living/basic/ruin_defender/mimic/mimic.dm:38
    public override async DmTask<object> melee_attack(atom target = null, DmList modifiers = null, float? ignore_cooldown = null)
    {
        object dot = await base.melee_attack(target, modifiers, ignore_cooldown);
        if (!Dm.truthy(dot) || !Dm.truthy(knockdown_people) || !prob(15) || target is not mob_living_carbon carbon_target)
            return dot;
        await carbon_target.Paralyze(4 * SECONDS);
        carbon_target.visible_message(span_danger(I18n.T("\\The [] knocks down \\the []!", this, carbon_target)),
            span_userdanger(I18n.T("\\The [] knocks you down!", this)));
        return dot;
    }
}

// tg: code/modules/mob/living/basic/ruin_defender/mimic/mimic.dm:258
[DmPath("/mob/living/basic/mimic/copy")]
public partial class mob_living_basic_mimic_copy : mob_living_basic_mimic
{
    /// googly eyes overlay (rules 10.6: a TgState field, made on first use)
    // tg: code/modules/mob/living/basic/ruin_defender/mimic/mimic.dm:266
    private static mutable_appearance googly_eyes => TgState.Current.mob_living_basic_mimic_copy__googly_eyes ??= mutable_appearance(Dm.resource("icons/mob/simple/mob.dmi"), "googly_eyes") as mutable_appearance;

    // tg: code/modules/mob/living/basic/ruin_defender/mimic/mimic.dm:274
    [DmParams("mapload", "copy", "creator", "destroy_original", "no_googlies")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        obj copy = Dm.arg<obj>(__extra, 0);
        mob_living creator = Dm.arg<mob_living>(__extra, 1);
        object destroy_original = Dm.arg(__extra, 2) ?? FALSE;
        object no_googlies = Dm.arg(__extra, 3) ?? FALSE;
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_PERMANENTLY_MORTAL, INNATE_TRAIT); // They won't remember their original contents upon ressurection and would just be floating eyes
        if (Dm.truthy(no_googlies))
            overlay_googly_eyes = FALSE;
        Waiting.NoSleep(CopyObject(copy, creator, destroy_original));
        return dot;
    }

    // tg: code/modules/mob/living/basic/ruin_defender/mimic/mimic.dm:281
    public override object Destroy(float? force = null)
    {
        creator_ref = null;
        copied_ref = null;
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/basic/ruin_defender/mimic/mimic.dm:286
    public override object Life(object seconds_per_tick = null, params object[] __extra)
    {
        seconds_per_tick ??= SSMOBS_DT();
        object dot = base.Life(seconds_per_tick, __extra);
        if (!Dm.truthy(dot)) //dead or deleted
            return dot;
        if (Dm.truthy(idledamage) && !Dm.truthy(ckey) && !Dm.truthy((ai_controller as datum_ai_controller)?.blackboard[BB_CURRENT_TARGET])) //Objects eventually revert to normal if no one is around to terrorize
            Waiting.NoSleep(adjust_brute_loss(0.5f * Dm.num(seconds_per_tick)));
        foreach (mob_living victim in Dm.each<mob_living>(contents)) //a fix for animated statues from the flesh to stone spell
        {
            Waiting.NoSleep(death());
            return dot;
        }
        return dot;
    }

    // tg: code/modules/mob/living/basic/ruin_defender/mimic/mimic.dm:296
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        foreach (atom_movable movable in Dm.each_cast<atom_movable>(this))
            await movable.forceMove(get_turf(this));
        return await base.death(gibbed, __extra);
    }

    // tg: code/modules/mob/living/basic/ruin_defender/mimic/mimic.dm:301
    public override async DmTask<object> wabbajack(object what_to_randomize = null, float? change_flags = null)
    {
        visible_message(span_warning(I18n.T("[] resists polymorphing into a new creature!", this)));
        return null;
    }

    // tg: code/modules/mob/living/basic/ruin_defender/mimic/mimic.dm:307
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null) // if our object gets deleted it calls Exited
    {
        object dot = await base.Exited(gone, direction);
        atom_movable copied = (copied_ref as datum_weakref)?.resolve() as atom_movable;
        if (copied == null)
        {
            copied_ref = null;
            return dot;
        }
        if (QDELETED(this) || gone != copied)
            return dot;
        await death();
        return dot;
    }

    // tg: code/modules/mob/living/basic/ruin_defender/mimic/mimic.dm:317
    public virtual async DmTask change_owner(mob owner = null)
    {
        mob creator_resolved = (creator_ref as datum_weakref)?.resolve() as mob;
        if (creator_resolved == null)
            creator_ref = null;
        if (QDELETED(owner) || creator_resolved == owner)
            return;
        if (owner is mob_living living_owner)
        {
            unfriend(creator_resolved as mob_living);
            await befriend(living_owner);
        }
        creator_ref = WEAKREF(owner) as datum_weakref;
    }

    /// Check whether this object can be copied. If destroy_original is true, this proc is ignored.
    // tg: code/modules/mob/living/basic/ruin_defender/mimic/mimic.dm:329
    public virtual object check_object(obj target = null)
    {
        return Dm.num((isitem(target) || isstructure(target)) && !Dm.truthy(is_type_in_typecache(target, GLOB.animatable_blacklist)));
    }

    // tg: code/modules/mob/living/basic/ruin_defender/mimic/mimic.dm:332
    public virtual async DmTask<object> CopyObject(obj original = null, mob_living user = null, object destroy_original = null)
    {
        destroy_original ??= FALSE;
        if (QDELETED(original) || (!Dm.truthy(destroy_original) && !Dm.truthy(check_object(original))))
            return FALSE;
        if (!Dm.truthy(destroy_original))
            await original.forceMove(this);
        copied_ref = WEAKREF(original) as datum_weakref;
        CopyObjectVisuals(original);
        if (Dm.truthy(overlay_googly_eyes))
            add_overlay(googly_eyes);
        if (isstructure(original) || ismachinery(original))
        {
            health = (Dm.num(anchored) * 50) + 50;
            if (Dm.truthy(original.density) && Dm.truthy(original.anchored))
            {
                knockdown_people = TRUE;
                melee_damage_lower = Dm.num(melee_damage_lower) * 2;
                melee_damage_upper = Dm.num(melee_damage_upper) * 2;
            }
        }
        else if (original is obj_item I)
        {
            health = 15 * Dm.num(I.w_class);
            melee_damage_lower = 2 + Dm.num(I.force);
            melee_damage_upper = 2 + Dm.num(I.force);
        }
        maxHealth = health;
        if (user != null)
            await change_owner(user);
        if (Dm.truthy(destroy_original))
            qdel(original);
        return TRUE;
    }

    /// Copies the object visually including name and desc
    // tg: code/modules/mob/living/basic/ruin_defender/mimic/mimic.dm:360
    public virtual void CopyObjectVisuals(obj original = null)
    {
        name = original.name;
        desc = original.desc;
        icon = original.icon;
        icon_state = original.icon_state;
        icon_living = icon_state;
        copy_overlays(original, cut_old: TRUE);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/basic/ruin_defender/mimic/mimic.dm:266  var/static/mutable_appearance/googly_eyes
    internal mutable_appearance mob_living_basic_mimic_copy__googly_eyes;
}
