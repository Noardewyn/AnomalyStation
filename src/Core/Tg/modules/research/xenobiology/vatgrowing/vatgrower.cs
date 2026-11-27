// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/xenobiology/vatgrowing/vatgrower.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. DEVIATION(DV12): the vat's glow overlay on the emissive plane is not created. The var blocks are
// generated (D111).

///Used to make mobs from microbiological samples. Grow grow grow.
// tg: code/modules/research/xenobiology/vatgrowing/vatgrower.dm:2
public partial class obj_machinery_vatgrower : obj_machinery
{
    // tg: code/modules/research/xenobiology/vatgrowing/vatgrower.dm:21
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        create_reagents(reagent_volume, reagent_flags);

        if (Dm.truthy(use_plumbing))
        {
            Waiting.NoSleep(AddElement(typeof(datum_element_simple_rotation)));
            Waiting.NoSleep(AddComponent(typeof(datum_component_plumbing_simple_demand)));
        }

        Waiting.NoSleep(AddElement(typeof(datum_element_contextual_screentip_item_typechecks), TgState.Current.obj_machinery_vatgrower__Initialize__hovering_item_typechecks));
        Waiting.NoSleep(AddElement(typeof(datum_element_contextual_screentip_bare_hands), Dm.named("lmb_text", I18n.T("Toggle Resampler")), Dm.named("rmb_text", I18n.T("Flush Soup"))));
        return dot;
    }

    // tg: code/modules/research/xenobiology/vatgrowing/vatgrower.dm:40
    public override void create_reagents(object max_vol = null, object flags = null)
    {
        base.create_reagents(max_vol, flags);
        RegisterSignal(reagents as datum, COMSIG_REAGENTS_HOLDER_UPDATED, nameof(on_reagent_change));
    }

    ///When we process, we make use of our reagents to try and feed the samples we have.
    // tg: code/modules/research/xenobiology/vatgrowing/vatgrower.dm:45
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!Dm.truthy(is_operational))
            return null;
        if (biological_sample == null)
            return null;
        // the sample's handle_growth returns nothing, so tg never plays the slosh nor its message (kept)
        Waiting.NoSleep(biological_sample.handle_growth(this));
        object grew = null;
        if (Dm.truthy(grew))
        {
            if (!prob(10))
                return null;
            playsound(loc, "sound/effects/slosh.ogg", 25, TRUE);
            audible_message(pick(Dm.list(span_notice(I18n.T("[] grumbles!", this)), span_notice(I18n.T("[] makes a splashing noise!", this)), span_notice(I18n.T("[] sloshes!", this)))));
        }
        Waiting.NoSleep(use_energy(active_power_usage * Dm.num(seconds_per_tick)));
        return null;
    }

    // tg: code/modules/research/xenobiology/vatgrowing/vatgrower.dm:57
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.item_interaction(user, tool, modifiers, __extra);
        if (tool is obj_item_petri_dish)
            return deposit_sample(user, (obj_item_petri_dish)tool);
        return dot;
    }

    // tg: code/modules/research/xenobiology/vatgrowing/vatgrower.dm:62
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/modules/research/xenobiology/vatgrowing/vatgrower.dm:65
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/modules/research/xenobiology/vatgrowing/vatgrower.dm:68
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.wrench_act(user, tool);
        if (Dm.truthy(await default_unfasten_wrench(user, tool)))
            return ITEM_INTERACT_SUCCESS;
        return dot;
    }

    // tg: code/modules/research/xenobiology/vatgrowing/vatgrower.dm:73
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        playsound(this, "sound/machines/click.ogg", 30, TRUE);
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return dot;
        resampler_active = Dm.truthy(resampler_active) ? FALSE : TRUE;
        balloon_alert(user, I18n.T("resampler []", Dm.truthy(resampler_active) ? I18n.T("activated") : I18n.T("deactivated")));
        update_appearance();
        return dot;
    }

    // tg: code/modules/research/xenobiology/vatgrowing/vatgrower.dm:82
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_hand_secondary(user, modifiers);
        if (Dm.eq(dot, SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN))
            return dot;
        if (!Dm.truthy(anchored))
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        object warning = await tgui_alert(user, I18n.T("Are you sure you want to empty the soup container?"), I18n.T("Flush soup container?"), Dm.list(I18n.T("Flush"), I18n.T("Cancel")));
        if (Dm.eq(warning, I18n.T("Flush")) && Dm.truthy(await user.can_perform_action(this)))
        {
            await (reagents as datum_reagents).clear_reagents();
            if (biological_sample != null)
                QDEL_NULL(ref biological_sample);
            balloon_alert(user, I18n.T("container empty"));
        }
        update_appearance();
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    ///Creates a clone of the supplied sample and puts it in the vat
    // tg: code/modules/research/xenobiology/vatgrowing/vatgrower.dm:98
    public virtual object deposit_sample(mob user = null, obj_item_petri_dish petri = null)
    {
        if (petri.sample == null)
        {
            balloon_alert(user, I18n.T("dish empty"));
            return ITEM_INTERACT_FAILURE;
        }
        if (biological_sample != null)
        {
            balloon_alert(user, I18n.T("already has a sample"));
            return ITEM_INTERACT_FAILURE;
        }
        biological_sample = Dm.@new<datum_biological_sample>();
        foreach (datum_micro_organism m in Dm.each<datum_micro_organism>(petri.sample.micro_organisms))
            Dm.append(ref biological_sample.micro_organisms, Waiting.NoSleep(Dm.new_async(m.type)));
        biological_sample.sample_layers = petri.sample.sample_layers;
        biological_sample.sample_color = petri.sample.sample_color;
        balloon_alert(user, I18n.T("added sample"));
        playsound(this, "sound/effects/bubbles/bubbles.ogg", 50, TRUE);
        update_appearance();
        RegisterSignal(biological_sample, COMSIG_SAMPLE_GROWTH_COMPLETED, nameof(on_sample_growth_completed));
        return ITEM_INTERACT_SUCCESS;
    }

    ///Adds text for when there is a sample in the vat
    // tg: code/modules/research/xenobiology/vatgrowing/vatgrower.dm:117
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (biological_sample == null)
            return dot;
        Dm.append(ref dot, span_notice(I18n.T("It seems to have a sample in it!")));
        foreach (object i in Dm.each(biological_sample.micro_organisms))
        {
            datum_micro_organism MO = i as datum_micro_organism;
            Dm.append(ref dot, MO.get_details(Dm.num(HAS_TRAIT(user, TRAIT_RESEARCH_SCANNER))));
        }
        return dot;
    }

    /// Call update icon when reagents change to update the reagent content icons. Eats signal args.
    // tg: code/modules/research/xenobiology/vatgrowing/vatgrower.dm:127
    [SignalHandler]
    public virtual void on_reagent_change(datum_reagents holder = null)
    {
        update_appearance();
    }

    ///Adds overlays to show the reagent contents
    // tg: code/modules/research/xenobiology/vatgrowing/vatgrower.dm:132
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        // var/static/image/on_overlay, off_overlay and emissive_overlay (rules 10.6): one set for every vat, made from the first
        // vat that draws itself (tg's own)
        ref object on_overlay = ref TgState.Current.obj_machinery_vatgrower__update_overlays__on_overlay;
        ref object off_overlay = ref TgState.Current.obj_machinery_vatgrower__update_overlays__off_overlay;
        if (on_overlay == null)
        {
            on_overlay = iconstate2appearance(icon, Dm.text("[]_on", icon_state));
            off_overlay = iconstate2appearance(icon, Dm.text("[]_off", icon_state));
            // DEVIATION(DV12): emissive_overlay = emissive_appearance(icon, "[icon_state]_glow", src, alpha = src.alpha), the glow on
            // the emissive plane, is not created, and . += emissive_overlay adds nothing
        }
        if (Dm.truthy(is_operational))
        {
            if (Dm.truthy(resampler_active))
                Dm.append(ref dot, on_overlay);
            else
                Dm.append(ref dot, off_overlay);
        }
        if (!Dm.truthy((reagents as datum_reagents).total_volume))
            return dot;
        object reagentcolor = mix_color_from_reagents((reagents as datum_reagents).reagent_list);
        mutable_appearance base_overlay = mutable_appearance(icon, Dm.text("[]_reagent", icon_state), appearance_flags: Dm.bitor(RESET_COLOR, KEEP_APART)) as mutable_appearance;
        base_overlay.color = reagentcolor;
        Dm.append(ref dot, base_overlay);
        if (biological_sample != null && Dm.truthy(is_operational))
        {
            object bubbles_overlay = mutable_appearance(icon, Dm.text("[]_bubbles", icon_state));
            Dm.append(ref dot, bubbles_overlay);
        }
        return dot;
    }

    // tg: code/modules/research/xenobiology/vatgrowing/vatgrower.dm:157
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        playsound(this, SFX_SPARKS, 100, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        balloon_alert(user, I18n.T("resampling circuit overloaded"));
        flick("growing_vat_emagged", this);
        return TRUE;
    }

    // tg: code/modules/research/xenobiology/vatgrowing/vatgrower.dm:166
    [SignalHandler]
    public virtual void on_sample_growth_completed()
    {
        if (Dm.truthy(resampler_active))
        {
            _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.playsound), get_turf(this), Dm.resource("sound/effects/servostep.ogg"), 100, 1), 1.5f * SECONDS, file: "code/modules/research/xenobiology/vatgrowing/vatgrower.dm", line: 169);
            biological_sample.reset_sample();
        }
        else
            QDEL_NULL(ref biological_sample);
        update_appearance();
    }
}

