// Translated from tgstation (snapshot 1d99bd7f): code/_onclick/hud/human.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the human HUD's screen objects (M2 P3: the walk/run button and the health displays; M4a P2, D126: the hands, swap,
// drop, the inventory toggle, the zone selector, resist, throw and the combat toggle; pulling, 2026-10-15: the pull button; gaps package G4: the memories button; gaps S1: the hunger display) and the locked slots (M4a P2); the
// hotkey verb (toggle_hotkey_verbs: verbs, M9) is not translated. The slot datums of the file are generated (var-only).

// tg: code/_onclick/hud/human.dm:1 (the [DmPath] is on hud.cs, hud.dm:401)
public partial class datum_hud_human : datum_hud
{
    // tg: code/_onclick/hud/human.dm:1-2
    public datum_hud_human()
    {
        default_inventory_slots = typeof(datum_inventory_slot_human);
    }

    // tg: code/_onclick/hud/human.dm:4
    public override async DmTask initialize_screen_objects()
    {
        await base.initialize_screen_objects();
        atom_movable_screen using__1 = null;
        // Static elements
        await add_screen_object(typeof(atom_movable_screen_language_menu), HUD_MOB_LANGUAGE_MENU, HUD_GROUP_STATIC, ui_style, ui_human_language); // gaps L2
        // The navigate and area creator buttons (:9-10): their screen types are not translated (navigation M9, area creation M5).
        Dm.Deferred("code/_onclick/hud/human.dm:9", "initialize_screen_objects: the navigate and area creator buttons (M10+)");
        await add_screen_object(typeof(atom_movable_screen_combattoggle_flashy), HUD_MOB_INTENTS, HUD_GROUP_INFO, ui_style);
        // The floor changer (:12): multi-z (after the core version)
        Dm.Deferred("code/_onclick/hud/human.dm:12", "initialize_screen_objects: the floor changer button (multi-z, after the core version)");
        await add_screen_object(typeof(atom_movable_screen_mov_intent), HUD_MOB_MOVE_INTENT, HUD_GROUP_STATIC, ui_style);
        await add_screen_object(typeof(atom_movable_screen_drop), HUD_MOB_DROP, HUD_GROUP_STATIC, ui_style, ui_swaphand_position(mymob, 1));
        await add_screen_object(typeof(atom_movable_screen_human_toggle), HUD_HUMAN_TOGGLE_INVENTORY, HUD_GROUP_STATIC, ui_style);
        // M8z (D234): the rest and sleep buttons
        await add_screen_object(typeof(atom_movable_screen_rest), HUD_MOB_REST, HUD_GROUP_HOTKEYS, ui_style);
        await add_screen_object(typeof(atom_movable_screen_sleep), HUD_MOB_SLEEP, HUD_GROUP_HOTKEYS, ui_style, ui_above_throw);
        await add_screen_object(typeof(atom_movable_screen_pull), HUD_MOB_PULL, HUD_GROUP_STATIC, ui_style, ui_above_movement_top);
        await add_screen_object(typeof(atom_movable_screen_zone_sel), HUD_MOB_ZONE_SELECTOR, HUD_GROUP_STATIC, ui_style);
        await add_screen_object(typeof(atom_movable_screen_memories), HUD_MOB_MEMORIES, HUD_GROUP_STATIC, ui_style, ui_human_memories_menu);
        await build_hand_slots();

        using__1 = await add_screen_object(typeof(atom_movable_screen_swap_hand), HUD_MOB_SWAPHAND_2, HUD_GROUP_STATIC, ui_style, ui_swaphand_position(mymob, 2)) as atom_movable_screen;
        using__1.icon_state = "act_swap";

        // Hotkey buttons
        await add_screen_object(typeof(atom_movable_screen_resist), HUD_MOB_RESIST, HUD_GROUP_HOTKEYS, ui_style);
        await add_screen_object(typeof(atom_movable_screen_throw_catch), HUD_MOB_THROW, HUD_GROUP_HOTKEYS, ui_style);

        // Info
        await add_screen_object(typeof(atom_movable_screen_spacesuit), HUD_MOB_SPACESUIT, HUD_GROUP_INFO);
        await add_screen_object(typeof(atom_movable_screen_healthdoll_human), HUD_MOB_HEALTHDOLL, HUD_GROUP_INFO);
        await add_screen_object(typeof(atom_movable_screen_stamina), HUD_MOB_STAMINA, HUD_GROUP_INFO);
        await add_screen_object(typeof(atom_movable_screen_healths), HUD_MOB_HEALTH, HUD_GROUP_INFO);
        await add_screen_object(typeof(atom_movable_screen_hunger), HUD_MOB_HUNGER, HUD_GROUP_INFO);
    }

