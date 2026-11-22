// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/organs/external/tails.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps package C5): the tail's root, its overlay and the cat tail's butt sprite (gaps P3b), the xenomorphs' tails and their
// overlay (gaps X5), the lizard's tail spines (gaps SP1); the other species' tails wait for them (the monkey's and the lizard's own types
// are var-only, generated).

///Tail parent, it doesn't do very much.
// tg: code/modules/surgery/organs/external/tails.dm:3
public partial class obj_item_organ_tail : obj_item_organ
{
    // tg: code/modules/surgery/organs/external/tails.dm:25
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        mob_living_carbon receiver = organ_owner;
        object dot = await base.on_mob_insert(receiver, special, movement_flags);
        receiver.clear_mood_event("tail_lost");
        receiver.clear_mood_event("tail_balance_lost");

        if (!Dm.truthy(special)) // if some admin wants to give someone tail moodles for tail shenanigans, they can spawn it and do it by hand
        {
            original_owner ??= WEAKREF(receiver);

            // If it's your tail, an infinite debuff is replaced with a timed one
            // If it's not your tail but of same species, I guess it works, but we are more sad
            // If it's not your tail AND of different species, we are horrified
            if (IS_WEAKREF_OF(receiver, original_owner))
                await receiver.add_mood_event("tail_regained", typeof(datum_mood_event_tail_regained_right));
            else if (Dm.@in(type, ((datum_species)receiver.dna.species).mutant_organs))
                await receiver.add_mood_event("tail_regained", typeof(datum_mood_event_tail_regained_species));
            else
                await receiver.add_mood_event("tail_regained", typeof(datum_mood_event_tail_regained_wrong));
        }
        return dot;
    }

    // tg: code/modules/surgery/organs/external/tails.dm:43
    public override async DmTask on_bodypart_insert(obj_item_bodypart limb = null)
    {
        obj_item_organ_spines our_spines = limb.owner.get_organ_slot(ORGAN_SLOT_EXTERNAL_SPINES) as obj_item_organ_spines;
        if (our_spines != null)
            await try_insert_tail_spines(limb);
        await base.on_bodypart_insert(limb);
    }

    // tg: code/modules/surgery/organs/external/tails.dm:49
    public override async DmTask on_bodypart_remove(obj_item_bodypart limb = null, object movement_flags = null)
    {
        await remove_tail_spines(limb);
        await base.on_bodypart_remove(limb, movement_flags);
    }

    /// If the owner has spines and an appropriate overlay exists, add a tail spines overlay.
    // tg: code/modules/surgery/organs/external/tails.dm:54
    public virtual async DmTask try_insert_tail_spines(obj_item_bodypart bodypart = null)
    {
        // Don't insert another overlay if there already is one.
        if (tail_spines_overlay != null)
            return;
        // If this tail doesn't have a valid set of tail spines, don't insert them
        if ((bodypart_overlay as datum_bodypart_overlay_mutant).sprite_datum is not datum_sprite_accessory_tails tail_sprite_datum)
            return;
        object tail_spine_key = tail_sprite_datum.spine_key;
        if (!Dm.truthy(tail_spine_key))
            return;

        tail_spines_overlay = Dm.@new<datum_bodypart_overlay_mutant_tail_spines>();
        tail_spines_overlay.tail_spine_key = tail_spine_key;
        object feature_name = bodypart.owner.dna.features[FEATURE_SPINES]; //tail spines don't live in DNA, but share feature names with regular spines
        tail_spines_overlay.set_appearance_from_name(feature_name);
        await bodypart.add_bodypart_overlay(tail_spines_overlay);
    }

    /// If we have a tail spines overlay, delete it
    // tg: code/modules/surgery/organs/external/tails.dm:73
    public virtual async DmTask remove_tail_spines(obj_item_bodypart bodypart = null)
    {
        if (tail_spines_overlay == null)
            return;
        await bodypart.remove_bodypart_overlay(tail_spines_overlay);
        QDEL_NULL(ref tail_spines_overlay);
    }

    // tg: code/modules/surgery/organs/external/tails.dm:79
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        await base.on_mob_remove(organ_owner, special, movement_flags);
        mob_living_carbon carbon_owner = organ_owner as mob_living_carbon;

        if (Dm.truthy(Dm.bitand(wag_flags, WAG_WAGGING)))
            await stop_wag(carbon_owner);

        carbon_owner.clear_mood_event("tail_regained");

        if (!Dm.truthy(special) && carbon_owner.dna != null && Dm.@in(type, ((datum_species)carbon_owner.dna.species).mutant_organs))
        {
            await carbon_owner.add_mood_event("tail_lost", typeof(datum_mood_event_tail_lost));
            await carbon_owner.add_mood_event("tail_balance_lost", typeof(datum_mood_event_tail_balance_lost));
        }
    }

    ///We need some special behaviour for accessories, wrapped here so we can easily add more interactions later
    ///Accepts an optional timeout after which we remove the tail wagging
    ///Returns false if the wag worked, true otherwise
    // tg: code/modules/surgery/organs/external/tails.dm:94
    public virtual async DmTask<object> start_wag(mob_living_carbon organ_owner = null, float? stop_after = null)
    {
        stop_after ??= INFINITY;
        if (Dm.truthy(Dm.bitand(wag_flags, WAG_WAGGING)) || !Dm.truthy(Dm.bitand(wag_flags, WAG_ABLE))) // we are already wagging
            return FALSE;
        if (IS_UNCONSCIOUS_OR_CRIT(organ_owner) || organ_owner != owner) // no wagging when owner is ko'd or tail has been disembodied(?)
            return FALSE;

        if (stop_after != INFINITY)
            _addtimer(Dm.@new<datum_callback>(this, nameof(stop_wag), organ_owner), stop_after.Value, TIMER_STOPPABLE | TIMER_DELETE_ME, file: "code/modules/surgery/organs/external/tails.dm", line: 101);

        datum_bodypart_overlay_mutant_tail accessory = bodypart_overlay as datum_bodypart_overlay_mutant_tail;
        wag_flags = Dm.bitor(wag_flags, WAG_WAGGING);
        accessory.wagging = TRUE;
        if (tail_spines_overlay != null) //if there are spines, they should wag with the tail
            tail_spines_overlay.wagging = TRUE;
        await organ_owner.update_body_parts();
        RegisterSignals(organ_owner, Dm.list(COMSIG_MOB_STATCHANGE, SIGNAL_ADDTRAIT(TRAIT_KNOCKEDOUT)), nameof(owner_kod));
        return TRUE;
    }

    // tg: code/modules/surgery/organs/external/tails.dm:112
    public virtual void owner_kod(mob_living_carbon organ_owner = null) // Resisting the urge to replace owner with daddy
    {
        if (IS_UNCONSCIOUS_OR_CRIT(organ_owner))
            Waiting.Detach(stop_wag(organ_owner));
    }

    ///We need some special behaviour for accessories, wrapped here so we can easily add more interactions later
    ///Returns false if the wag stopping worked, true otherwise
    // tg: code/modules/surgery/organs/external/tails.dm:119
    public virtual async DmTask<object> stop_wag(mob_living_carbon organ_owner = null)
    {
        if (!Dm.truthy(Dm.bitand(wag_flags, WAG_ABLE)))
            return FALSE;

        object succeeded = FALSE;
        if (Dm.truthy(Dm.bitand(wag_flags, WAG_WAGGING)))
        {
            wag_flags = Dm.bitand(wag_flags, Dm.bitnot(WAG_WAGGING));
            succeeded = TRUE;
        }

        datum_bodypart_overlay_mutant_tail tail_overlay = bodypart_overlay as datum_bodypart_overlay_mutant_tail;
        tail_overlay.wagging = FALSE;
        if (tail_spines_overlay != null) //if there are spines, they should stop wagging with the tail
            tail_spines_overlay.wagging = FALSE;
        if (organ_owner == null)
            return succeeded;

        await organ_owner.update_body_parts();
        UnregisterSignal(organ_owner, Dm.list(COMSIG_MOB_STATCHANGE, SIGNAL_ADDTRAIT(TRAIT_KNOCKEDOUT)));
        return succeeded;
    }

    // tg: code/modules/surgery/organs/external/tails.dm:139
    public virtual object get_butt_sprite()
    {
        return null;
    }
}

