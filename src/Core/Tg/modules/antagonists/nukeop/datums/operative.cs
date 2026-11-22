// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/nukeop/datums/operative.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N1b, D316) but the leaf of the callous personality (the personalities, M10+). The var block is generated (D111).

// tg: code/modules/antagonists/nukeop/datums/operative.dm:1
public partial class datum_antagonist_nukeop : datum_antagonist
{
    // tg: code/modules/antagonists/nukeop/datums/operative.dm:40
    public override async DmTask greet()
    {
        play_stinger();
        to_chat(owner, span_big(I18n.T("You are a [] agent!", nuke_team != null ? nuke_team.syndicate_name : "syndicate")));
        await owner.announce_objectives();
    }

    // tg: code/modules/antagonists/nukeop/datums/operative.dm:45
    public override async DmTask on_gain()
    {
        await give_alias();
        await forge_objectives();
        // owner.current.add_personality(/datum/personality/callous): the personalities (status_procs.dm:541) are not translated (M10+)
        Dm.Deferred("code/modules/antagonists/nukeop/datums/operative.dm:48", "on_gain: add_personality(/datum/personality/callous), the personalities (M10+)");
        await base.on_gain();
        owner.set_assigned_role(SSjob.get_job_type(job_type) as datum_job);
        await equip_op();
        if (Dm.truthy(send_to_spawnpoint))
            await move_to_spawnpoint();
        if (Dm.truthy(give_bonus_tc))
        {
            float extra_tc = CEILING(Dm.num(GLOB.joined_player_list.len) / 5, 5);
            var bonus_uplink = owner.find_syndicate_uplink() as datum_component_uplink;
            bonus_uplink?.uplink_handler.add_telecrystals(extra_tc);
        }

        var uplink = owner.find_syndicate_uplink() as datum_component_uplink;
        if (uplink != null)
        {
            var team = get_team() as datum_team_nuclear;
            if (!Dm.truthy(team.team_discounts))
            {
                DmList uplink_items = Dm.list();
                foreach (datum_uplink_item item in Dm.each<datum_uplink_item>(SStraitor.uplink_items))
                {
                    // DEVIATION(DV343): no sale of an entry the uplink hides (its item, procs or system are not live)
                    if (uplink_item_hidden(item))
                        continue;
                    if (item.item != null && !Dm.truthy(item.cant_discount) && Dm.truthy(Dm.bitand(item.purchasable_from, uplink.uplink_handler.uplink_flag)) && item.cost > 1)
                        uplink_items.Add(item);
                }
                team.team_discounts = Dm.list();
                Dm.append(ref team.team_discounts, create_uplink_sales(discount_team_amount, typeof(datum_uplink_category_discount_team_gear), -1, uplink_items));
                Dm.append(ref team.team_discounts, create_uplink_sales(discount_limited_amount, typeof(datum_uplink_category_limited_discount_team_gear), 1, uplink_items));
            }
            Dm.append(ref uplink.uplink_handler.extra_purchasable, team.team_discounts);
        }

        if (nuke_team?.tracked_nuke != null && Dm.truthy(nuke_team?.memorized_code))
            memorize_code();
    }

    // tg: code/modules/antagonists/nukeop/datums/operative.dm:75
    public override object get_team()
    {
        return nuke_team;
    }

    // tg: code/modules/antagonists/nukeop/datums/operative.dm:78
    public override DmTask<object> apply_innate_effects(mob_living mob_override = null)
    {
        add_team_hud(mob_override ?? owner.current, typeof(datum_antagonist_nukeop));
        return DmTask<object>.FromResult(null);
    }

    // tg: code/modules/antagonists/nukeop/datums/operative.dm:81
    public override DmTask forge_objectives(params object[] __extra)
    {
        if (nuke_team != null)
            Dm.combine(ref objectives, nuke_team.objectives);
        return DmTask.CompletedTask;
    }

    // tg: code/modules/antagonists/nukeop/datums/operative.dm:88
    public override async DmTask create_team(datum_team new_team = null)
    {
        if (new_team == null)
        {
            // Find the first leader to join up
            foreach (datum_antagonist_nukeop_leader leader in Dm.each<datum_antagonist_nukeop_leader>(GLOB.antagonists))
            {
                if (leader.nuke_team != null)
                {
                    nuke_team = leader.nuke_team;
                    return;
                }
            }
            // Otherwise make a new team entirely
            nuke_team = await Dm.new_async(typeof(datum_team_nuclear)) as datum_team_nuclear;
            return;
        }
        if (new_team is not datum_team_nuclear)
            stack_trace(Dm.text("Wrong team type passed to [] initialization.", type), file: "code/modules/antagonists/nukeop/datums/operative.dm", line: 99);
        nuke_team = new_team as datum_team_nuclear;
    }