    // tg: code/_onclick/hud/human.dm:37
    public override void update_locked_slots()
    {
        if (!Dm.truthy(mymob))
            return;
        float blocked_slots = NONE;

        mob_living_carbon_human human_mob = mymob as mob_living_carbon_human;
        if (human_mob is mob_living_carbon_human)
        {
            blocked_slots = Dm.num(Dm.bitor(blocked_slots, (human_mob.dna?.species as datum_species)?.no_equip_flags));
            if ((human_mob.w_uniform == null || !Dm.truthy(Dm.bitand(human_mob.w_uniform.item_flags, IN_INVENTORY))) && !HAS_TRAIT(human_mob, TRAIT_NO_JUMPSUIT))
            {
                obj_item_bodypart chest = human_mob.get_bodypart(BODY_ZONE_CHEST) as obj_item_bodypart;
                if (chest == null || Dm.truthy(IS_ORGANIC_LIMB(chest)))
                    blocked_slots = Dm.bitor(blocked_slots, Dm.bitor(ITEM_SLOT_ID, ITEM_SLOT_BELT));
                obj_item_bodypart left_leg = human_mob.get_bodypart(BODY_ZONE_L_LEG) as obj_item_bodypart;
                if (left_leg == null || Dm.truthy(IS_ORGANIC_LIMB(left_leg)))
                    blocked_slots = Dm.bitor(blocked_slots, ITEM_SLOT_LPOCKET);
                obj_item_bodypart right_leg = human_mob.get_bodypart(BODY_ZONE_R_LEG) as obj_item_bodypart;
                if (right_leg == null || Dm.truthy(IS_ORGANIC_LIMB(right_leg)))
                    blocked_slots = Dm.bitor(blocked_slots, ITEM_SLOT_RPOCKET);
            }
            if (human_mob.wear_suit == null || !Dm.truthy(Dm.bitand(human_mob.wear_suit.item_flags, IN_INVENTORY)))
                blocked_slots = Dm.bitor(blocked_slots, ITEM_SLOT_SUITSTORE);
            if (Dm.num(human_mob.num_hands) <= 0)
                blocked_slots = Dm.bitor(blocked_slots, ITEM_SLOT_GLOVES);
            if (Dm.num(human_mob.num_legs) < 2) // update this when you can wear shoes on one foot
                blocked_slots = Dm.bitor(blocked_slots, ITEM_SLOT_FEET);
            obj_item_bodypart_head head = human_mob.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart_head;
            if (head == null)
                blocked_slots = Dm.bitor(blocked_slots, Dm.bitor(Dm.bitor(ITEM_SLOT_HEAD, ITEM_SLOT_EARS), Dm.bitor(ITEM_SLOT_EYES, ITEM_SLOT_MASK)));
            obj_item_organ_eyes eyes = human_mob.get_organ_slot(ORGAN_SLOT_EYES) as obj_item_organ_eyes;
            if (Dm.truthy(eyes?.no_glasses))
                blocked_slots = Dm.bitor(blocked_slots, ITEM_SLOT_EYES);
        }

        foreach (object slot_key in Dm.each(screen_objects))
        {
            atom_movable_screen_inventory inv = screen_objects[slot_key] as atom_movable_screen_inventory;
            if (!(inv is atom_movable_screen_inventory) || !Dm.truthy(inv.slot_id))
                continue;
            inv.alpha = Dm.truthy(Dm.bitand(blocked_slots, inv.slot_id)) ? 128 : Dm.num(Dm.initial(inv, (atom p) => p.alpha));
        }
    }
}

// tg: code/_onclick/hud/human.dm:74
public partial class mob_living_carbon_human : mob_living_carbon
{
    // tg: code/_onclick/hud/human.dm:74
    public virtual void __gvb_toggle_hotkey_verbs(DmList structured_args = null)
    {
        Dm.NotTranslated("code/_onclick/hud/human.dm:74", "__gvb_toggle_hotkey_verbs: M2 P3 (skeleton)");
    }

    // tg: code/_onclick/hud/human.dm:74
    public virtual void toggle_hotkey_verbs(params object[] __extra)
    {
        Dm.NotTranslated("code/_onclick/hud/human.dm:74", "toggle_hotkey_verbs: M2 P3 (skeleton)");
    }
}

// tg: code/_onclick/hud/human.dm:83
[DmPath("/datum/inventory_slot/human")]
public partial class datum_inventory_slot_human : datum_inventory_slot
{
}
