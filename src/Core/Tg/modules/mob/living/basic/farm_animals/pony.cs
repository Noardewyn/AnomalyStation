// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/farm_animals/pony.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

// tg: code/modules/mob/living/basic/farm_animals/pony.dm:2
public partial class mob_living_basic_pony : mob_living_basic
{
    // tg: code/modules/mob/living/basic/farm_animals/pony.dm:47
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        apply_colour();

        Waiting.NoSleep(AddElement(typeof(datum_element_pet_bonus), "whicker"));
        Waiting.NoSleep(AddElement(typeof(datum_element_ai_retaliate)));
        Waiting.NoSleep(AddElement(typeof(datum_element_ai_flee_while_injured)));
        Waiting.NoSleep(AddElementTrait(TRAIT_WADDLING, INNATE_TRAIT, typeof(datum_element_waddling)));

        DmList food_types = TgState.Current.mob_living_basic_pony__food_types ??= Dm.list(typeof(obj_item_food_grown_apple));
        Waiting.NoSleep(AddComponent(typeof(datum_component_tameable), food_types, 25f, 15f, unique_tamer));
        return dot;
    }

    // tg: code/modules/mob/living/basic/farm_animals/pony.dm:62
    public override async DmTask tamed(mob_living tamer = null, obj_item food = null, params object[] __extra)
    {
        await base.tamed(tamer, food, __extra);
        playsound(this, Dm.resource("sound/mobs/non-humanoids/pony/snort.ogg"), 50);
        await AddElement(typeof(datum_element_ridable), typeof(datum_component_riding_creature_pony));
        visible_message(span_notice(Dm.text("[] snorts happily.", this)));
        Dm.@new<obj_effect_temp_visual_heart>(loc);

        DmList tamed_emotes = TgState.Current.mob_living_basic_pony__tamed_emotes ??= Dm.list(
            Dm.kv(BB_EMOTE_HEAR, Dm.list("snorts.")),
            Dm.kv(BB_EMOTE_SEE, Dm.list("snorts.")),
            Dm.kv(BB_EMOTE_SOUND, Dm.list(Dm.resource("sound/mobs/non-humanoids/pony/snort.ogg"))),
            Dm.kv(BB_SPEAK_CHANCE, 3f));

        (ai_controller as datum_ai_controller)?.override_blackboard_key(BB_BASIC_MOB_SPEAK_LINES, tamed_emotes);

        if (Dm.truthy(unique_tamer))
        {
            my_owner = WEAKREF(tamer) as datum_weakref;
            RegisterSignal(this, COMSIG_MOVABLE_PREBUCKLE, nameof(on_prebuckle));
        }
    }

    // tg: code/modules/mob/living/basic/farm_animals/pony.dm:83
    public override object Destroy(float? force = null)
    {
        UnregisterSignal(this, COMSIG_MOVABLE_PREBUCKLE);
        my_owner = null;
        return base.Destroy(force);
    }

    /// Only let us get ridden if the buckler is our owner, if we have a unique owner.
    // tg: code/modules/mob/living/basic/farm_animals/pony.dm:89
    [SignalHandler]
    public virtual object on_prebuckle(mob source = null, mob_living buckler = null, object force = null, object buckle_mob_flags = null)
    {
        mob_living tamer = my_owner?.resolve() as mob_living;
        if (!Dm.truthy(unique_tamer) || (tamer == null && Dm.truthy(unique_tamer)))
            return null;
        if (buckler != tamer)
        {
            whinny_angrily();
            return COMPONENT_BLOCK_BUCKLE;
        }
        return null;
    }

    // tg: code/modules/mob/living/basic/farm_animals/pony.dm:98
    public virtual void apply_colour()
    {
        if (greyscale_config == null)
            return;
        Waiting.NoSleep(set_greyscale(colors: ponycolors));
    }

    // tg: code/modules/mob/living/basic/farm_animals/pony.dm:103
    public virtual void whinny_angrily()
    {
        manual_emote("whinnies ANGRILY!");
        playsound(this, pick(Dm.list(
            Dm.resource("sound/mobs/non-humanoids/pony/whinny01.ogg"),
            Dm.resource("sound/mobs/non-humanoids/pony/whinny02.ogg"),
            Dm.resource("sound/mobs/non-humanoids/pony/whinny03.ogg")
        )), 50);
    }

    // tg: code/modules/mob/living/basic/farm_animals/pony.dm:112
    public override async DmTask<object> take_damage(object damage_amount = null, string damage_type = null, object damage_flag = null, float? sound_effect = null, object attack_dir = null, float? armour_penetration = null)
    {
        object dot = await base.take_damage(damage_amount, damage_type, damage_flag, sound_effect, attack_dir, armour_penetration);
        if (prob(33))
            whinny_angrily();
        return dot;
    }

    // tg: code/modules/mob/living/basic/farm_animals/pony.dm:117
    public override async DmTask<object> melee_attack(atom target = null, DmList modifiers = null, float? ignore_cooldown = null)
    {
        object dot = await base.melee_attack(target, modifiers, ignore_cooldown);
        if (!Dm.truthy(dot))
            return dot;
        whinny_angrily();
        return dot;
    }

    // tg: code/modules/mob/living/basic/farm_animals/pony.dm:123
    public override object get_shove_flags(mob_living shover = null, obj_item weapon = null)
    {
        object dot = base.get_shove_flags(shover, weapon);
        return Dm.bitor(dot, SHOVE_CAN_STAGGER);
    }
}

