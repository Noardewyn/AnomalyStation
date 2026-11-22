// Translated from tgstation (snapshot 1d99bd7f): code/modules/spells/spell_types/teleport/_teleport.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps R3 (D327): the whole file: the teleport spells (to a random turf near the caster, or into a chosen or random area), which
// Santa's teleport and the wizard's teleport and blink build on. The var blocks are generated (D111).

/**
 * ## Teleport Spell
 *
 * Teleports the caster to a turf selected by get_destinations().
 */
// tg: code/modules/spells/spell_types/teleport/_teleport.dm:7
public partial class datum_action_cooldown_spell_teleport : datum_action_cooldown_spell
{
    // tg: code/modules/spells/spell_types/teleport/_teleport.dm:21
    public override async DmTask<object> cast(atom cast_on = null)
    {
        object dot = await base.cast(cast_on);
        DmList destinations = await get_destinations(cast_on) as DmList;
        if (Dm.length(destinations) == 0)
            throw Dm.CRASH(Dm.text("[] failed to find a teleport destination.", GetType()), "code/modules/spells/spell_types/teleport/_teleport.dm", 25);

        await do_teleport(cast_on as atom_movable, pick(destinations) as atom, asoundout: post_teleport_sound, channel: teleport_channel, forced: force_teleport);
        return dot;
    }

    /// Gets a list of destinations that are valid
    // tg: code/modules/spells/spell_types/teleport/_teleport.dm:30
    public virtual async DmTask<object> get_destinations(atom center = null)
    {
        await DmTask.CompletedTask;
        throw Dm.CRASH(Dm.text("[] did not implement get_destinations and either has no effects or implemented the spell incorrectly.", GetType()), "code/modules/spells/spell_types/teleport/_teleport.dm", 31);
    }

    /// Checks if the passed turf is a valid destination.
    // tg: code/modules/spells/spell_types/teleport/_teleport.dm:34
    public virtual async DmTask<object> is_valid_destination(turf selected = null)
    {
        if (isspaceturf(selected) && Dm.truthy(Dm.bitand(destination_flags, TELEPORT_SPELL_SKIP_SPACE)))
            return FALSE;
        if (Dm.truthy(selected.density) && Dm.truthy(Dm.bitand(destination_flags, TELEPORT_SPELL_SKIP_DENSE)))
            return FALSE;
        if (Dm.truthy(await selected.is_blocked_turf(exclude_mobs: TRUE)) && Dm.truthy(Dm.bitand(destination_flags, TELEPORT_SPELL_SKIP_BLOCKED)))
            return FALSE;

        return TRUE;
    }
}

/**
 * ### Radius Teleport Spell
 *
 * A subtype of teleport that will teleport the caster
 * to a random turf within a radius of themselves.
 */
// tg: code/modules/spells/spell_types/teleport/_teleport.dm:50
public partial class datum_action_cooldown_spell_teleport_radius_turf : datum_action_cooldown_spell_teleport
{
    // tg: code/modules/spells/spell_types/teleport/_teleport.dm:56
    public override async DmTask<object> get_destinations(atom center = null)
    {
        DmList valid_turfs = Dm.list();
        DmList possibles = RANGE_TURFS(outer_tele_radius, center);
        if (inner_tele_radius > 0)
            Dm.remove(ref possibles, RANGE_TURFS(inner_tele_radius, center));

        foreach (turf nearby_turf in Dm.each_cast<turf>(possibles))
        {
            if (!Dm.truthy(await is_valid_destination(nearby_turf)))
                continue;

            Dm.append(ref valid_turfs, nearby_turf);
        }

        // If there are valid turfs around us?
        // Screw it, allow 'em to teleport to ANY nearby turf.
        return Dm.length(valid_turfs) > 0 ? valid_turfs : possibles;
    }

    // tg: code/modules/spells/spell_types/teleport/_teleport.dm:72
    public override async DmTask<object> is_valid_destination(turf selected = null)
    {
        object dot = await base.is_valid_destination(selected);
        if (!Dm.truthy(dot))
            return FALSE;

        // putting them at the edge is dumb
        if (selected.x > world.maxx - outer_tele_radius || selected.x < outer_tele_radius)
            return FALSE;
        if (selected.y > world.maxy - outer_tele_radius || selected.y < outer_tele_radius)
            return FALSE;

        return TRUE;
    }
}

/**
 * ### Area Teleport Spell
 *
 * A subtype of teleport that will teleport the caster
 * to a random turf within a selected (or random) area.
 */
// tg: code/modules/spells/spell_types/teleport/_teleport.dm:91
public partial class datum_action_cooldown_spell_teleport_area_teleport : datum_action_cooldown_spell_teleport
{
    // tg: code/modules/spells/spell_types/teleport/_teleport.dm:101
    public override async DmTask<object> get_destinations(atom center = null)
    {
        DmList valid_turfs = Dm.list();
        foreach (turf possible_destination in Dm.each_cast<turf>(get_area_turfs(GLOB.teleportlocs[last_chosen_area_name])))
        {
            if (!Dm.truthy(await is_valid_destination(possible_destination)))
                continue;

            Dm.append(ref valid_turfs, possible_destination);
        }

        return valid_turfs;
    }

    // tg: code/modules/spells/spell_types/teleport/_teleport.dm:111
    public override async DmTask<object> before_cast(atom cast_on = null)
    {
        object dot = await base.before_cast(cast_on);
        if (Dm.truthy(Dm.bitand(dot, SPELL_CANCEL_CAST)))
            return dot;

        object target_area;
        if (Dm.truthy(randomise_selection))
            target_area = pick(GLOB.teleportlocs);
        else
            target_area = await tgui_input_list(cast_on, I18n.T("Chose an area to teleport to."), I18n.T("Teleport"), GLOB.teleportlocs);

        if (QDELETED(this) || QDELETED(cast_on) || (owner != null && !Dm.truthy(await can_cast_spell())))
            return Dm.bitor(dot, SPELL_CANCEL_CAST);
        if (!Dm.truthy(target_area) || GLOB.teleportlocs[target_area] == null)
            return Dm.bitor(dot, SPELL_CANCEL_CAST);

        last_chosen_area_name = target_area;
        return dot;
    }

    // tg: code/modules/spells/spell_types/teleport/_teleport.dm:129
    public override async DmTask<object> cast(atom cast_on = null)
    {
        if (isliving(cast_on))
        {
            var living_cast_on = (mob_living)cast_on;
            if (living_cast_on.buckled != null)
                await living_cast_on.buckled.unbuckle_mob(living_cast_on, force: TRUE);
        }
        return await base.cast(cast_on);
    }

    // tg: code/modules/spells/spell_types/teleport/_teleport.dm:135
    public override async DmTask invocation__proc(mob_living invoker = null)
    {
        var last_chosen_area = GLOB.teleportlocs[last_chosen_area_name] as area;

        if (!Dm.truthy(invocation_says_area) || last_chosen_area == null)
        {
            await base.invocation__proc(invoker);
            return;
        }

        if (Dm.eq(invocation_type, INVOCATION_SHOUT))
            await invoker.say(Dm.text("[], []!", invocation, uppertext(last_chosen_area.name)), forced: Dm.text("spell ([])", this));
        else if (Dm.eq(invocation_type, INVOCATION_WHISPER))
            await invoker.whisper(Dm.text("[], [].", invocation, uppertext(last_chosen_area.name)), forced: Dm.text("spell ([])", this));
    }
}
