// Translated from tgstation (snapshot 1d99bd7f): code/modules/hallucination/inhand_fake_item.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice G (D187): the whole file. The fake item is a screen object of the hallucinator's client, drawn in a hand or belt or pocket slot of his
// HUD. The var blocks are generated (D111).

/// Hallucinates a fake item in our hands, pockets, or belt or whatever.
// tg: code/modules/hallucination/inhand_fake_item.dm:2
public partial class datum_hallucination_fake_item : datum_hallucination
{
    // tg: code/modules/hallucination/inhand_fake_item.dm:12
    public override async DmTask<object> start()
    {
        if (IS_UNCONSCIOUS(hallucinator))
            return FALSE;

        DmList slots_free = Dm.list();
        if (Dm.truthy(Dm.bitand(valid_slots, ITEM_SLOT_HANDS)))
        {
            foreach (object hand in Dm.each(hallucinator.get_empty_held_indexes()))
                slots_free[datum_hud.ui_hand_position(Dm.num(hand))] = ITEM_SLOT_HANDS;
        }

        // These slots are human only, + they have to have a uniform
        mob_living_carbon_human human_hallucinator = hallucinator as mob_living_carbon_human;
        if (human_hallucinator != null && human_hallucinator.w_uniform != null)
        {
            if (Dm.truthy(Dm.bitand(valid_slots, ITEM_SLOT_BELT)) && human_hallucinator.belt == null)
                slots_free[ui_belt] = ITEM_SLOT_BELT;
            if (Dm.truthy(Dm.bitand(valid_slots, ITEM_SLOT_LPOCKET)) && human_hallucinator.l_store == null)
                slots_free[ui_storage1] = ITEM_SLOT_LPOCKET;
            if (Dm.truthy(Dm.bitand(valid_slots, ITEM_SLOT_RPOCKET)) && human_hallucinator.r_store == null)
                slots_free[ui_storage2] = ITEM_SLOT_RPOCKET;
        }

        if (length(slots_free) == 0)
            return FALSE;

        object picked_space = pick(slots_free);
        obj_item_hallucinated hallucinated_item = make_fake_item(picked_space, slots_free[picked_space]) as obj_item_hallucinated;
        feedback_details += Dm.text("Item Type: []", hallucinated_item.name);

        if (hallucinator.client != null)
            Dm.append(hallucinator.client.screen, hallucinated_item);
        QDEL_IN_HALLUCINATION(this, rand(15 * SECONDS, 35 * SECONDS), "code/modules/hallucination/inhand_fake_item.dm", 41);
        return TRUE;
    }

    // tg: code/modules/hallucination/inhand_fake_item.dm:44
    public virtual object make_fake_item(object where_to_put_it = null, object equip_flags = null)
    {
        obj_item_hallucinated hallucinated_item = Dm.@new<obj_item_hallucinated>(hallucinator, this);
        hallucinated_item.screen_loc = Dm.str(where_to_put_it);

        hallucinated_item.name = Dm.initial(template_item_type, (obj_item p) => p.name);
        hallucinated_item.desc = Dm.initial(template_item_type, (obj_item p) => p.desc);
        hallucinated_item.icon = Dm.initial(template_item_type, (obj_item p) => p.icon);
        hallucinated_item.icon_state = Dm.initial(template_item_type, (obj_item p) => p.icon_state);
        hallucinated_item.update_weight_class(Dm.initial(template_item_type, (obj_item p) => p.w_class)); // Not strictly necessary, but keen eyed people will notice

        return hallucinated_item;
    }
}

// tg: code/modules/hallucination/inhand_fake_item.dm:57
public partial class datum_hallucination_fake_item_c4 : datum_hallucination_fake_item
{
    // tg: code/modules/hallucination/inhand_fake_item.dm:60
    public override object make_fake_item(object where_to_put_it = null, object equip_flags = null)
    {
        if (prob(50))
            template_item_type = typeof(obj_item_grenade_c4_x4);
        return base.make_fake_item(where_to_put_it, equip_flags);
    }
}

// tg: code/modules/hallucination/inhand_fake_item.dm:70
public partial class datum_hallucination_fake_item_esword : datum_hallucination_fake_item
{
    // tg: code/modules/hallucination/inhand_fake_item.dm:74
    public override object make_fake_item(object where_to_put_it = null, object equip_flags = null)
    {
        // Make the item via parent call
        obj_item_hallucinated hallucinated_item = base.make_fake_item(where_to_put_it, equip_flags) as obj_item_hallucinated;

        // If we were placed in our mob's hands there's a 40% chance to make it appear active
        if (Dm.truthy(Dm.bitand(equip_flags, ITEM_SLOT_HANDS)) && prob(40))
        {
            Type sabre_color = pick(subtypesof(typeof(obj_item_melee_energy_sword_saber))) as Type;
            // Yes this can break if someone changes esword icon stuff
            hallucinated_item.icon_state = Dm.text("[]_on_[]", hallucinated_item.icon_state, Dm.initial(sabre_color, (obj_item_melee_energy_sword_saber p) => p.sword_color_icon));
            hallucinator.playsound_local(get_turf(hallucinator), Dm.resource("sound/items/weapons/saberon.ogg"), 35, TRUE);
        }

        return hallucinated_item;
    }
}

