// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/tameable.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C2).

///This component lets you make specific mobs tameable by feeding them
// tg: code/datums/components/tameable.dm:4
public partial class datum_component_tameable : datum_component
{
    // tg: code/datums/components/tameable.dm:12
    [DmParams("food_types", "tame_chance", "bonus_tame_chance", "unique")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object food_types = Dm.arg(__extra, 0);
        object tame_chance = Dm.arg(__extra, 1);
        object bonus_tame_chance = Dm.arg(__extra, 2);
        object unique = Dm.arg(__extra, 3) ?? TRUE;
        if (!isatom(parent)) //yes, you could make a tameable toolbox.
            return COMPONENT_INCOMPATIBLE;

        if (Dm.truthy(tame_chance))
        {
            this.tame_chance = tame_chance;
            this.current_tame_chance = tame_chance;
        }
        if (Dm.truthy(bonus_tame_chance))
            this.bonus_tame_chance = bonus_tame_chance;
        this.unique = unique;

        if (Dm.truthy(food_types) && !HAS_TRAIT(parent, TRAIT_MOB_EATER))
            await parent.AddElement(typeof(datum_element_basic_eating), null, null, null, null, null, null, food_types);

        RegisterSignal(parent, COMSIG_MOB_ATE, nameof(try_tame));
        RegisterSignal(parent, COMSIG_SIMPLEMOB_SENTIENCEPOTION, nameof(on_tame)); //Instantly succeeds
        RegisterSignal(parent, COMSIG_SIMPLEMOB_TRANSFERPOTION, nameof(on_tame)); //Instantly succeeds
        return null;
    }

    // tg: code/datums/components/tameable.dm:30
    [SignalHandler]
    public virtual void try_tame(atom source = null, obj_item food = null, mob_living attacker = null)
    {
        if (attacker == null || Dm.truthy(already_friends(attacker)))
            return;

        object inform_tamer = FALSE;
        float modified_tame_chance = Dm.num(current_tame_chance);
        if (HAS_TRAIT(attacker, TRAIT_BEAST_EMPATHY))
        {
            modified_tame_chance += 50;
            inform_tamer = TRUE;
        }

        source.balloon_alert(attacker, "eats from your hand");
        if (prob(modified_tame_chance)) //note: lack of feedback message is deliberate, keep them guessing unless they're an expert!
            on_tame(source, attacker, food, inform_tamer);
        else
            current_tame_chance = Dm.num(current_tame_chance) + Dm.num(bonus_tame_chance);
    }

    /// Check if the passed mob is already considered one of our friends
    // tg: code/datums/components/tameable.dm:49
    public virtual object already_friends(mob_living potential_friend = null)
    {
        if (!isliving(parent))
            return FALSE; // Figure this out when we actually need it
        mob_living living_parent = parent as mob_living;
        return living_parent.has_ally(potential_friend);
    }

    ///Ran once taming succeeds
    // tg: code/datums/components/tameable.dm:56
    [SignalHandler]
    public virtual void on_tame(atom source = null, mob_living tamer = null, obj_item food = null, object inform_tamer = null)
    {
        inform_tamer ??= FALSE;
        Waiting.Detach(source.tamed(tamer, food)); //Run custom behavior if needed
        if (isliving(parent))
        {
            mob_living living_parent = parent as mob_living;
            (living_parent.ai_controller as datum_ai_controller)?.set_blackboard_key(BB_TAMED, TRUE);
        }
        if (isliving(parent) && isliving(tamer))
        {
            Waiting.Detach(((mob_living)source).befriend(tamer));
            if (Dm.truthy(inform_tamer))
                source.balloon_alert(tamer, "tamed");
        }

        if (HAS_TRAIT(tamer, TRAIT_BEAST_EMPATHY))
            Waiting.Detach(rename_pet(source as mob_living, tamer));
        if (Dm.truthy(unique))
            qdel(this);
        else
            current_tame_chance = tame_chance;
    }

    // tg: code/datums/components/tameable.dm:74
    public virtual async DmTask rename_pet(mob_living animal = null, mob_living tamer = null)
    {
        object chosen_name = await sanitize_name(await tgui_input_text(tamer, "Choose your pet's name!", "Name pet", animal.name, MAX_NAME_LEN), allow_numbers: TRUE);
        if (QDELETED(animal) || Dm.eq(chosen_name, animal.name))
            return;
        if (!Dm.truthy(chosen_name))
        {
            to_chat(tamer, span_warning("Please enter a valid name."));
            await rename_pet(animal, tamer);
            return;
        }
        await animal.fully_replace_character_name(animal.name, chosen_name);
    }
}
