// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/mob_transformation_simple.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

// tg: code/modules/mob/mob_transformation_simple.dm:5
public partial class mob
{
    //This proc is the most basic of the procs. All it does is make a new mob on the same tile and transfer over the few basic things.
    // tg: code/modules/mob/mob_transformation_simple.dm:5
    public virtual async DmTask<object> change_mob_type(object new_type = null, atom location = null, object new_name = null, object delete_old_mob = null)
    {
        delete_old_mob ??= FALSE;
        if (isnewplayer(this))
        {
            to_chat(usr, span_danger("Cannot convert players who have not entered yet."));
            return null;
        }

        if (!Dm.truthy(new_type))
            new_type = await input("Mob type path:", "Mob type");

        if (new_type is string new_type_text)
            new_type = text2path(new_type_text);

        if (!ispath(new_type))
        {
            to_chat(usr, Dm.text("Invalid type path (new_type = []) in change_mob_type(). Contact a coder.", new_type));
            return null;
        }

        if (ispath(new_type, typeof(mob_dead_new_player)))
        {
            to_chat(usr, span_danger("Cannot convert into a new_player mob type."));
            return null;
        }

        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_PRE_MOB_CHANGED_TYPE]) ? NONE : this._SendSignal(COMSIG_PRE_MOB_CHANGED_TYPE, Dm.list(this))), COMPONENT_BLOCK_MOB_CHANGE)))
            return null;

        return await change_mob_type_unchecked(new_type, location, new_name, delete_old_mob);
    }

    /// Version of [change_mob_type] that does no usr prompting (may send an error message though). Satisfies procs with the SHOULD_NOT_SLEEP restriction
    // tg: code/modules/mob/mob_transformation_simple.dm:31
    public virtual async DmTask<object> change_mob_type_unchecked(object new_type = null, atom location = null, object new_name = null, object delete_old_mob = null)
    {
        delete_old_mob ??= FALSE;
        mob desired_mob;

        if (isturf(location))
            desired_mob = Dm.@new((Type)new_type, location) as mob;
        else
            desired_mob = Dm.@new((Type)new_type, this.loc) as mob;

        if (!ismob(desired_mob))
        {
            to_chat(usr, Dm.text("Type path is not a mob (new_type = []) in change_mob_type(). Contact a coder.", new_type));
            qdel(desired_mob);
            return null;
        }

        if (new_name is string new_name_text)
        {
            desired_mob.name = new_name_text;
            desired_mob.real_name = new_name_text;
        }
        else
        {
            desired_mob.name = this.name;
            desired_mob.real_name = this.real_name;
        }

        if (Dm.truthy(has_dna()) && Dm.truthy(desired_mob.has_dna()))
        {
            mob_living_carbon old_mob = this as mob_living_carbon;
            mob_living_carbon new_mob = desired_mob as mob_living_carbon;
            await old_mob.dna.copy_dna(new_mob.dna, NONE);
            await new_mob.updateappearance(icon_update: TRUE, mutcolor_update: TRUE, mutations_overlay_update: TRUE);
        }
        else if (ishuman(desired_mob) && !HAS_TRAIT(desired_mob, TRAIT_LESSER_HUMANOID))
        {
            mob_living_carbon_human new_human = desired_mob as mob_living_carbon_human;
            if (client != null)
                client.prefs.safe_transfer_prefs_to(new_human);
            new_human.dna.update_dna_identity();
            await new_human.updateappearance(icon_update: TRUE, mutcolor_update: TRUE, mutations_overlay_update: TRUE);
        }

        if (mind != null && isliving(desired_mob) && (!isobserver(this) || mind.current == this || QDELETED(mind.current)))
        {
            if (!Dm.eq(Dm.ckey(mind.key), ckey))
                stack_trace(Dm.text("DEBUG: The bug where mob transfers or transforms sometimes kick unrelated people out of mobs has happened again. mob []([])\\ref[] owned by [] is being changed into a [] but has a mind owned by [].", this, GetType(), this, ckey, new_type, Dm.ckey(mind.key)), "code/modules/mob/mob_transformation_simple.dm", 67);
            await mind.transfer_to(desired_mob, 1); // second argument to force key move to new mob
        }
        else
            desired_mob.PossessByPlayer(key);

        if (Dm.truthy(this._listen_lookup?[COMSIG_MOB_CHANGED_TYPE]))

            this._SendSignal(COMSIG_MOB_CHANGED_TYPE, Dm.list(this, desired_mob));
        if (Dm.truthy(delete_old_mob))
            _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), this), 1, file: "code/modules/mob/mob_transformation_simple.dm", line: 74);
        return desired_mob;
    }
}