// tg: code/modules/research/xenobiology/vatgrowing/vatgrower.dm:175
public partial class obj_machinery_vatgrower_small : obj_machinery_vatgrower
{
    // tg: code/modules/research/xenobiology/vatgrowing/vatgrower.dm:189
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        return null;
    }

    // tg: code/modules/research/xenobiology/vatgrowing/vatgrower.dm:192
    public override object deposit_sample(mob user = null, obj_item_petri_dish petri = null)
    {
        object dot = base.deposit_sample(user, petri);

        if (!Dm.eq(dot, ITEM_INTERACT_SUCCESS))
            return dot;

        RegisterSignal(biological_sample, COMSIG_SAMPLE_DEPOSITED, nameof(on_sample_deposited));
        return dot;
    }

    // tg: code/modules/research/xenobiology/vatgrowing/vatgrower.dm:200
    [SignalHandler]
    public virtual void on_sample_deposited(datum_biological_sample sample = null, atom thing = null)
    {
        if (isliving(thing))
        {
            mob_living living_type = (mob_living)thing;
            if (living_type.mob_size > MOB_SIZE_SMALL)
            {
                Waiting.Detach(explosion(this, light_impact_range: 2, explosion_cause: "growing vat too small"));
                qdel(this);
                return;
            }
        }
    }
}

public sealed partial class TgState
{
    // tg: code/modules/research/xenobiology/vatgrowing/vatgrower.dm:28  /obj/machinery/vatgrower/Initialize's var/static/list/hovering_item_typechecks
    internal DmList obj_machinery_vatgrower__Initialize__hovering_item_typechecks = Dm.list(
        Dm.kv(typeof(obj_item_petri_dish), Dm.list(Dm.kv(SCREENTIP_CONTEXT_LMB, "Add Sample"))),
        Dm.kv(typeof(obj_item_reagent_containers), Dm.list(Dm.kv(SCREENTIP_CONTEXT_LMB, "Pour Reagents"))));
    // tg: code/modules/research/xenobiology/vatgrowing/vatgrower.dm:134  update_overlays' var/static/image/on_overlay, off_overlay
    internal object obj_machinery_vatgrower__update_overlays__on_overlay;
    internal object obj_machinery_vatgrower__update_overlays__off_overlay;
}
