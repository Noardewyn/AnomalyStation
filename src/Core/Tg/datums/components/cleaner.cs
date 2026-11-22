// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/cleaner.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C3).

/**
 * Component that can be used to clean things.
 * Takes care of duration, cleaning skill and special cleaning interactions.
 * A callback can be set by the datum holding the cleaner to add custom functionality.
 * Soap uses a callback to decrease the amount of uses it has left after cleaning for example.
 */
// tg: code/datums/components/cleaner.dm:7
public partial class datum_component_cleaner : datum_component
{
    // tg: code/datums/components/cleaner.dm:19
    [DmParams("base_cleaning_duration", "skill_duration_modifier_offset", "cleaning_strength", "pre_clean_callback", "on_cleaned_callback")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        this.base_cleaning_duration = Dm.arg(__extra, 0) ?? 3 * SECONDS;
        this.skill_duration_modifier_offset = Dm.arg(__extra, 1) ?? 0;
        this.cleaning_strength = Dm.arg(__extra, 2) ?? CLEAN_SCRUB;
        this.pre_clean_callback = Dm.arg<datum_callback>(__extra, 3);
        this.on_cleaned_callback = Dm.arg<datum_callback>(__extra, 4);
        return null;
    }

    // tg: code/datums/components/cleaner.dm:33
    public override object Destroy(float? force = null)
    {
        pre_clean_callback = null;
        on_cleaned_callback = null;
        return base.Destroy(force);
    }

    // tg: code/datums/components/cleaner.dm:38
    public override async DmTask RegisterWithParent()
    {
        if (ismob(parent))
            RegisterSignal(parent, COMSIG_LIVING_UNARMED_ATTACK, nameof(on_unarmed_attack));
        if (isitem(parent))
            RegisterSignal(parent, COMSIG_ITEM_INTERACTING_WITH_ATOM, nameof(on_interaction));
    }