///Tail parent type, with wagging functionality
// tg: code/modules/surgery/organs/external/tails.dm:144
public partial class datum_bodypart_overlay_mutant_tail : datum_bodypart_overlay_mutant
{
    // tg: code/modules/surgery/organs/external/tails.dm:155
    public override object get_base_icon_state()
    {
        return Dm.text("[][]", Dm.truthy(wagging) ? "wagging_" : "", sprite_datum.icon_state); //add the wagging tag if we be wagging
    }

    // tg: code/modules/surgery/organs/external/tails.dm:158
    public override object can_draw_on_bodypart(obj_item_bodypart bodypart_owner = null, mob_living_carbon owner = null)
    {
        if (!Dm.truthy(Dm.bitand(bodypart_owner.owner?.obscured_slots, HIDEJUMPSUIT)))
            return base.can_draw_on_bodypart(bodypart_owner, owner);
        if (!Dm.truthy(mesh_in_suits))
            return FALSE;
        if (Dm.locate<datum_bodypart_texture_mesh>(bodypart_owner.bodypart_textures) != null)
            return base.can_draw_on_bodypart(bodypart_owner, owner);
        return FALSE;
    }
}

// tg: code/modules/surgery/organs/external/tails.dm:167 (gaps P3b: the cat infusion)
public partial class obj_item_organ_tail_cat : obj_item_organ_tail
{
    // tg: code/modules/surgery/organs/external/tails.dm:175
    public override object get_butt_sprite()
    {
        return Dm.icon(Dm.resource("icons/mob/butts.dmi"), BUTT_SPRITE_CAT);
    }
}

