// Translated from tgstation (snapshot 1d99bd7f): code/modules/mapfluff/ruins/lavalandruin_code/elephantgraveyard.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps J6 (D298): the grave crate and the shovel's fresh grave (lines 133-315). Gaps OD3 (D333): the rest of the file, the Lavaland ruin's
// parts: the bone statues (their see-through fade is DV499), the wasteland's cracked earth and dry rock (on the strong rock of
// minerals.cs), the oil well (a fishing spot) and the filled graves' contents with the lead researcher's. The
// add_context screentip is a live-partial row.

// tg: code/modules/mapfluff/ruins/lavalandruin_code/elephantgraveyard.dm:133
public partial class obj_structure_closet_crate_grave : obj_structure_closet_crate
{
    // tg: code/modules/mapfluff/ruins/lavalandruin_code/elephantgraveyard.dm:173
    public override async DmTask<object> close(atom_movable user = null)
    {
        object dot = await base.close(user);
        // So that graves stay undense
        set_density(FALSE);
        return dot;
    }

    // tg: code/modules/mapfluff/ruins/lavalandruin_code/elephantgraveyard.dm:178
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        Dm.append(ref dot, span_notice(I18n.T("It can be [] with a shovel.", EXAMINE_HINT(Dm.truthy(opened) ? "closed" : "dug open"))));
        return dot;
    }

    // tg: code/modules/mapfluff/ruins/lavalandruin_code/elephantgraveyard.dm:214
    public override object closet_update_overlays(DmList new_overlays = null) => null;

    // tg: code/modules/mapfluff/ruins/lavalandruin_code/elephantgraveyard.dm:217
    public override async DmTask<object> before_open(mob_living user = null, object force = null)
    {
        object dot = await base.before_open(user, force);
        if (!Dm.truthy(dot))
            return FALSE;

        if (!Dm.truthy(force))
        {
            to_chat(user, span_notice(I18n.T("The ground here is too hard to dig up with your bare hands. You'll need a shovel.")));
            return FALSE;
        }

        return TRUE;
    }

    // tg: code/modules/mapfluff/ruins/lavalandruin_code/elephantgraveyard.dm:228
    public override object before_close(mob_living user = null)
    {
        object dot = base.before_close(user);
        if (!Dm.truthy(dot))
            return FALSE;

        if (!Dm.truthy(dug_closed))
        {
            to_chat(user, span_notice(I18n.T("You'll need a shovel to cover it up.")));
            return FALSE;
        }

        dug_closed = FALSE;
        return TRUE;
    }

    // tg: code/modules/mapfluff/ruins/lavalandruin_code/elephantgraveyard.dm:240
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        //anything that isn't a shovel does normal stuff to the grave[like putting stuff in]
        if (!Dm.eq(tool.tool_behaviour, TOOL_SHOVEL))
            return await base.item_interaction(user, tool, modifiers, __extra);

        //player is attempting to destroy the open grave with a shovel
        if (Dm.truthy(user.combat_mode))
        {
            if (!Dm.truthy(opened))
                return ITEM_INTERACT_BLOCKING;

            user.visible_message(
                span_notice(I18n.T("[] Is attempting to remove [].", user, this)),
                span_notice(I18n.T("You start removing [].", this)));
            if (!Dm.truthy(await tool.use_tool(this, user, delay: 1.5f * SECONDS, volume: 40)) || !Dm.truthy(opened))
                return ITEM_INTERACT_BLOCKING;

            to_chat(user, span_notice(I18n.T("You remove \\the [] completely.", this)));
            await user.add_mood_event("graverobbing", typeof(datum_mood_event_graverobbing));
            await deconstruct(TRUE);
            return ITEM_INTERACT_SUCCESS;
        }

        //player is attempting to open/close the grave with a shovel
        user.visible_message(
            span_notice(I18n.T("[] Is attempting to [] [].", user, Dm.truthy(opened) ? "close" : "dig open", this)),
            span_notice(I18n.T("You start [] [].", Dm.truthy(opened) ? "closing" : "digging open", this)));
        if (!Dm.truthy(await tool.use_tool(this, user, delay: 1.5f * SECONDS, volume: 40)))
            return ITEM_INTERACT_BLOCKING;

        // HAS_PERSONALITY(user, callous/misanthrope): the personalities, not ported; the morbid mind only
        bool is_chill_with_robbing = HAS_MIND_TRAIT(user, TRAIT_MORBID);
        if (Dm.truthy(opened))
        {
            dug_closed = TRUE;
            await close(user);
            return ITEM_INTERACT_SUCCESS;
        }

        if (!Dm.truthy(await open(user, force: TRUE)) || !Dm.truthy(affect_mood))
            return ITEM_INTERACT_SUCCESS;

        await user.add_mood_event("graverobbing", is_chill_with_robbing ? typeof(datum_mood_event_morbid_graverobbing) : typeof(datum_mood_event_graverobbing));
        if (Dm.truthy(lead_tomb) && Dm.truthy(first_open))
        {
            if (is_chill_with_robbing || !Dm.truthy(user is mob_living_carbon carbon_user ? await carbon_user.gain_trauma(typeof(datum_brain_trauma_magic_stalker)) : null))
                to_chat(user, span_notice(I18n.T("Did someone say something? I'm sure it was nothing.")));
            else
                to_chat(user, span_boldwarning(I18n.T("Oh no, no no no, THEY'RE EVERYWHERE! EVERY ONE OF THEM IS EVERYWHERE!")));
            first_open = FALSE;
        }

        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/mapfluff/ruins/lavalandruin_code/elephantgraveyard.dm:291
    public override async DmTask<object> container_resist_act(mob_living user = null, params object[] __extra)
    {
        if (Dm.truthy(opened))
            return null;
        // The player is trying to dig themselves out of an early grave
        user.change_next_special_move(CLICK_CD_BREAKOUT);
        user.visible_message(
            span_warning(I18n.T("[]'s dirt begins to shift and rumble!", this)),
            span_notice(I18n.T("You desperately begin to claw at the dirt around you, trying to force yourself upwards through the soil... (this will take about [].)", DisplayTimeText(breakout_time))),
            span_hear(I18n.T("You hear the sound of shifting dirt from [].", this)));
        if (Dm.truthy(await do_after(user, breakout_time, target: this)))
        {
            if (Dm.truthy(opened))
                return null;
            user.visible_message(
                span_danger(I18n.T("[] emerges from [], scattering dirt everywhere!", user, this)),
                span_notice(I18n.T("You triumphantly surface out of [], scattering dirt all around the grave!", this)));
            bust_open();
        }
        else
        {
            if (user.loc == this)
                to_chat(user, span_warning(I18n.T("You fail to dig yourself out of []!", this)));
        }
        return null;
    }
}

