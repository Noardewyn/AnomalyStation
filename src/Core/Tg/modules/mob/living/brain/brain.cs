// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/brain/brain.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M6 slice 5, gibbing: a gibbed player's brain keeps their mind in tg's brain mob, transfer_identity): the brain mob
// whole but the suicide's procs (send_applicable_messages, get_visible_suicide_message, apply_suicide_damage: the suicide
// verb, M10), blob_act (blobs, M10+: the atom's blob_act is not declared), the mouse pointer of a brain in a mech (mechs, M10+)
// and the policy keywords (the server's policy config, M10).
// The var block is generated (D111); the [DmPath] is in _onclick/other_mobs.cs (rules 2.3).

// tg: code/modules/mob/living/brain/brain.dm:1
public partial class mob_living_brain : mob_living
{
    // tg: code/modules/mob/living/brain/brain.dm:9
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        create_dna();
        stored_dna.initialize_dna(random_human_blood_type());
        if (isturf(loc)) //not spawned in an MMI or brain organ (most likely adminspawned)
        {
            obj_item_organ_brain OB = Dm.@new<obj_item_organ_brain>(loc); //we create a new brain organ for it.
            OB.brainmob = this;
            Waiting.NoSleep(forceMove(OB));
        }
        ADD_TRAIT(this, TRAIT_NEVER_CONSIDERED_ALIVE, INNATE_TRAIT);
        return dot;
    }

    // tg: code/modules/mob/living/brain/brain.dm:19
    public override async DmTask on_changed_z_level(turf old_turf = null, turf new_turf = null, object same_z_layer = null, float? notify_contents = null)
    {
        obj_item_organ_brain brain_loc = loc as obj_item_organ_brain;
        if (brain_loc != null && new_turf == null && brain_loc.owner != null) //we're actively being put inside a new body.
        {
            await base.on_changed_z_level(old_turf, get_turf(brain_loc.owner), same_z_layer, notify_contents);
            return;
        }
        await base.on_changed_z_level(old_turf, new_turf, same_z_layer, notify_contents);
    }

    // tg: code/modules/mob/living/brain/brain.dm:25
    public virtual void create_dna()
    {
        stored_dna = Dm.@new<datum_dna_stored>(this);
        if (!Dm.truthy(stored_dna.species))
        {
            object rando_race = pick(get_selectable_species());
            stored_dna.species = Dm.@new((Type)rando_race);
        }
    }

    // tg: code/modules/mob/living/brain/brain.dm:31
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(key)) //If there is a mob connected to this thing. Have to check key twice to avoid false death reporting.
        {
            if (stat != DEAD)
                Waiting.NoSleep(death(TRUE));
            if (mind != null) //You aren't allowed to return to brains that don't exist
                mind.set_current(null);
            ghostize(); //Ghostize checks for key so nothing else is necessary.
        }
        container = null;
        QDEL_NULL(ref stored_dna);
        return base.Destroy(force);
    }

    //you cant blow up brainmobs because it makes transfer_to() freak out when borgs blow up.
    // tg: code/modules/mob/living/brain/brain.dm:52
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        return FALSE;
    }

    //no eyes
    // tg: code/modules/mob/living/brain/brain.dm:58
    public override object get_eye_protection()
    {
        return FLASH_PROTECTION_WELDER;
    }

    // tg: code/modules/mob/living/brain/brain.dm:61
    public override object get_ear_protection(float? ignore_deafness = null)
    {
        return Dm.add(base.get_ear_protection(ignore_deafness), EAR_PROTECTION_HEAVY);
    }

    // no eyes, no flashing
    // tg: code/modules/mob/living/brain/brain.dm:64
    public override async DmTask<object> flash_act(float? intensity = null, float? override_blindness_check = null, float? affect_silicon = null, float? visual = null, Type type = null, float? length = null)
    {
        return null;
    }

    // tg: code/modules/mob/living/brain/brain.dm:67
    public override object can_be_revived()
    {
        if (container == null || health <= dead_threshold)
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/mob/living/brain/brain.dm:72
    public override async DmTask<object> fully_replace_character_name(object oldname = null, object newname = null, float? log_new_name = null)
    {
        object dot = await base.fully_replace_character_name(oldname, newname, log_new_name);
        if (!Dm.truthy(dot))
            return dot;

        if (stored_dna != null)
            stored_dna.real_name = real_name;
        return dot;
    }

    // tg: code/modules/mob/living/brain/brain.dm:79
    public override async DmTask<object> forceMove(atom destination = null, params object[] __extra)
    {
        // Let's see if our destination is good first
        if (Dm.truthy(is_type_in_list(destination, TgState.Current.mob_living_brain__forceMove__safe_destinations)))
            return await doMove(destination);

        // if not, let's try to move what's containing us instead
        if (container != null)
            return await container.forceMove(destination);
        else if (loc is obj_item_organ_brain brain_loc)
            return await brain_loc.forceMove(destination);
        else
            throw Dm.CRASH(Dm.text("Brainmob without a container [] attempted to move to [].", this, destination), "code/modules/mob/living/brain/brain.dm", 92);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/brain/brain.dm:81  var/static/list/safe_destinations = list(/obj/item/organ/brain, /obj/item/brain_processor)
    internal DmList mob_living_brain__forceMove__safe_destinations = Dm.list(typeof(obj_item_organ_brain), typeof(obj_item_brain_processor));
}