    // tg: code/modules/antagonists/nukeop/datums/operative.dm:102
    public override async DmTask admin_add(datum_mind new_owner = null, mob admin = null)
    {
        await new_owner.add_antag_datum(this);
        message_admins(Dm.text("[] has nuke op'ed [].", key_name_admin(admin), key_name_admin(new_owner)));
        log_admin(Dm.text("[] has nuke op'ed [].", key_name(admin), key_name(new_owner)));
    }

    // tg: code/modules/antagonists/nukeop/datums/operative.dm:107
    public override object get_admin_commands()
    {
        DmList dot = base.get_admin_commands() as DmList;
        dot["Send to base"] = Dm.@new<datum_callback>(this, nameof(admin_send_to_base));
        dot["Tell code"] = Dm.@new<datum_callback>(this, nameof(admin_tell_code));
        return dot;
    }

    // tg: code/modules/antagonists/nukeop/datums/operative.dm:112
    public override async DmTask<object> get_preview_icon()
    {
        if (preview_outfit == null)
            return null;

        var final_icon = await render_preview_outfit(preview_outfit) as icon;

        if (preview_outfit_behind != null)
        {
            var teammate = await render_preview_outfit(preview_outfit_behind) as icon;
            teammate.Blend("#80808080", ICON_MULTIPLY);

            final_icon.Blend(teammate, ICON_UNDERLAY, -ICON_SIZE_X / 4, 0);
            final_icon.Blend(teammate, ICON_UNDERLAY, ICON_SIZE_X / 4, 0);
        }

        if (nuke_icon_state != null)
        {
            icon nuke = Dm.icon(Dm.resource("icons/obj/machines/nuke.dmi"), nuke_icon_state);
            nuke.Shift(SOUTH, 6);
            final_icon.Blend(nuke, ICON_OVERLAY);
        }

        return finish_preview_icon(final_icon);
    }

    // tg: code/modules/antagonists/nukeop/datums/operative.dm:132
    public virtual async DmTask<object> equip_op()
    {
        if (!ishuman(owner.current))
            return null;

        var operative = (mob_living_carbon_human)owner.current;
        ADD_TRAIT(operative, TRAIT_NOFEAR_HOLDUPS, INNATE_TRAIT);

        if (nukeop_outfit == null) // this variable is null in instances where an antagonist datum is granted via enslaving the mind (/datum/mind/proc/enslave_mind_to_creator), like in golems.
            return null;

        // If our nuke_ops_species pref is set to TRUE, (or we have no client) make us a human
        if (operative.client == null || Dm.truthy(operative.client.prefs.read_preference(typeof(datum_preference_toggle_nuke_ops_species))))
            operative.set_species(typeof(datum_species_human));

        await operative.equip_species_outfit(nukeop_outfit);

        return TRUE;
    }

    // tg: code/modules/antagonists/nukeop/datums/operative.dm:150
    public virtual async DmTask admin_send_to_base(mob admin = null)
    {
        await owner.current.forceMove(pick(GLOB.nukeop_base_start) as atom);
    }

