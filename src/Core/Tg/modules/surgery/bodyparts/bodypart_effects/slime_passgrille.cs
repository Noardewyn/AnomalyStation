// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/bodyparts/bodypart_effects/slime_passgrille.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SP2c (D349): the whole file (a jellyperson without clothes squeezes through a grille). The var block is generated (D111).

/// Gives PASSGRILLE so long as all of the bodyparts are slime (and some other conditions)
// tg: code/modules/surgery/bodyparts/bodypart_effects/slime_passgrille.dm:3
public partial class datum_status_effect_grouped_bodypart_effect_slime_passgrille : datum_status_effect_grouped_bodypart_effect
{
    // tg: code/modules/surgery/bodyparts/bodypart_effects/slime_passgrille.dm:6
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = await base.on_apply(__extra);
        RegisterSignals(owner, Dm.list(COMSIG_CARBON_POST_REMOVE_LIMB, COMSIG_CARBON_POST_ATTACH_LIMB), nameof(check_active));
        return dot;
    }

    // tg: code/modules/surgery/bodyparts/bodypart_effects/slime_passgrille.dm:10
    public override async DmTask<object> on_remove()
    {
        object dot = await base.on_remove();
        UnregisterSignal(owner, Dm.list(
            COMSIG_ATOM_BUMPED,
            COMSIG_CARBON_POST_ATTACH_LIMB,
            COMSIG_CARBON_POST_REMOVE_LIMB,
            COMSIG_MOB_EQUIPPED_ITEM,
            COMSIG_MOB_UNEQUIPPED_ITEM,
            COMSIG_MOVABLE_MOVED));
        if (Dm.truthy(is_active))
            await deactivate();
        owner.remove_movespeed_modifier(typeof(datum_movespeed_modifier_walking_through_grille));
        return dot;
    }

    // tg: code/modules/surgery/bodyparts/bodypart_effects/slime_passgrille.dm:24
    public override object should_activate()
    {
        foreach (obj_item_bodypart other_part in Dm.each_cast<obj_item_bodypart>((owner as mob_living_carbon).get_bodyparts()))
        {
            if (Dm.truthy(Dm.bitand(other_part.pass_flags, PASSGRILLE)))
                continue;
            if (Dm.truthy(IS_ROBOTIC_LIMB(other_part)) || !Dm.truthy(Dm.bitand(other_part.biological_state, BIO_JELLY)))
                return FALSE;
        }

        foreach (obj_item_organ other_organ in Dm.each_cast<obj_item_organ>((owner as mob_living_carbon).organs)) // safe assertion
        {
            if (Dm.truthy(Dm.bitand(other_organ.pass_flags, PASSGRILLE)))
                continue;
            if (Dm.truthy(IS_ROBOTIC_ORGAN(other_organ))) // no check for jelly organs (yet), as half of their organs are normal fleshy things.
                return FALSE;
        }

        return TRUE;
    }

    // tg: code/modules/surgery/bodyparts/bodypart_effects/slime_passgrille.dm:39
    public override void activate()
    {
        base.activate();
        RegisterSignal(owner, COMSIG_MOVABLE_BUMP, nameof(on_bumped));
        RegisterSignal(owner, COMSIG_MOVABLE_MOVED, nameof(on_move));
    }

    // tg: code/modules/surgery/bodyparts/bodypart_effects/slime_passgrille.dm:44
    public override async DmTask deactivate()
    {
        await base.deactivate();
        UnregisterSignal(owner, Dm.list(
            COMSIG_MOVABLE_BUMP,
            COMSIG_MOVABLE_MOVED));
        if (QDELING(owner))
            return;

        if (Dm.truthy(Dm.bitand(owner.pass_flags, PASSGRILLE)))
            clear_passgrille();
        if (Dm.truthy(await try_spit_out()))
            to_chat(owner, span_warning(I18n.T("You get squeezed out of the crevice you were within!")));
    }

    // tg: code/modules/surgery/bodyparts/bodypart_effects/slime_passgrille.dm:58
    public virtual async DmTask<object> try_spit_out()
    {
        turf start_loc = get_turf(owner);
        if (start_loc == null || !Dm.truthy(await start_loc.is_blocked_turf(source_atom: owner)))
            return FALSE;

        if (Dm.truthy(await spit_out(owner.dir)))
            return TRUE;

        if (Dm.truthy(await spit_out(REVERSE_DIR(owner.dir))))
            return TRUE;

        return FALSE;
    }

    // tg: code/modules/surgery/bodyparts/bodypart_effects/slime_passgrille.dm:71
    [SignalHandler]
    public virtual void on_bumped(datum source = null, atom bumped = null)
    {
        if (!Dm.truthy(Dm.bitand(bumped.pass_flags_self, PASSGRILLE)))
            return;
        if (Dm.truthy(DOING_INTERACTION_WITH_TARGET(owner, bumped)))
            return;
        turf bump_turf = get_turf(bumped);
        if (Dm.truthy(Waiting.NoSleep(bump_turf.is_blocked_turf(source_atom: owner, ignore_atoms: Dm.list(bumped)))))
            return;

        Waiting.Detach(try_squeeze_into(bumped)); // INVOKE_ASYNC
    }

    // tg: code/modules/surgery/bodyparts/bodypart_effects/slime_passgrille.dm:84
    public virtual async DmTask try_squeeze_into(atom bumped = null)
    {
        float squeeze_time = 3 * SECONDS;
        string warning_message = "";
        DmList items = owner.get_equipped_items(INCLUDE_HELD) as DmList;
        // squeeze time goes up if they have any items on - not for balance reasons, but just to let them now they're about to be stripped clean.
        if (length(items) != 0)
        {
            squeeze_time += 2 * SECONDS;
            warning_message = I18n.T(" (This will drop all of your equipment!)");
        }
        // another check is made for dangerous_unequip items, which will also add to the squeeze time and give a more specific warning message.
        foreach (obj_item thing in Dm.each_cast<obj_item>(items))
        {
            if (HAS_TRAIT(thing, TRAIT_DANGEROUS_UNEQUIP))
            {
                squeeze_time += 3 * SECONDS;
                warning_message = I18n.T(" (This will drop ALL of your equipment, <b>including []</b>!)", thing);
                break;
            }
        }

        owner.visible_message(
            span_warning(I18n.T("[] starts squeezing through the crevices within []...", owner, bumped)),
            span_notice(I18n.T("You start squeezing through the crevices within []...[]", bumped, warning_message)),
            visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE);

        if (!Dm.truthy(await do_after(owner, squeeze_time, bumped)) || QDELETED(this) || !Dm.truthy(is_active))
            return;

        owner.visible_message(
            span_warning(I18n.T("[] squeezes into the crevices within []!", owner, bumped)),
            span_notice(I18n.T("You squeeze into the crevices within [].", bumped)),
            visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE);

        add_passgrille();
        await owner.forceMove(bumped.loc);
    }

    // tg: code/modules/surgery/bodyparts/bodypart_effects/slime_passgrille.dm:117
    public virtual async DmTask<object> spit_out(object in_direction = null)
    {
        // intentionally uses range over range_turfs to get closest turfs first
        foreach (object t__1 in Dm.each(range(2, get_step(owner, Dm.num(in_direction)))))
        {
            if (t__1 is not turf_open nearby_open_turf)
                continue;
            if (Dm.truthy(await nearby_open_turf.is_blocked_turf(exclude_mobs: TRUE, source_atom: owner)))
                continue;
            await owner.forceMove(nearby_open_turf);
            await owner.Paralyze(4 * SECONDS);
            await owner.Knockdown(8 * SECONDS);
            await owner.adjust_jitter(12 * SECONDS);
            await owner.bleed(36); // leaves a pool o jelly behind
            return TRUE;
        }

        return FALSE;
    }

    // tg: code/modules/surgery/bodyparts/bodypart_effects/slime_passgrille.dm:131
    [SignalHandler]
    public virtual void on_move(datum source = null, atom old_loc = null)
    {
        if (!Dm.truthy(Dm.bitand(owner.pass_flags, PASSGRILLE)))
            return;

        foreach (atom blocker in Dm.each_cast<atom>(owner.loc))
        {
            if (Dm.truthy(Dm.bitand(blocker.pass_flags_self, PASSGRILLE)))
                return;
        }

        // leaving grilles = back to normal. (though the effect is still active)
        clear_passgrille();
    }

    // tg: code/modules/surgery/bodyparts/bodypart_effects/slime_passgrille.dm:144
    public virtual void add_passgrille()
    {
        owner.add_traits(Dm.list(TRAIT_HANDS_BLOCKED, TRAIT_UNDENSE, TRAIT_PULL_BLOCKED, TRAIT_UI_BLOCKED), TRAIT_STATUS_EFFECT(id));
        owner.pass_flags = (int)owner.pass_flags | PASSGRILLE;
        Waiting.NoSleep(owner.add_movespeed_modifier(typeof(datum_movespeed_modifier_walking_through_grille)));

        mob_living_carbon carbon_owner = owner as mob_living_carbon;
        Waiting.NoSleep((carbon_owner.dna.species as datum_species).update_no_equip_flags(carbon_owner, ALL));
    }

    // tg: code/modules/surgery/bodyparts/bodypart_effects/slime_passgrille.dm:152
    public virtual void clear_passgrille()
    {
        owner.remove_traits(Dm.list(TRAIT_HANDS_BLOCKED, TRAIT_UNDENSE, TRAIT_PULL_BLOCKED, TRAIT_UI_BLOCKED), TRAIT_STATUS_EFFECT(id));
        owner.pass_flags = (int)owner.pass_flags & ~PASSGRILLE;
        owner.remove_movespeed_modifier(typeof(datum_movespeed_modifier_walking_through_grille));

        mob_living_carbon carbon_owner = owner as mob_living_carbon;
        Waiting.NoSleep((carbon_owner.dna.species as datum_species).update_no_equip_flags(carbon_owner, Dm.initial(carbon_owner.dna.species, (datum_species p) => p.no_equip_flags)));
    }
}