//******Decoration objects
//***Bone statues and giant skeleton parts.
// tg: code/modules/mapfluff/ruins/lavalandruin_code/elephantgraveyard.dm:3  (gaps OD3)
public partial class obj_structure_statue_bone : obj_structure_statue
{
    // tg: code/modules/mapfluff/ruins/lavalandruin_code/elephantgraveyard.dm:12
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        // DEVIATION(DV499): AddComponent(/datum/component/seethrough, SEE_THROUGH_MAP_DEFAULT) fades the colossal bone's big sprite for a
        // top-down viewer standing behind it; the first-person view sees it from its own eye, so it is not added.
        Dm.Deferred("code/modules/mapfluff/ruins/lavalandruin_code/elephantgraveyard.dm:15", "Initialize: AddComponent(/datum/component/seethrough), the bone statue's fade for a top-down viewer behind it (DV499)");
        return dot;
    }
}

//***Wasteland floor and rock turfs here.
// tg: code/modules/mapfluff/ruins/lavalandruin_code/elephantgraveyard.dm:44  (gaps OD3)
public partial class turf_open_misc_asteroid_basalt_wasteland : turf_open_misc_asteroid_basalt
{
    // tg: code/modules/mapfluff/ruins/lavalandruin_code/elephantgraveyard.dm:55
    public override object break_tile()
    {
        return null;
    }

    // tg: code/modules/mapfluff/ruins/lavalandruin_code/elephantgraveyard.dm:58
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (prob(floor_variance))
            icon_state = Dm.text("[][]", base_icon_state, rand(0, 6));
        return dot;
    }
}

// tg: code/modules/mapfluff/ruins/lavalandruin_code/elephantgraveyard.dm:68  (gaps OD3)
public partial class turf_closed_mineral_strong_wasteland : turf_closed_mineral_strong
{
    // tg: code/modules/mapfluff/ruins/lavalandruin_code/elephantgraveyard.dm:78
    public override void drop_ores()
    {
        if (prob(10))
        {
            Dm.@new<obj_item_stack_ore_iron>(this);
            Dm.@new<obj_item_stack_ore_glass>(this);
            Dm.@new<obj_effect_decal_remains_human>(this, 1);
        }
        else
            Dm.@new<obj_item_stack_sheet_bone>(this);
    }
}

