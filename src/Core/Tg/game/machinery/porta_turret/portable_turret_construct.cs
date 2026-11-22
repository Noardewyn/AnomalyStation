// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/porta_turret/portable_turret_construct.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G4d, D316): the turret frame built step by step into a portable turret; its attack_ai (the AI cannot
// use it) since silicons SI2. The var block is generated (D111).

// tg: code/game/machinery/porta_turret/portable_turret_construct.dm:11
public partial class obj_machinery_porta_turret_construct : obj_machinery
{
    // tg: code/game/machinery/porta_turret/portable_turret_construct.dm:1-9 (file-local defines)
    private const float PTURRET_UNSECURED = 0;
    private const float PTURRET_BOLTED = 1;
    private const float PTURRET_START_INTERNAL_ARMOUR = 2;
    private const float PTURRET_INTERNAL_ARMOUR_ON = 3;
    private const float PTURRET_GUN_EQUIPPED = 4;
    private const float PTURRET_SENSORS_ON = 5;
    private const float PTURRET_CLOSED = 6;
    private const float PTURRET_START_EXTERNAL_ARMOUR = 7;
    private const float PTURRET_EXTERNAL_ARMOUR_ON = 8;

    // tg: code/game/machinery/porta_turret/portable_turret_construct.dm:25
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        switch (build_step)
        {
            case PTURRET_UNSECURED:
                Dm.append(dot, span_notice(I18n.T("The external bolts are <b>unwrenched</b>, and the frame could be <i>pried</i> apart.")));
                break;
            case PTURRET_BOLTED:
                Dm.append(dot, span_notice(I18n.T("The frame requires <b>metal</b> for its internal armor, the external bolts are <i>wrenched</i> in place.")));
                break;
            case PTURRET_START_INTERNAL_ARMOUR:
                Dm.append(dot, span_notice(I18n.T("The turret's armor needs to be <b>bolted</b> in place, the armor looked like it could be <i>welded</i> out.")));
                break;
            case PTURRET_INTERNAL_ARMOUR_ON:
                Dm.append(dot, span_notice(I18n.T("The turret requires an <b>energy based gun</b> to function, the armor is secured by <i>bolts</i>.")));
                break;
            case PTURRET_GUN_EQUIPPED:
                Dm.append(dot, span_notice(I18n.T("The turret requires an <b>proximity sensor</b> to function. The energy gun could <i>be removed</i>.")));
                break;
            case PTURRET_SENSORS_ON:
                Dm.append(dot, span_notice(I18n.T("The turret's access hatch is <b>unscrewed</b>. The proximity sensor could <i>be removed</i>.")));
                break;
            case PTURRET_CLOSED:
                Dm.append(dot, span_notice(I18n.T("The turret requires <b>metal</b> for its external armor, the access hatch could be <i>unscrewed</i>.")));
                break;
            case PTURRET_START_EXTERNAL_ARMOUR:
                Dm.append(dot, span_notice(I18n.T("The turret's armor needs to be <b>welded</b> in place, the armor looks like it could be <i>pried</i> off.")));
                break;
        }
        return dot;
    }

    // tg: code/game/machinery/porta_turret/portable_turret_construct.dm:45
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        //this is a bit unwieldy but self-explanatory
        switch (build_step)
        {
            case PTURRET_BOLTED:
            {
                if (!istype(tool, typeof(obj_item_stack_sheet_iron)))
                    return NONE;
                obj_item_stack_sheet_iron sheet = tool as obj_item_stack_sheet_iron;
                if (!Dm.truthy(await sheet.use(2)))
                {
                    to_chat(user, span_warning(I18n.T("You need two sheets of iron to continue construction!")));
                    return ITEM_INTERACT_BLOCKING;
                }
                to_chat(user, span_notice(I18n.T("You add some metal armor to the interior frame.")));
                build_step = PTURRET_START_INTERNAL_ARMOUR;
                icon_state = "turret_frame2";
                return ITEM_INTERACT_SUCCESS;
            }

            case PTURRET_INTERNAL_ARMOUR_ON:
            {
                if (!istype(tool, typeof(obj_item_gun_energy))) //the gun installation part
                    return NONE;
                obj_item_gun_energy egun = tool as obj_item_gun_energy;
                if (Dm.truthy(Dm.bitand(egun.gun_flags, TURRET_INCOMPATIBLE)))
                {
                    to_chat(user, span_notice(I18n.T("You don't think it would be right to add [] to the turret", tool)));
                    return ITEM_INTERACT_BLOCKING;
                }
                if (!Dm.truthy(await user.transferItemToLoc(egun, this)))
                    return ITEM_INTERACT_BLOCKING;
                installed_gun = egun;
                to_chat(user, span_notice(I18n.T("You add [] to the turret.", tool)));
                build_step = PTURRET_GUN_EQUIPPED;
                return ITEM_INTERACT_SUCCESS;
            }

            case PTURRET_GUN_EQUIPPED:
            {
                if (!istype(tool, typeof(obj_item_assembly_prox_sensor))) // isprox (__DEFINES/is_helpers.dm), inline
                    return NONE;
                build_step = PTURRET_SENSORS_ON;
                if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(tool)))
                    return ITEM_INTERACT_BLOCKING;
                to_chat(user, span_notice(I18n.T("You add the proximity sensor to the turret.")));
                qdel(tool);
                return ITEM_INTERACT_SUCCESS;
            }

            case PTURRET_CLOSED:
            {
                if (!istype(tool, typeof(obj_item_stack_sheet_iron)))
                    return NONE;
                obj_item_stack_sheet_iron sheet = tool as obj_item_stack_sheet_iron;
                if (!Dm.truthy(await sheet.use(2)))
                {
                    to_chat(user, span_warning(I18n.T("You need two sheets of iron to continue construction!")));
                    return ITEM_INTERACT_BLOCKING;
                }
                to_chat(user, span_notice(I18n.T("You add some metal armor to the exterior frame.")));
                build_step = PTURRET_START_EXTERNAL_ARMOUR;
                return ITEM_INTERACT_SUCCESS;
            }
        }

        return NONE;
    }

    // tg: code/game/machinery/porta_turret/portable_turret_construct.dm:99
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        switch (build_step)
        {
            case PTURRET_UNSECURED:
                tool.play_tool_sound(this, 100);
                to_chat(user, span_notice(I18n.T("You secure the external bolts.")));
                await set_anchored(TRUE);
                build_step = PTURRET_BOLTED;
                return ITEM_INTERACT_SUCCESS;

            case PTURRET_BOLTED:
                tool.play_tool_sound(this, 75);
                to_chat(user, span_notice(I18n.T("You unfasten the external bolts.")));
                await set_anchored(FALSE);
                build_step = PTURRET_UNSECURED;
                return ITEM_INTERACT_SUCCESS;

            case PTURRET_START_INTERNAL_ARMOUR:
                tool.play_tool_sound(this, 100);
                to_chat(user, span_notice(I18n.T("You bolt the metal armor into place.")));
                build_step = PTURRET_INTERNAL_ARMOUR_ON;
                return ITEM_INTERACT_SUCCESS;

            case PTURRET_INTERNAL_ARMOUR_ON:
                tool.play_tool_sound(this, 100);
                to_chat(user, span_notice(I18n.T("You remove the turret's metal armor bolts.")));
                build_step = PTURRET_START_INTERNAL_ARMOUR;
                return ITEM_INTERACT_SUCCESS;
        }

        return ITEM_INTERACT_SKIP_TO_ATTACK;
    }

    // tg: code/game/machinery/porta_turret/portable_turret_construct.dm:127
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        switch (build_step)
        {
            case PTURRET_UNSECURED:
                tool.play_tool_sound(this, 75);
                to_chat(user, span_notice(I18n.T("You dismantle the turret construction.")));
                Dm.@new<obj_item_stack_sheet_iron>(loc, 5);
                qdel(this);
                return ITEM_INTERACT_SUCCESS;

            case PTURRET_START_EXTERNAL_ARMOUR:
                tool.play_tool_sound(this, 75);
                to_chat(user, span_notice(I18n.T("You pry off the turret's exterior armor.")));
                Dm.@new<obj_item_stack_sheet_iron>(loc, 2);
                build_step = PTURRET_CLOSED;
                return ITEM_INTERACT_SUCCESS;
        }

        return ITEM_INTERACT_SKIP_TO_ATTACK;
    }

    // tg: code/game/machinery/porta_turret/portable_turret_construct.dm:145
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        switch (build_step)
        {
            case PTURRET_START_INTERNAL_ARMOUR:
                if (!Dm.truthy(tool.tool_start_check(user, amount: 5))) //uses up 5 fuel
                    return ITEM_INTERACT_BLOCKING;

                to_chat(user, span_notice(I18n.T("You start to remove the turret's interior metal armor...")));
                if (!Dm.truthy(await tool.use_tool(this, user, 20, volume: 50, amount: 5))) //uses up 5 fuel
                    return ITEM_INTERACT_BLOCKING;
                build_step = PTURRET_BOLTED;
                to_chat(user, span_notice(I18n.T("You remove the turret's interior metal armor.")));
                Dm.@new<obj_item_stack_sheet_iron>(drop_location(), 2);
                return ITEM_INTERACT_SUCCESS;

            case PTURRET_START_EXTERNAL_ARMOUR:
            {
                if (!Dm.truthy(tool.tool_start_check(user, amount: 5)))
                    return ITEM_INTERACT_BLOCKING;

                to_chat(user, span_notice(I18n.T("You begin to weld the turret's armor down...")));
                if (!Dm.truthy(await tool.use_tool(this, user, 30, volume: 50, amount: 5)))
                    return ITEM_INTERACT_BLOCKING;
                build_step = PTURRET_EXTERNAL_ARMOUR_ON;
                to_chat(user, span_notice(I18n.T("You weld the turret's armor down.")));

                //The final step: create a full turret

                obj_machinery_porta_turret turret;
                //fuck lasertag turrets
                if (istype(installed_gun, typeof(obj_item_gun_energy_laser_bluetag)) || istype(installed_gun, typeof(obj_item_gun_energy_laser_redtag)))
                    turret = Dm.@new<obj_machinery_porta_turret_lasertag>(loc);
                else
                    turret = Dm.@new<obj_machinery_porta_turret>(loc);
                turret.name = finish_name;
                turret.installation = installed_gun.type;
                await turret.setup(installed_gun);
                turret.locked = FALSE;
                qdel(this);
                return ITEM_INTERACT_SUCCESS;
            }
        }

        return ITEM_INTERACT_SKIP_TO_ATTACK;
    }

    // tg: code/game/machinery/porta_turret/portable_turret_construct.dm:184
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        switch (build_step)
        {
            case PTURRET_SENSORS_ON:
                tool.play_tool_sound(this, 100);
                build_step = PTURRET_CLOSED;
                to_chat(user, span_notice(I18n.T("You close the internal access hatch.")));
                return ITEM_INTERACT_SUCCESS;

            case PTURRET_CLOSED:
                tool.play_tool_sound(this, 100);
                build_step = PTURRET_SENSORS_ON;
                to_chat(user, span_notice(I18n.T("You open the internal access hatch.")));
                return ITEM_INTERACT_SUCCESS;
        }

        return ITEM_INTERACT_SKIP_TO_ATTACK;
    }

    // tg: code/game/machinery/porta_turret/portable_turret_construct.dm:200
    public override async DmTask<object> nameformat(object input = null, mob_living user = null)
    {
        finish_name = input as string;
        return input;
    }

    // tg: code/game/machinery/porta_turret/portable_turret_construct.dm:204
    public override async DmTask rename_reset()
    {
        finish_name = Dm.initial(this, (obj_machinery_porta_turret_construct p) => p.finish_name);
    }

    // tg: code/game/machinery/porta_turret/portable_turret_construct.dm:207
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        switch (build_step)
        {
            case PTURRET_GUN_EQUIPPED:
                build_step = PTURRET_INTERNAL_ARMOUR_ON;

                await installed_gun.forceMove(loc);
                to_chat(user, span_notice(I18n.T("You remove [] from the turret frame.", installed_gun)));
                installed_gun = null;
                break;

            case PTURRET_SENSORS_ON:
                to_chat(user, span_notice(I18n.T("You remove the prox sensor from the turret frame.")));
                Dm.@new<obj_item_assembly_prox_sensor>(loc);
                build_step = PTURRET_GUN_EQUIPPED;
                break;
        }
        return dot;
    }
}

// The AI's and cyborgs' clicks (silicons SI2, D326)
// tg: code/game/machinery/porta_turret/portable_turret_construct.dm:226
public partial class obj_machinery_porta_turret_construct : obj_machinery
{
    // tg: code/game/machinery/porta_turret/portable_turret_construct.dm:226
    public override async DmTask<object> attack_ai(mob user = null)
    {
        return null;
    }
}
