// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/reagent_containers/patch.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice C: the whole file (a patch stuck where it was clicked, its embedding feeding the patient through the skin, its
// overlay on the body turning with him, burning off). Deferred leaves: the patch's game log (M10). The var blocks are
// generated.

// tg: code/modules/reagents/reagent_containers/patch.dm:1
public partial class obj_item_reagent_containers_applicator_patch : obj_item_reagent_containers_applicator
{
    // tg: code/modules/reagents/reagent_containers/patch.dm:15
    public override object canconsume(mob eater = null, mob user = null)
    {
        if (!iscarbon(eater))
            return FALSE;
        mob_living_carbon carbon_eater = (mob_living_carbon)eater;
        obj_item_bodypart affecting = carbon_eater.get_bodypart(check_zone(user.zone_selected) as string) as obj_item_bodypart;
        if (affecting == null)
        {
            to_chat(user, span_warning(I18n.T("The limb is missing!")));
            return FALSE;
        }
        if (!Dm.truthy(IS_ORGANIC_LIMB(affecting)))
        {
            to_chat(user, span_notice(I18n.T("Medicine won't work on an inorganic limb!")));
            return FALSE;
        }
        return TRUE;
    }

    // tg: code/modules/reagents/reagent_containers/patch.dm:28
    public override async DmTask<object> on_consumption(mob consumer = null, mob giver = null, DmList modifiers = null)
    {
        mob_living_carbon carbon_consumer = (mob_living_carbon)consumer;
        consumer.log_message(Dm.text("Had \\a [] patch applied by [], containing the following reagents: [].", this, giver, english_list(((datum_reagents)reagents).reagent_list)), LOG_GAME);
        object clicked_x = LAZYACCESS(modifiers, ICON_X);
        object clicked_y = LAZYACCESS(modifiers, ICON_Y);
        if (clicked_x == null)
            clicked_x = ICON_SIZE_X * (0.5f + rand(-5, 5) * 0.05f);
        else
            clicked_x = text2num(clicked_x);
        if (clicked_y == null)
            clicked_y = ICON_SIZE_Y * (0.5f + rand(-5, 5) * 0.05f);
        else
            clicked_y = text2num(clicked_y);

        await giver.do_attack_animation(consumer, used_item: this);
        datum_embedding_med_patch embed = get_embed() as datum_embedding_med_patch;
        if (embed == null)
        {
            // tg: embed = set_embed(/datum/embedding/med_patch); set_embed returns nothing, so the new data is read back
            set_embed(typeof(datum_embedding_med_patch));
            embed = get_embed() as datum_embedding_med_patch;
        }
        embed.overlay_x = Dm.num(clicked_x) - ICON_SIZE_X * 0.5f;
        embed.overlay_y = Dm.num(clicked_y) - ICON_SIZE_Y * 0.5f;
        await force_embed(carbon_consumer, carbon_consumer.get_bodypart(check_zone(giver.zone_selected) as string) is var limb && Dm.truthy(limb) ? limb : carbon_consumer.get_bodypart(BODY_ZONE_CHEST));
        return null;
    }
}

// tg: code/modules/reagents/reagent_containers/patch.dm:52
public partial class datum_embedding_med_patch : datum_embedding
{
    // tg: code/modules/reagents/reagent_containers/patch.dm:75
    public override void set_owner(mob_living_carbon victim = null, obj_item_bodypart target_limb = null)
    {
        base.set_owner(victim, target_limb);
        overlay_setup();
        RegisterSignal(owner, COMSIG_LIVING_IGNITED, nameof(on_ignited));
        RegisterSignal(owner, COMSIG_ATOM_DIR_CHANGE, nameof(on_dir_change));
    }

    // tg: code/modules/reagents/reagent_containers/patch.dm:81
    public override async DmTask<object> stop_embedding()
    {
        if (owner != null)
        {
            UnregisterSignal(owner, Dm.list(COMSIG_LIVING_IGNITED, COMSIG_ATOM_DIR_CHANGE));
            if (patch_overlay != null)
                owner.cut_overlay(patch_overlay);
        }
        patch_overlay = null; // QDEL_NULL(patch_overlay): a /mutable_appearance is a value with no Destroy (rules 3.7)
        return await base.stop_embedding();
    }