//***Oil well puddles.
// tg: code/modules/mapfluff/ruins/lavalandruin_code/elephantgraveyard.dm:90  (gaps OD3)
public partial class obj_structure_sink_oil_well : obj_structure_sink
{
    // tg: code/modules/mapfluff/ruins/lavalandruin_code/elephantgraveyard.dm:98
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        //I'm pretty much aware that, because how oil wells and sinks work, attackby() won't work unless in combat mode.
        //Thankfully, the user can cast the line from a distance.
        Waiting.NoSleep(AddComponent(typeof(datum_component_fishing_spot), typeof(datum_fish_source_oil_well)));
        return dot;
    }

    // tg: code/modules/mapfluff/ruins/lavalandruin_code/elephantgraveyard.dm:104
    public override async DmTask<object> find_and_mount_on_atom(float? mark_for_late_init = null, float? late_init = null)
    {
        ///Oil wells exist indepent of any wall structure
        await DmTask.CompletedTask;
        return FALSE;
    }

    // tg: code/modules/mapfluff/ruins/lavalandruin_code/elephantgraveyard.dm:108
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        flick("puddle-oil-splash", this);
        await ((datum_reagents)reagents).expose(user, TOUCH, 20); //Covers target in 20u of oil.
        to_chat(user, span_notice(I18n.T("You touch the pool of oil, only to get oil all over yourself. It would be wise to wash this off with water.")));
        return null;
    }

    // tg: code/modules/mapfluff/ruins/lavalandruin_code/elephantgraveyard.dm:113
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        //we deconstruct with a shovel
        await DmTask.CompletedTask;
        return NONE;
    }

    // tg: code/modules/mapfluff/ruins/lavalandruin_code/elephantgraveyard.dm:117
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        flick("puddle-oil-splash", this);
        if (Dm.eq(tool.tool_behaviour, TOOL_SHOVEL)) //attempt to deconstruct the puddle with a shovel
        {
            to_chat(user, I18n.T("You fill in the oil well with soil."));
            tool.play_tool_sound(this);
            await deconstruct(TRUE);
            return ITEM_INTERACT_SUCCESS;
        }

        return await base.item_interaction(user, tool, modifiers, __extra);
    }

    // tg: code/modules/mapfluff/ruins/lavalandruin_code/elephantgraveyard.dm:127
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        Dm.@new<obj_effect_decal_cleanable_blood_oil>(loc);
        await DmTask.CompletedTask;
    }
}

// tg: code/modules/mapfluff/ruins/lavalandruin_code/elephantgraveyard.dm:182  (gaps OD3)
public partial class obj_structure_closet_crate_grave_filled : obj_structure_closet_crate_grave
{
    // tg: code/modules/mapfluff/ruins/lavalandruin_code/elephantgraveyard.dm:185
    public override async DmTask PopulateContents(params object[] __extra)  //GRAVEROBBING IS NOW A FEATURE
    {
        await base.PopulateContents(__extra);
        Dm.@new<obj_effect_decal_remains_human>(this);
        switch (rand(1, 8))
        {
            case 1:
                Dm.@new<obj_item_coin_gold>(this);
                Dm.@new<obj_item_storage_wallet>(this);
                break;
            case 2:
                Dm.@new<obj_item_clothing_glasses_meson>(this);
                break;
            case 3:
                Dm.@new<obj_item_coin_silver>(this);
                Dm.@new<obj_item_shovel_spade>(this);
                break;
            case 4:
                Dm.@new<obj_item_book_bible_booze>(this);
                break;
            case 5:
                Dm.@new<obj_item_clothing_neck_stethoscope>(this);
                Dm.@new<obj_item_scalpel>(this);
                Dm.@new<obj_item_hemostat>(this);
                break;
            case 6:
                Dm.@new<obj_item_reagent_containers_cup_beaker>(this);
                Dm.@new<obj_item_clothing_glasses_science>(this);
                break;
            case 7:
                Dm.@new<obj_item_clothing_glasses_sunglasses_big>(this);
                Dm.@new<obj_item_cigarette_rollie>(this);
                break;
            default:
                //empty grave
                return;
        }
    }
}

// tg: code/modules/mapfluff/ruins/lavalandruin_code/elephantgraveyard.dm:321  (gaps OD3)
public partial class obj_structure_closet_crate_grave_filled_lead_researcher : obj_structure_closet_crate_grave_filled
{
    // tg: code/modules/mapfluff/ruins/lavalandruin_code/elephantgraveyard.dm:329
    public override async DmTask PopulateContents(params object[] __extra)  //ADVANCED GRAVEROBBING
    {
        await base.PopulateContents(__extra);
        Dm.@new<obj_effect_decal_cleanable_blood_gibs_old>(this);
        Dm.@new<obj_item_book_granter_crafting_recipe_boneyard_notes>(this);
    }
}