// Pony subtypes (syndicate and dangerous share one body in tg's two files of procs).
// tg: code/modules/mob/living/basic/farm_animals/pony.dm:144
public partial class mob_living_basic_pony_syndicate : mob_living_basic_pony
{
    // tg: code/modules/mob/living/basic/farm_animals/pony.dm:154
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        DmList mane_colors = Dm.list(
            Dm.kv(COLOR_RED, 6f),
            Dm.kv(COLOR_BLUE, 6f),
            Dm.kv(COLOR_PINK, 3f),
            Dm.kv(COLOR_GREEN, 3f),
            Dm.kv(COLOR_BLACK, 3f),
            Dm.kv(COLOR_YELLOW, 2f),
            Dm.kv(COLOR_ORANGE, 1f),
            Dm.kv(COLOR_WHITE, 1f),
            Dm.kv(COLOR_DARK_BROWN, 1f));
        ponycolors = Dm.list("#5d566f", pick_weight(mane_colors));
        name = pick("Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday") as string;
        DmList food_types = TgState.Current.mob_living_basic_pony_syndicate__food_types ??= Dm.list(typeof(obj_item_food_grown_apple));
        Waiting.NoSleep(AddComponent(typeof(datum_component_tameable), food_types, 100f, 15f, unique_tamer));
        return dot;
    }
}

// tg: code/modules/mob/living/basic/farm_animals/pony.dm:176
public partial class mob_living_basic_pony_dangerous : mob_living_basic_pony
{
    // tg: code/modules/mob/living/basic/farm_animals/pony.dm:189
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        DmList mane_colors = Dm.list(COLOR_RED, COLOR_ORANGE, COLOR_YELLOW);
        ponycolors = Dm.list("#666666", pick(mane_colors));
        name = pick("S-Horse", "Plotva", "Horsekk", "Agro", "Hons") as string;
        DmList food_types = TgState.Current.mob_living_basic_pony_dangerous__food_types ??= Dm.list(typeof(obj_item_food_grown_apple));
        Waiting.NoSleep(AddComponent(typeof(datum_component_tameable), food_types, 100f, 15f, unique_tamer));
        return dot;
    }
}

public sealed partial class TgState
{
    internal DmList mob_living_basic_pony__food_types;
    internal DmList mob_living_basic_pony__tamed_emotes;
    internal DmList mob_living_basic_pony_syndicate__food_types;
    internal DmList mob_living_basic_pony_dangerous__food_types;
}
