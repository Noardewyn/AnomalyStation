// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/bonfire.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// gaps ST4 (D333): the file whole. The bonfire built from ten logs: lit by a hot item, a burning mob or thing walking in, or a fire,
// while its turf holds 5 moles of oxygen; it burns what stands on it (fire stacks for the living, fire_act for the rest), or grills
// it with a grill on it; a rod adds the stake (a restrained mob buckled 13 pixels up) or the grill; taken apart into its logs by
// hand. Its embers (`/particles/bonfire`) are set as tg sets them; the client does not draw BYOND particle sets yet (M10+
// (particles), not-translated.md 210). The var block is generated (D111).

// tg: code/game/objects/structures/bonfire.dm:10
public partial class obj_structure_bonfire : obj_structure
{
    ///how many fire stacks are applied when you step into a bonfire
    // tg: code/game/objects/structures/bonfire.dm:2
    private const float BONFIRE_FIRE_STACK_STRENGTH = 5;

    // tg: code/game/objects/structures/bonfire.dm:30
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        DmList loc_connections = TgState.Current.obj_structure_bonfire__Initialize__loc_connections ??= Dm.list(
            Dm.kv(COMSIG_ATOM_ENTERED, nameof(on_entered)));
        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), loc_connections));
        burning_loop = Dm.@new<datum_looping_sound_burning>(this);
        return dot;
    }

    // tg: code/game/objects/structures/bonfire.dm:38
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSobj, this);
        QDEL_NULL(ref burning_loop);
        return base.Destroy(force);
    }

    // tg: code/game/objects/structures/bonfire.dm:43
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is obj_item_stack_rods rods && !Dm.truthy(can_buckle) && !Dm.truthy(grill))
        {
            object choice = await tgui_alert(user, I18n.T("What would you like to construct?"), I18n.T("Bonfire"), Dm.list(I18n.T("Stake"), I18n.T("Grill")));
            if (choice == null)
                return ITEM_INTERACT_BLOCKING;
            await rods.use(1);
            if (Dm.eq(choice, I18n.T("Stake")))
            {
                can_buckle = TRUE;
                buckle_requires_restraints = TRUE;
                to_chat(user, span_notice(I18n.T("You add a rod to \\the [].", this)));
                mutable_appearance rod_underlay = mutable_appearance(Dm.resource("icons/obj/service/hydroponics/equipment.dmi"), "bonfire_rod") as mutable_appearance;
                rod_underlay.pixel_z = 16;
                Dm.append(underlays, rod_underlay);
                return ITEM_INTERACT_SUCCESS;
            }
            if (Dm.eq(choice, I18n.T("Grill")))
            {
                grill = TRUE;
                to_chat(user, span_notice(I18n.T("You add a grill to \\the [].", this)));
                add_overlay("bonfire_grill");
                return ITEM_INTERACT_SUCCESS;
            }
        }

        if (Dm.num(tool.get_temperature()) >= FIRE_MINIMUM_TEMPERATURE_TO_EXIST)
        {
            await start_burning();
            return ITEM_INTERACT_SUCCESS;
        }

        if (Dm.truthy(grill))
        {
            if (tool is obj_item_melee_roastingstick)
                return ITEM_INTERACT_BLOCKING;
            if (Dm.truthy(user.combat_mode) || Dm.truthy(Dm.bitand(tool.item_flags, ABSTRACT)))
                return NONE;
            if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(tool)))
                return ITEM_INTERACT_BLOCKING;
            await tool.forceMove(get_turf(this));
            //Center the icon where the user clicked.
            if (!Dm.truthy(LAZYACCESS(modifiers, ICON_X)) || !Dm.truthy(LAZYACCESS(modifiers, ICON_Y)))
                return ITEM_INTERACT_SUCCESS;
            //Clamp it so that the icon never moves more than 16 pixels in either direction (thus leaving the table turf)
            tool.pixel_x = tool.base_pixel_x + Dm.num(clamp(Dm.num(text2num(LAZYACCESS(modifiers, ICON_X))) - 16, -(ICON_SIZE_X * 0.5f), ICON_SIZE_X * 0.5f));
            tool.pixel_y = tool.base_pixel_y + Dm.num(clamp(Dm.num(text2num(LAZYACCESS(modifiers, ICON_Y))) - 16, -(ICON_SIZE_Y * 0.5f), ICON_SIZE_Y * 0.5f));
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/game/objects/structures/bonfire.dm:89
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        if (Dm.truthy(burning))
        {
            to_chat(user, span_warning(I18n.T("You need to extinguish [] before removing the logs!", this)));
            return dot;
        }
        if (!Dm.truthy(has_buckled_mobs()) && Dm.truthy(await do_after(user, 5 * SECONDS, target: this)))
        {
            foreach (obj_item_grown_log bonfire_log in Dm.each<obj_item_grown_log>(contents.Copy()))
            {
                await bonfire_log.forceMove(drop_location() as atom);
                bonfire_log.pixel_x += Dm.num(rand(1, 4));
                bonfire_log.pixel_y += Dm.num(rand(1, 4));
            }
            if (Dm.truthy(can_buckle) || Dm.truthy(grill))
                Dm.@new<obj_item_stack_rods>(loc);
            qdel(this);
            return dot;
        }
        return dot;
    }

    // tg: code/game/objects/structures/bonfire.dm:106
    public virtual object check_oxygen()
    {
        if (loc is turf_open bonfire_turf)
        {
            if (bonfire_turf.air != null && Dm.num(bonfire_turf.air.moles[typeof(datum_gas_oxygen)]) >= 5)
                return TRUE;
        }
        return FALSE;
    }

    // tg: code/game/objects/structures/bonfire.dm:113
    public virtual async DmTask start_burning()
    {
        if (Dm.truthy(burning) || !Dm.truthy(check_oxygen()))
            return;

        burning_loop.start();
        icon_state = burn_icon;
        burning = TRUE;
        await set_light(6);
        await bonfire_burn();
        particles = Dm.@new<particles_bonfire>();
        START_PROCESSING(SSobj, this);
    }

    // tg: code/game/objects/structures/bonfire.dm:125
    public override async DmTask<object> fire_act(object exposed_temperature = null, object exposed_volume = null)
    {
        await start_burning();
        return null;
    }

    // tg: code/game/objects/structures/bonfire.dm:128
    [SignalHandler]
    public virtual void on_entered(datum source = null, atom_movable entered = null)
    {
        if (Dm.truthy(burning))
        {
            if (!Dm.truthy(grill))
                Waiting.NoSleep(bonfire_burn());
            return;
        }

        //Not currently burning, let's see if we can ignite it.
        if (entered is mob_living burning_body)
        {
            if (Dm.truthy(burning_body.on_fire))
            {
                Waiting.NoSleep(start_burning());
                visible_message(span_notice(I18n.T("[] runs over [], starting its fire!", entered, this)));
            }
        }
        else if (Dm.truthy(Dm.bitand(entered.resistance_flags, ON_FIRE)))
        {
            Waiting.NoSleep(start_burning());
            visible_message(span_notice(I18n.T("[]'s fire spreads to [], setting it ablaze!", entered, this)));
        }
    }

    // tg: code/game/objects/structures/bonfire.dm:146
    public virtual async DmTask bonfire_burn(float seconds_per_tick = 2)
    {
        turf current_location = get_turf(this);
        if (!Dm.truthy(grill))
            current_location.hotspot_expose(1000, 250 * seconds_per_tick, 1);
        foreach (atom burn_target in Dm.each<atom>(current_location.contents.Copy()))
        {
            if (burn_target == this)
                continue;
            else if (burn_target is mob_living burn_victim)
            {
                await burn_victim.adjust_fire_stacks(BONFIRE_FIRE_STACK_STRENGTH * 0.5f * seconds_per_tick);
                burn_victim.ignite_mob();
            }
            else
            {
                atom_movable burned_movable = burn_target as atom_movable;
                if (Dm.truthy(grill) && burned_movable is obj_item grilled_item)
                {
                    grilled_item.SEND_SIGNAL(COMSIG_ITEM_GRILL_PROCESS, Dm.list(grilled_item, this, seconds_per_tick)); //Not a big fan, maybe make this use fire_act() in the future.
                    continue;
                }
                await burned_movable.fire_act(1000, 250 * seconds_per_tick);
            }
        }
    }

    // tg: code/game/objects/structures/bonfire.dm:165
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!Dm.truthy(check_oxygen()))
        {
            Waiting.NoSleep(extinguish());
            return null;
        }
        Waiting.NoSleep(bonfire_burn(Dm.num(seconds_per_tick)));
        return null;
    }

    // tg: code/game/objects/structures/bonfire.dm:171
    public override async DmTask<object> extinguish()
    {
        object dot = await base.extinguish();
        if (!Dm.truthy(burning))
            return dot;

        burning_loop.stop();
        icon_state = "bonfire";
        burning = FALSE;
        await set_light(0);
        QDEL_NULL(ref particles);
        STOP_PROCESSING(SSobj, this);
        return dot;
    }

    // tg: code/game/objects/structures/bonfire.dm:183
    public override async DmTask<object> buckle_mob(mob_living M = null, float? force = null, float? check_loc = null, float? buckle_mob_flags = null)
    {
        object dot = await base.buckle_mob(M, force ?? FALSE, check_loc ?? TRUE, buckle_mob_flags);
        if (Dm.truthy(dot))
            M.pixel_y += 13;
        return dot;
    }

    // tg: code/game/objects/structures/bonfire.dm:187
    public override async DmTask<object> unbuckle_mob(mob_living buckled_mob = null, float? force = null, float? can_fall = null)
    {
        object dot = await base.unbuckle_mob(buckled_mob, force ?? FALSE, can_fall ?? TRUE);
        if (Dm.truthy(dot))
            buckled_mob.pixel_y -= 13;
        return dot;
    }
}

// tg: code/game/objects/structures/bonfire.dm:194
public partial class obj_structure_bonfire_dense_prelit : obj_structure_bonfire_dense
{
    // tg: code/game/objects/structures/bonfire.dm:194
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        base.Initialize(mapload, __extra);
        return INITIALIZE_HINT_LATELOAD;
    }

    // tg: code/game/objects/structures/bonfire.dm:198
    public override void LateInitialize(params object[] __extra)
    {
        Waiting.NoSleep(start_burning());
    }
}

// tg: code/game/objects/structures/bonfire.dm:201
public partial class obj_structure_bonfire_prelit : obj_structure_bonfire
{
    // tg: code/game/objects/structures/bonfire.dm:201
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        base.Initialize(mapload, __extra);
        return INITIALIZE_HINT_LATELOAD;
    }

    // Late init so that we can wait for air to exist in lazyloaded templates
    // tg: code/game/objects/structures/bonfire.dm:206
    public override void LateInitialize(params object[] __extra)
    {
        Waiting.NoSleep(start_burning());
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/structures/bonfire.dm:32  /obj/structure/bonfire/Initialize's var/static/list/loc_connections
    internal DmList obj_structure_bonfire__Initialize__loc_connections;
}