    // tg: code/modules/antagonists/nukeop/datums/operative.dm:153
    public virtual void admin_tell_code(mob admin = null)
    {
        object code = null;
        foreach (obj_machinery_nuclearbomb bombue in Dm.each<obj_machinery_nuclearbomb>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_nuclearbomb))))
        {
            if (length(bombue.r_code) <= 5 && bombue.r_code != Dm.initial(bombue, (obj_machinery_nuclearbomb b) => b.r_code))
            {
                code = bombue.r_code;
                break;
            }
        }
        if (Dm.truthy(code))
        {
            antag_memory = Dm.str(antag_memory) + Dm.text("<B>Syndicate Nuclear Bomb Code</B>: []<br>", code);
            to_chat(owner.current, I18n.T("The nuclear authorization code is: <B>[]</B>", code));
        }
        else
            to_chat(admin, span_danger(I18n.T("No valid nuke found!")));
    }

    // tg: code/modules/antagonists/nukeop/datums/operative.dm:165
    public virtual async DmTask give_alias()
    {
        if (Dm.truthy(nuke_team?.syndicate_name))
        {
            if (owner.current is mob_living_carbon_human human_to_rename) // Reinforcements get a real name
            {
                object first_name = owner.current.client?.prefs?.read_preference(typeof(datum_preference_name_operative_alias));
                if (!Dm.truthy(first_name))
                    first_name = pick(GLOB.operative_aliases);
                string chosen_name = Dm.text("[] []", first_name, nuke_team.syndicate_name);
                await human_to_rename.fully_replace_character_name(null, chosen_name);
            }
            else
            {
                float number = nuke_team?.members.Find(owner) ?? 1;
                if (number == 0)
                    number = 1;
                await owner.current.fully_replace_character_name(null, Dm.text("[] Operative #[]", nuke_team.syndicate_name, number));
            }
        }
    }

    // tg: code/modules/antagonists/nukeop/datums/operative.dm:176
    public virtual void memorize_code()
    {
        antag_memory = Dm.str(antag_memory) + Dm.text("<B>[] Code</B>: []<br>", nuke_team.tracked_nuke, nuke_team.memorized_code);
        owner.add_memory(typeof(datum_memory_key_nuke_code), Dm.named("nuclear_code", nuke_team.memorized_code));
        to_chat(owner, I18n.T("The nuclear authorization code is: <B>[]</B>", nuke_team.memorized_code));
    }

    /// Actually moves our nukie to where they should be
    // tg: code/modules/antagonists/nukeop/datums/operative.dm:182
    public virtual async DmTask move_to_spawnpoint()
    {
        // Ensure that the nukiebase is loaded, and wait for it if required
        await SSmapping.lazy_load_template(LAZY_TEMPLATE_KEY_NUKIEBASE);
        var destination = get_spawnpoint() as turf;
        await owner.current.forceMove(destination);
        if (!Dm.truthy(owner.current.onSyndieBase()))
        {
            message_admins(Dm.text("[] is a NUKE OP and move_to_spawnpoint put them somewhere that isn't the syndie base, help please.", ADMIN_LOOKUPFLW(owner.current)));
            stack_trace(Dm.text("Nuke op move_to_spawnpoint resulted in a location not on the syndicate base. (Was moved to: [])", destination),
                file: "code/modules/antagonists/nukeop/datums/operative.dm", line: 189);
        }
    }

    /// Gets the position we spawn at
    // tg: code/modules/antagonists/nukeop/datums/operative.dm:192
    public virtual object get_spawnpoint()
    {
        float team_number = 1;
        if (nuke_team != null)
            team_number = nuke_team.members.Find(owner);

        return GLOB.nukeop_base_start[((team_number - 1) % GLOB.nukeop_base_start.len) + 1];
    }

    // tg: code/modules/antagonists/nukeop/datums/operative.dm:199
    public virtual async DmTask spawn_infiltrator()
    {
        var ship = SSmapping.shuttle_templates[infiltrator_id] as datum_map_template_shuttle_infiltrator;
        float x = world.maxx - TRANSITIONEDGE - ship.width;
        float y = world.maxy - TRANSITIONEDGE - ship.height;
        float z = 0;
        if (SSmapping.empty_space != null)
            z = SSmapping.empty_space.z_value;
        else
        {
            foreach (datum_space_level z_level in Dm.each<datum_space_level>(SSmapping.z_list))
            {
                if (z_level.traits.Find(ZTRAIT_RESERVED) != 0)
                {
                    z = z_level.z_value;
                    break;
                }
            }
        }

        var turf = locate(x, y, z) as turf;

        if (turf == null)
        {
            stack_trace("Infiltrator found no turf to load in", file: "code/modules/antagonists/nukeop/datums/operative.dm", line: 215);
            return;
        }

        if (!Dm.truthy(await ship.load(turf)))
        {
            stack_trace("Infiltrator has failed to load!", file: "code/modules/antagonists/nukeop/datums/operative.dm", line: 218);
            return;
        }

        var mobile_port = SSshuttle.getShuttle("syndicate") as obj_docking_port_mobile;
        mobile_port.destination = SSshuttle.getDock("syndicate_away") as obj_docking_port_stationary;
        mobile_port.mode = SHUTTLE_IGNITING;
        mobile_port.setTimer(mobile_port.ignitionTime);
    }

    // tg: code/modules/antagonists/nukeop/datums/operative.dm:225
    public override async DmTask<object> on_respawn(mob new_character = null)
    {
        await new_character.forceMove(pick(GLOB.nukeop_base_start) as atom);
        await equip_op();
        return TRUE;
    }
}
