// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/chem_splash.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps F1: the chem grenades, the gas crystals and a burst reagent tank).

// Replaces chemgrenade stuff, allowing reagent explosions to be called from anywhere.
// It should be called using a location, the range, and a list of reagents involved.

// Threatscale is a multiplier for the 'threat' of the grenade. If you're increasing the affected range drastically, you might want to improve this.
// Extra heat affects the temperature of the mixture, and may cause it to react in different ways.

public static partial class GlobalProcs
{
    /**
     * The basic chemical bomb proc.
     * Combines a set of reagent holders into one holder and reacts it.
     * If there are any reagents left over it spreads them across the surrounding environment.
     * The maximum volume of the holder is temporarily adjusted to allow for reactions which increase total volume to work at full effectiveness.
     * The maximum volume of the holder is then reset to its original value.
     *
     * Arguments:
     * - [epicenter][/turf]: The epicenter of the splash if some of the reagents aren't consumed.
     * - [holder][/datum/reagents]: The holder to combine all of the reagents into. A temporary one is created if this is null.
     * - [reactants][/list/datum/reagents]: The set of reagent holders to combine.
     * - extra_heat: Some amount to heat the combined reagents by before reacting them.
     * - threatscale: A multiplier for the reagent quantities involved.
     * - adminlog: Whether to alert the admins that this has occured.
     */
    // tg: code/modules/reagents/chem_splash.dm:22
    public static async DmTask<object> chem_splash(atom epicenter = null, datum_reagents holder = null, float? affected_range = null, DmList reactants = null, float? extra_heat = null, float? threatscale = null, float? adminlog = null)
    {
        affected_range ??= 3;
        reactants ??= Dm.list();
        extra_heat ??= 0;
        threatscale ??= 1;
        adminlog ??= 1;
        if (!isturf(epicenter) || !Dm.truthy(reactants.len) || threatscale <= 0)
            return null;

        float total_reagents = holder != null ? holder.total_volume : 0;
        float maximum_reagents = holder != null ? holder.maximum_volume : 0;
        foreach (datum_reagents reactant in Dm.each<datum_reagents>(reactants))
        {
            if (Dm.truthy(reactant.total_volume))
                total_reagents += reactant.total_volume;
            maximum_reagents += reactant.maximum_volume;
        }

        if (total_reagents <= 0)
            return FALSE;

        bool tmp_holder;
        float original_max_volume = 0;
        if (holder == null)
        {
            tmp_holder = true;
            holder = Dm.@new<datum_reagents>(maximum_reagents * threatscale);
            holder.my_atom = epicenter;
        }
        else
        {
            tmp_holder = false;
            original_max_volume = holder.maximum_volume;
            if (threatscale < 1)
            {
                await holder.multiply(threatscale);
                holder.maximum_volume = maximum_reagents * Dm.num(threatscale);
            }
            else
            {
                holder.maximum_volume = maximum_reagents * Dm.num(threatscale);
                await holder.multiply(threatscale);
            }
        }

        foreach (datum_reagents reactant in Dm.each_cast<datum_reagents>(reactants))
            await reactant.trans_to(holder, reactant.total_volume, threatscale, no_react: TRUE);

        holder.chem_temp = Dm.max(holder.chem_temp + Dm.num(extra_heat), TCMB); // Average temperature of reagents + extra heat.
        await holder.handle_reactions(); // React them now.

        if (Dm.truthy(holder.total_volume))
        {
            if (affected_range >= 0)
                await spread_reagents(holder, epicenter, affected_range);
            await holder.clear_reagents();
        }

        if (tmp_holder)
            qdel(holder);
        else
            holder.maximum_volume = original_max_volume;

        return TRUE;
    }

    /**
     * Exposes all accessible atoms within some distance of an epicenter to some reagents.
     * Does not clear the source reagent holder; that must be done manually if it is desired.
     *
     * Arguments:
     * - [source][/datum/reagents]: The reagents to spread around.
     * - [epicenter][/atom]: The epicenter/source location of the reagent spread.
     * - spread_range: The range in which to spread the reagents. Will not go over 20
     */
    // tg: code/modules/reagents/chem_splash.dm:80
    public static async DmTask<object> spread_reagents(datum_reagents source = null, atom epicenter = null, float? spread_range = null)
    {
        spread_range = Dm.min(Dm.num(spread_range), 20); // Fuck off with trying to do more then this
        datum_effect_system_basic_steam_spread steam = Dm.@new<datum_effect_system_basic_steam_spread>(epicenter, 10, FALSE);
        await steam.attach(epicenter).start();
        // This is a basic floodfill algorithm of atmos connected tiles
        // Turfs will be stored in the form turf -> TRUE
        float chem_temp = source.chem_temp;
        bool hot_chem = chem_temp >= 300;
        DmList turflist = Dm.list();
        DmList reactable = Dm.list();
        turflist[epicenter] = TRUE;
        for (int i = 1; i <= turflist.len; i++)
        {
            turf valid_step = turflist[i] as turf;
            if (get_dist(valid_step, epicenter) > spread_range) // We are over threshold, don't add anything new and just keep goin
            {
                turflist.Cut(i, i + 1);
                i--;
                continue;
            }

            foreach (turf lad in Dm.each_cast<turf>(valid_step.atmos_adjacent_turfs))
            {
                if (Dm.truthy(turflist[lad]))
                    continue;
                turflist[lad] = TRUE;
            }

            Dm.append(ref reactable, valid_step.get_all_contents()); // Yes this means multitile objects double react. I don't care. skill issue
            if (hot_chem)
                valid_step.hotspot_expose(chem_temp * 2, 5);
        }

        // Remove anything we can't see
        foreach (atom thing in Dm.each_cast<atom>(Dm.bitand((object)dview(spread_range, epicenter), reactable)))
        {
            float distance = Dm.max(1, get_dist(thing, epicenter));
            float fraction = 0.5f / Dm.pow(2, distance); //50/25/12/6... for a 200u splash, 25/12/6/3... for a 100u, 12/6/3/1 for a 50u
            await source.expose(thing, TOUCH, fraction);
        }
        return null;
    }
}