    // tg: code/datums/components/cleaner.dm:44
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(COMSIG_ITEM_INTERACTING_WITH_ATOM, COMSIG_LIVING_UNARMED_ATTACK));
    }

    /**
     * Handles the COMSIG_LIVING_UNARMED_ATTACK signal used for cleanbots
     * Redirects to afterattack, while setting parent (the bot) as user.
     */
    // tg: code/datums/components/cleaner.dm:54
    [SignalHandler]
    public virtual object on_unarmed_attack(datum source = null, atom target = null, object proximity_flags = null, object modifiers = null)
    {
        if (Dm.truthy(Dm.bitand(on_interaction(source, source as mob_living, target, modifiers as DmList), ITEM_INTERACT_ANY_BLOCKER)))
            return COMPONENT_CANCEL_ATTACK_CHAIN;
        return NONE;
    }

    /**
     * Handles the COMSIG_ITEM_INTERACTING_WITH_ATOM signal by calling the clean proc.
     */
    // tg: code/datums/components/cleaner.dm:64
    [SignalHandler]
    public virtual object on_interaction(datum source = null, mob_living user = null, atom target = null, DmList modifiers = null)
    {
        if (isitem(source) && HAS_TRAIT(target, TRAIT_COMBAT_MODE_SKIP_INTERACTION) && Dm.truthy(user.combat_mode)) // SHOULD_SKIP_INTERACTION (__DEFINES/tools.dm:62), inline
            return NONE;

        object call_wash = TRUE;
        object give_xp = TRUE;
        if (pre_clean_callback != null)
        {
            object callback_return = Waiting.NoSleep(pre_clean_callback.Invoke(source, target, user));
            if (Dm.truthy(Dm.bitand(callback_return, CLEAN_BLOCKED)))
                return Dm.truthy(Dm.bitand(callback_return, CLEAN_DONT_BLOCK_INTERACTION)) ? NONE : ITEM_INTERACT_BLOCKING;
            if (Dm.truthy(Dm.bitand(callback_return, CLEAN_NO_WASH)))
                call_wash = FALSE;
            if (Dm.truthy(Dm.bitand(callback_return, CLEAN_NO_XP)))
                give_xp = FALSE;
        }

        Waiting.Detach(clean(source, target, user, Dm.num(call_wash), Dm.num(give_xp)));
        return ITEM_INTERACT_SUCCESS;
    }

    /**
     * Cleans something using this cleaner.
     * The cleaning duration is modified by the cleaning skill of the user.
     * Successfully cleaning gives cleaning experience to the user and invokes the on_cleaned_callback.
     *
     * Arguments
     * * source the datum that sent the signal to start cleaning
     * * target the thing being cleaned
     * * user the person doing the cleaning
     * * call_wash set this to false if the target should not be wash()ed
     * * grant_xp set this to false if the user should not be granted cleaning experience
     */
    // tg: code/datums/components/cleaner.dm:96
    public virtual async DmTask clean(datum source = null, atom target = null, mob_living user = null, float? call_wash = null, float? grant_xp = null)
    {
        call_wash ??= TRUE;
        grant_xp ??= TRUE;
        //make sure we don't attempt to clean something while it's already being cleaned
        if (HAS_TRAIT(target, TRAIT_CURRENTLY_CLEANING) || Dm.truthy(Dm.bitand(Dm.truthy(target._listen_lookup?[COMSIG_ATOM_PRE_CLEAN]) ? target._SendSignal(COMSIG_ATOM_PRE_CLEAN, Dm.list(target, user)) : NONE, COMSIG_ATOM_CANCEL_CLEAN)))
            return;
        //add the trait and overlay
        ADD_TRAIT(target, TRAIT_CURRENTLY_CLEANING, REF(this));
        // We need to update our planes on overlay changes
        RegisterSignal(target, COMSIG_MOVABLE_Z_CHANGED, nameof(cleaning_target_moved));
        mutable_appearance low_bubble = mutable_appearance(Dm.resource("icons/effects/effects.dmi"), "bubbles", CLEANABLE_OBJECT_LAYER, target, GAME_PLANE) as mutable_appearance;
        mutable_appearance high_bubble = mutable_appearance(Dm.resource("icons/effects/effects.dmi"), "bubbles", CLEANABLE_OBJECT_LAYER, target, ABOVE_GAME_PLANE) as mutable_appearance;
        DmList icon_offsets = target.get_oversized_icon_offsets() as DmList;
        low_bubble.pixel_w = Dm.num(icon_offsets["x"]);
        low_bubble.pixel_z = Dm.num(icon_offsets["y"]);
        high_bubble.pixel_w = Dm.num(icon_offsets["x"]);
        high_bubble.pixel_z = Dm.num(icon_offsets["y"]);
        if (target.plane > low_bubble.plane) //check if the higher overlay is necessary
            target.add_overlay(high_bubble);
        else if (target.plane == low_bubble.plane)
        {
            if (target.layer > low_bubble.layer)
                target.add_overlay(high_bubble);
            else
                target.add_overlay(low_bubble);
        }
        else //(target.plane < low_bubble.plane)
            target.add_overlay(low_bubble);

        //set the cleaning duration
        object cleaning_duration = base_cleaning_duration;
        if (Dm.truthy(user.mind)) //higher cleaning skill can make the duration shorter
        {
            //offsets the multiplier you get from cleaning skill, but doesn't allow the duration to be longer than the base duration
            cleaning_duration = Dm.mul(cleaning_duration, min(Dm.add(user.mind.get_skill_modifier(typeof(datum_skill_cleaning), SKILL_SPEED_MODIFIER), skill_duration_modifier_offset), 1));
        }
        // Assoc list, collects all items being cleaned with its value being any blood on it
        DmList all_cleaned = Dm.list();
        all_cleaned[target] = GET_ATOM_BLOOD_DNA(target) ?? Dm.list();
        //do the cleaning
        object clean_succeeded = FALSE;
        if (Dm.truthy(await do_after(user, Dm.num(cleaning_duration), target: target)))
        {
            clean_succeeded = TRUE;
            foreach (obj_effect_decal_cleanable cleanable_decal in Dm.each<obj_effect_decal_cleanable>(target)) //it's important to do this before you wash all of the cleanables off
            {
                if (Dm.truthy(call_wash) && Dm.truthy(grant_xp))
                    user.mind?.adjust_experience(typeof(datum_skill_cleaning), round(cleanable_decal.beauty / CLEAN_SKILL_BEAUTY_ADJUSTMENT));
                all_cleaned[cleanable_decal] = GET_ATOM_BLOOD_DNA(cleanable_decal);
            }
            if (Dm.truthy(call_wash) && Dm.truthy(await target.wash(cleaning_strength)) && Dm.truthy(grant_xp))
                user.mind?.adjust_experience(typeof(datum_skill_cleaning), round(CLEAN_SKILL_GENERIC_WASH_XP));
        }

        if (on_cleaned_callback != null)
            await on_cleaned_callback.Invoke(source, target, user, clean_succeeded, all_cleaned);
        //remove the cleaning overlay
        target.cut_overlay(low_bubble);
        target.cut_overlay(high_bubble);
        UnregisterSignal(target, COMSIG_MOVABLE_Z_CHANGED);
        REMOVE_TRAIT(target, TRAIT_CURRENTLY_CLEANING, REF(this));
    }

    // tg: code/datums/components/cleaner.dm:144
    [SignalHandler]
    public virtual void cleaning_target_moved(atom_movable source = null, turf old_turf = null, turf new_turf = null, object same_z_layer = null)
    {
        if (Dm.truthy(same_z_layer))
            return;
        // First, get rid of the old overlay
        mutable_appearance old_low_bubble = mutable_appearance(Dm.resource("icons/effects/effects.dmi"), "bubbles", CLEANABLE_OBJECT_LAYER, old_turf, GAME_PLANE) as mutable_appearance;
        mutable_appearance old_high_bubble = mutable_appearance(Dm.resource("icons/effects/effects.dmi"), "bubbles", CLEANABLE_OBJECT_LAYER, old_turf, ABOVE_GAME_PLANE) as mutable_appearance;
        source.cut_overlay(old_low_bubble);
        source.cut_overlay(old_high_bubble);

        // Now, add the new one
        mutable_appearance new_low_bubble = mutable_appearance(Dm.resource("icons/effects/effects.dmi"), "bubbles", CLEANABLE_OBJECT_LAYER, new_turf, GAME_PLANE) as mutable_appearance;
        mutable_appearance new_high_bubble = mutable_appearance(Dm.resource("icons/effects/effects.dmi"), "bubbles", CLEANABLE_OBJECT_LAYER, new_turf, ABOVE_GAME_PLANE) as mutable_appearance;
        source.add_overlay(new_low_bubble);
        source.add_overlay(new_high_bubble);
    }
}