// tg: code/modules/hallucination/inhand_fake_item.dm:93
public partial class datum_hallucination_fake_item_baton : datum_hallucination_fake_item
{
    // tg: code/modules/hallucination/inhand_fake_item.dm:97
    public override object make_fake_item(object where_to_put_it = null, object equip_flags = null)
    {
        obj_item_hallucinated hallucinated_item = base.make_fake_item(where_to_put_it, equip_flags) as obj_item_hallucinated;

        // If we were placed in our mob's hands there's a 30% chance to make it appear active
        if (Dm.truthy(Dm.bitand(equip_flags, ITEM_SLOT_HANDS)) && prob(30))
        {
            // Yes this can break if someone changes baton icon stuff
            hallucinated_item.icon_state = Dm.text("[]_active", hallucinated_item.icon_state);
            hallucinator.playsound_local(get_turf(hallucinator), SFX_SPARKS, 75, TRUE, -1);
        }

        return hallucinated_item;
    }
}

// tg: code/modules/hallucination/inhand_fake_item.dm:109
public partial class datum_hallucination_fake_item_emag : datum_hallucination_fake_item
{
    // tg: code/modules/hallucination/inhand_fake_item.dm:113
    public override object make_fake_item(object where_to_put_it = null, object equip_flags = null)
    {
        if (prob(50))
            template_item_type = typeof(obj_item_card_emag_doorjack);
        return base.make_fake_item(where_to_put_it, equip_flags);
    }
}

// tg: code/modules/hallucination/inhand_fake_item.dm:118
public partial class datum_hallucination_fake_item_flashbang : datum_hallucination_fake_item
{
    // tg: code/modules/hallucination/inhand_fake_item.dm:122
    public override object make_fake_item(object where_to_put_it = null, object equip_flags = null)
    {
        obj_item_hallucinated hallucinated_item = base.make_fake_item(where_to_put_it, equip_flags) as obj_item_hallucinated;
        if (prob(15))
        {
            // Yes this can break if someone changse grenade icon stuff
            hallucinated_item.icon_state = Dm.text("[]_active", hallucinated_item.icon_state);
            hallucinator.playsound_local(get_turf(hallucinator), Dm.resource("sound/items/weapons/armbomb.ogg"), 60, TRUE);
            to_chat(hallucinator, span_warning(Dm.text("You prime []! 5 seconds!", hallucinated_item)));
        }

        return hallucinated_item;
    }
}

// tg: code/modules/hallucination/inhand_fake_item.dm:133
public partial class datum_hallucination_fake_item_summon_guns : datum_hallucination_fake_item
{
    // tg: code/modules/hallucination/inhand_fake_item.dm:137
    public override object make_fake_item(object where_to_put_it = null, object equip_flags = null)
    {
        template_item_type = pick(GLOB.summoned_guns) as Type;
        object dot = base.make_fake_item(where_to_put_it, equip_flags);
        hallucinator.playsound_local(get_turf(hallucinator), Dm.resource("sound/effects/magic/summon_guns.ogg"), 50, TRUE);
        return dot;
    }
}

// tg: code/modules/hallucination/inhand_fake_item.dm:142
public partial class datum_hallucination_fake_item_summon_magic : datum_hallucination_fake_item
{
    // tg: code/modules/hallucination/inhand_fake_item.dm:146
    public override object make_fake_item(object where_to_put_it = null, object equip_flags = null)
    {
        template_item_type = pick(Dm.add(GLOB.summoned_magic, GLOB.summoned_special_magic)) as Type;
        object dot = base.make_fake_item(where_to_put_it, equip_flags);
        hallucinator.playsound_local(get_turf(hallucinator), Dm.resource("sound/effects/magic/summon_magic.ogg"), 50, TRUE);
        return dot;
    }
}

// tg: code/modules/hallucination/inhand_fake_item.dm:151
public partial class obj_item_hallucinated : obj_item
{
    // tg: code/modules/hallucination/inhand_fake_item.dm:155
    [DmParams("mapload", "parent")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        datum_hallucination parent = Dm.arg<datum_hallucination>(__extra, 0);
        object dot = null;
        dot = base.Initialize(mapload, Dm.forward(__extra, parent));
        if (parent == null)
        {
            stack_trace(Dm.text("[] was created without a parent hallucination.", type), "code/modules/hallucination/inhand_fake_item.dm", 157);
            return INITIALIZE_HINT_QDEL;
        }

        RegisterSignal(parent, COMSIG_QDELETING, nameof(parent_deleting));
        this.parent = parent;

        add_traits(Dm.list(TRAIT_NODROP, TRAIT_EXAMINE_SKIP), INNATE_TRAIT);
        return dot;
    }

    // tg: code/modules/hallucination/inhand_fake_item.dm:164
    public override object Destroy(float? force = null)
    {
        UnregisterSignal(parent, COMSIG_QDELETING);
        parent = null;
        return base.Destroy(force);
    }

    /// Signal proc for [COMSIG_QDELETING], if our associated hallucination deletes, we should too
    // tg: code/modules/hallucination/inhand_fake_item.dm:170
    [SignalHandler]
    public virtual void parent_deleting(datum source = null)
    {
        qdel(this);
    }
}
