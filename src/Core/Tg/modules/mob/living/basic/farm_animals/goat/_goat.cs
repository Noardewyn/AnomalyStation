// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/farm_animals/goat/_goat.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

//goat
// tg: code/modules/mob/living/basic/farm_animals/goat/_goat.dm:3
public partial class mob_living_basic_goat : mob_living_basic
{
    /// List of stuff (mobs) that we want to eat
    // tg: code/modules/mob/living/basic/farm_animals/goat/_goat.dm:41
    private static DmList edibles => TgState.Current.mob_living_basic_goat__edibles ??= Dm.list(typeof(obj_structure_alien_resin_flower_bud), typeof(obj_structure_glowshroom), typeof(obj_structure_spacevine));

    // tg: code/modules/mob/living/basic/farm_animals/goat/_goat.dm:47
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        add_udder();
        Waiting.NoSleep(AddElement(typeof(datum_element_cliff_walking))); //we walk the cliff
        Waiting.NoSleep(AddElement(typeof(datum_element_footstep), FOOTSTEP_MOB_SHOE));
        Waiting.NoSleep(AddElement(typeof(datum_element_ai_retaliate)));
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_GOAT, CELL_VIRUS_TABLE_GENERIC_MOB, 1f, 5f));
        RegisterSignal(this, COMSIG_HOSTILE_PRE_ATTACKINGTARGET, nameof(on_pre_attack));
        RegisterSignal(this, COMSIG_ATOM_WAS_ATTACKED, nameof(on_attacked));
        RegisterSignal(this, COMSIG_MOVABLE_PRE_MOVE, nameof(on_move));
        (ai_controller as datum_ai_controller)?.set_blackboard_key(BB_BASIC_FOODS, typecacheof(edibles));
        return dot;
    }

    /// Called when we attack something in order to piece together the intent of the AI/user and provide desired behavior. The element might be okay here but I'd rather the fluff.
    /// Goats are really good at beating up plants by taking bites out of them, but we use the default attack for everything else
    // tg: code/modules/mob/living/basic/farm_animals/goat/_goat.dm:61
    public virtual object on_pre_attack(datum source = null, atom target = null)
    {
        if (Dm.truthy(is_type_in_list(target, edibles)))
        {
            Waiting.Detach(eat_plant(Dm.list(target)));
            return COMPONENT_HOSTILE_NO_ATTACK;
        }

        if (!isliving(target))
            return null;

        mob_living living_target = target as mob_living;
        if (!Dm.truthy(Dm.bitand(living_target.mob_biotypes, MOB_PLANT)))
            return null;

        Waiting.NoSleep(living_target.adjust_brute_loss(20));
        playsound(this, Dm.resource("sound/items/eatfood.ogg"), rand(30, 50), TRUE);
        obj_item_bodypart edible_bodypart = null;

        if (ishuman(living_target))
        {
            mob_living_carbon_human plant_man = target as mob_living_carbon_human;
            edible_bodypart = pick(plant_man.get_bodyparts()) as obj_item_bodypart;
            Waiting.Detach(edible_bodypart.dismember());
        }

        living_target.visible_message(
            span_warning(Dm.text("[] takes a big chomp out of []!", this, living_target)),
            span_userdanger(Dm.text("[] takes a big chomp out of your []!", this, edible_bodypart ?? (object)"body"))
        );

        return COMPONENT_HOSTILE_NO_ATTACK;
    }

    /// If we are being attacked, get an evil-looking gleam in our eye
    // tg: code/modules/mob/living/basic/farm_animals/goat/_goat.dm:93
    public virtual void on_attacked(datum source = null, atom attacker = null, object attack_flags = null)
    {
        if (!(gleam_cooldown <= world.time))
            return;

        visible_message(span_danger(Dm.text("[] gets an evil-looking gleam in [] eye.", this, p_their())));
        gleam_cooldown = world.time + gleam_delay;
    }

    /// If we are moving onto a turf with edible plants, eat them
    // tg: code/modules/mob/living/basic/farm_animals/goat/_goat.dm:103
    [SignalHandler]
    public virtual void on_move(datum source = null, atom entering_loc = null)
    {
        if (!isturf(entering_loc) || stat == DEAD)
            return;

        DmList edible_plants = Dm.list();
        foreach (obj target in Dm.each<obj>(entering_loc))
        {
            if (Dm.truthy(is_type_in_list(target, edibles)))
                Dm.append(ref edible_plants, target);
        }

        Waiting.Detach(eat_plant(edible_plants));
    }

    /// Proc that handles dealing with the various types of plants we might eat. Assumes that a valid list of type(s) will be passed in.
    // tg: code/modules/mob/living/basic/farm_animals/goat/_goat.dm:119
    public virtual void add_udder()
    {
        if (gender == MALE)
            return;
        Waiting.NoSleep(AddComponent(typeof(datum_component_udder)));
    }

    // tg: code/modules/mob/living/basic/farm_animals/goat/_goat.dm:125
    public virtual async DmTask eat_plant(DmList plants = null)
    {
        bool eaten = false;

        foreach (object target_object in Dm.each(plants))
        {
            atom target = target_object as atom;
            if (target is obj_structure_spacevine vine)
            {
                await vine.eat(this);
                eaten = true;
            }

            if (target is obj_structure_alien_resin_flower_bud)
            {
                await target.take_damage(rand(30, 50), BRUTE, 0);
                eaten = true;
            }

            if (target is obj_structure_glowshroom)
            {
                qdel(target);
                eaten = true;
            }
        }

        if (eaten && prob(10))
        {
            await say("Nom"); // bon appetit
            playsound(this, Dm.resource("sound/items/eatfood.ogg"), rand(30, 50), TRUE);
        }
    }
}

public sealed partial class TgState
{
    internal DmList mob_living_basic_goat__edibles;
}