// tg: code/modules/surgery/organs/external/tails.dm:203  (gaps X5: the alien queen's tail and its whip)
public partial class obj_item_organ_tail_xeno_queen : obj_item_organ_tail
{
    // tg: code/modules/surgery/organs/external/tails.dm:215
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        tail_whip = Dm.@new<datum_action_cooldown_spell_aoe_repulse_xeno>(this);
        return dot;
    }

    // tg: code/modules/surgery/organs/external/tails.dm:219
    [DmParams("receiver", "special", "movement_flags")]
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        mob_living_carbon receiver = organ_owner;
        object dot = await base.on_mob_insert(receiver, special, movement_flags);
        await tail_whip.Grant(receiver);
        await receiver.add_movespeed_modifier(typeof(datum_movespeed_modifier_tail_dragger));
        return dot;
    }

    // tg: code/modules/surgery/organs/external/tails.dm:224
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        await base.on_mob_remove(organ_owner, special, movement_flags);
        await tail_whip.Remove(organ_owner as mob);
        ((mob)organ_owner).remove_movespeed_modifier(typeof(datum_movespeed_modifier_tail_dragger));
    }
}

///Alien tail bodypart overlay
// tg: code/modules/surgery/organs/external/tails.dm:230  (gaps X5)
public partial class datum_bodypart_overlay_mutant_tail_xeno : datum_bodypart_overlay_mutant_tail
{
    // tg: code/modules/surgery/organs/external/tails.dm:238
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_appearance_from_name(default_appearance);
    }

    // tg: code/modules/surgery/organs/external/tails.dm:242
    public override void randomize_appearance()
    {
        set_appearance_from_name(default_appearance);
    }
}

///Bodypart overlay for tail spines. Handled by the tail - has no actual organ associated.
// tg: code/modules/surgery/organs/external/tails.dm:269 (gaps SP1: the lizards' spines)
[DmPath("/datum/bodypart_overlay/mutant/tail_spines")]
public partial class datum_bodypart_overlay_mutant_tail_spines : datum_bodypart_overlay_mutant
{
    /// Key for tail spine states, depends on the shape of the tail. Defined in the tail sprite datum.
    // tg: code/modules/surgery/organs/external/tails.dm:279  hand-declared: tg gives it the tail sprite's text key (try_insert_tail_spines), where
    // its NONE default made the generated var a number (types-converter.md 3.5)
    public object tail_spine_key = NONE;

    // tg: code/modules/surgery/organs/external/tails.dm:273  the var overrides (rules 10.2; the class is hand-written, so its constructor is too)
    public datum_bodypart_overlay_mutant_tail_spines()
    {
        feature_key = FEATURE_TAILSPINES;
        draw_on_husks = HUSK_OVERLAY_GRAYSCALE;
        offset_location = ENTIRE_BODY;
    }

    // tg: code/modules/surgery/organs/external/tails.dm:269  the object-creating default (rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        layers = Dm.list(
            Dm.kv(EXTERNAL_ADJACENT, BODY_ADJ_LAYER),
            Dm.kv(EXTERNAL_BEHIND, BODY_BEHIND_LAYER));
    }

    // tg: code/modules/surgery/organs/external/tails.dm:281
    public override object get_base_icon_state()
    {
        return Dm.add(Dm.add(tail_spine_key != null ? Dm.text("[]_", tail_spine_key) : "", Dm.truthy(wagging) ? "wagging_" : ""), sprite_datum.icon_state); // Select the wagging state if appropriate
    }

    // tg: code/modules/surgery/organs/external/tails.dm:284
    public override object can_draw_on_bodypart(obj_item_bodypart bodypart_owner = null, mob_living_carbon owner = null)
    {
        return Dm.num(Dm.truthy(base.can_draw_on_bodypart(bodypart_owner, owner)) && !Dm.truthy(Dm.bitand(bodypart_owner.owner?.obscured_slots, HIDEJUMPSUIT)));
    }

    // tg: code/modules/surgery/organs/external/tails.dm:287
    public override async DmTask set_dye_color(object new_color = null, obj_item_organ organ = null)
    {
        dye_color = new_color; //no update_body_parts() call, tail/set_dye_color will do it.
    }
}