    // tg: code/modules/reagents/reagent_containers/patch.dm:89
    public override object can_embed(atom_movable source = null, mob_living_carbon victim = null, object hit_zone = null, datum_thrownthing throwingdatum = null)
    {
        object dot = base.can_embed(source, victim, hit_zone, throwingdatum);
        if (!Dm.truthy(dot))
            return dot;
        obj_item_bodypart affecting = (victim.get_bodypart(hit_zone as string) is var part && Dm.truthy(part) ? part : victim.get_bodypart()) as obj_item_bodypart;
        if (!Dm.truthy(IS_ORGANIC_LIMB(affecting)))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/reagents/reagent_containers/patch.dm:98
    [SignalHandler]
    public virtual void on_ignited(datum source = null)
    {
        if (!Dm.truthy(Dm.bitand(parent.resistance_flags, FLAMMABLE)))
            return;
        if (ishuman(owner))
        {
            mob_living_carbon_human as_human = (mob_living_carbon_human)owner;
            if (Dm.num(as_human.get_thermal_protection()) >= FIRE_IMMUNITY_MAX_TEMP_PROTECT)
                return;
        }
        Waiting.Detach(fall_out());
        qdel(parent);
    }

    /// Create a patch overlay and add it to the mob
    // tg: code/modules/reagents/reagent_containers/patch.dm:110
    public virtual void overlay_setup()
    {
        applied_dir = Dm.num(owner.dir);
        patch_overlay = (mutable_appearance)mutable_appearance(parent.icon, parent.icon_state, FLOAT_LAYER, parent, appearance_flags: Dm.bitor(KEEP_APART, RESET_COLOR));
        patch_overlay.color = parent.color;
        if (Dm.truthy(parent.cached_color_filter))
            patch_overlay = filter_appearance_recursive(patch_overlay, parent.cached_color_filter);
        patch_overlay.transform = Dm.mul(patch_overlay.transform, 0.5f);
        patch_overlay.pixel_w = overlay_x;
        patch_overlay.pixel_z = overlay_y;
        owner.add_overlay(patch_overlay);
    }

    /// Changes visual position of the patch based on owner's rotation
    // tg: code/modules/reagents/reagent_containers/patch.dm:123
    [SignalHandler]
    public virtual void on_dir_change(datum source = null, object old_dir = null, object new_dir = null)
    {
        owner.cut_overlay(patch_overlay);
        float dir_new = Dm.num(new_dir);
        if (dir_new == applied_dir)
        {
            patch_overlay.pixel_w = overlay_x;
            patch_overlay.layer = FLOAT_LAYER;
            owner.add_overlay(patch_overlay);
            return;
        }

        if (dir_new == REVERSE_DIR(applied_dir))
        {
            patch_overlay.pixel_w = -overlay_x;
            patch_overlay.layer = BELOW_MOB_LAYER;
            owner.add_overlay(patch_overlay);
            return;
        }

        float check_dir = EAST;
        float check_new_dir = SOUTH;
        if (Dm.truthy(Dm.bitand(applied_dir, Dm.bitor(NORTH, SOUTH))))
        {
            check_dir = NORTH;
            check_new_dir = EAST;
        }

        // Turn ourselves based on how we were placed originally
        float dir_sign = Dm.num(Dm.bitand(applied_dir, check_dir));
        if (overlay_x >= 0)
            dir_sign = Dm.not(dir_sign);
        if (Dm.truthy(Dm.bitand(dir_new, check_new_dir)))
            dir_sign = Dm.not(dir_sign);

        // 0.5 multiplier to fake perspective
        patch_overlay.pixel_w = overlay_x * (Dm.truthy(dir_sign) ? 0.5f : -0.5f);
        patch_overlay.layer = Dm.truthy(dir_sign) ? FLOAT_LAYER : BELOW_MOB_LAYER;
        owner.add_overlay(patch_overlay);
    }

    // tg: code/modules/reagents/reagent_containers/patch.dm:157
    public override async DmTask<object> process_effect(object seconds_per_tick = null)
    {
        if (HAS_TRAIT(owner, TRAIT_STASIS))
            return null;

        datum_reagents holder = (datum_reagents)parent.reagents;
        if (!Dm.truthy(holder.total_volume))
        {
            await fall_out();
            qdel(parent);
            return TRUE;
        }

        float show_message = FALSE;
        if (reagent_message_cd <= world.time) // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm), inline (rules 11.1 (a))
        {
            show_message = TRUE;
            reagent_message_cd = world.time + PATCH_MESSAGE_COOLDOWN; // COOLDOWN_START, inline
        }

        await holder.trans_to(owner, transfer_per_second, methods: PATCH, show_message: show_message);
        return null;
    }
}

// tg: code/modules/reagents/reagent_containers/patch.dm:203
public partial class obj_item_reagent_containers_applicator_patch_synthflesh : obj_item_reagent_containers_applicator_patch
{
    // tg: code/modules/reagents/reagent_containers/patch.dm:211
    public override object canconsume(mob eater = null, mob user = null)
    {
        object dot = base.canconsume(eater, user);
        if (!iscarbon(eater))
            return dot;
        if (((datum_reagents)reagents).has_reagent(typeof(datum_reagent_medicine_c2_synthflesh)) is not datum_reagent_medicine_c2_synthflesh)
            return dot;
        // Check mob damage for synthflesh unhusking
        mob_living_carbon carbies = (mob_living_carbon)eater;
        if (HAS_TRAIT_FROM(carbies, TRAIT_HUSK, BURN) && Dm.num(carbies.get_fire_loss()) > UNHUSK_DAMAGE_THRESHOLD * 2.5f)
        {
            // give them a warning if the mob is a husk but synthflesh won't unhusk yet
            carbies.visible_message(span_boldwarning(I18n.T("[]'s burns need to be repaired first before synthflesh will unhusk it!", carbies)));
        }
        return dot;
    }
}
