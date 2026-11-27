// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/revolutionary.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the revolutionary, a humanoid rioter with a random weapon, name, cause and death scream (the bitrunning Meta
// Central's mob segments), its corpse and outfit, and its AI (revolutionary.bt.json). The var blocks are generated.

// tg: code/modules/mob/living/basic/revolutionary.dm:1
public partial class mob_living_basic_revolutionary : mob_living_basic
{
    /// list of weapons we can have
    // tg: code/modules/mob/living/basic/revolutionary.dm:19
    private static DmList possible_weapons => TgState.Current.mob_living_basic_revolutionary__possible_weapons ??= Dm.list(
        Dm.kv(typeof(obj_item_storage_toolbox_mechanical), "robust"),
        Dm.kv(typeof(obj_item_spear), "pierce"),
        Dm.kv(typeof(obj_item_fireaxe), "slice"),
        Dm.kv(typeof(obj_item_melee_baseball_bat), "bat"),
        Dm.kv(typeof(obj_item_melee_baton), "discipline")
    );
    /// List of things to shout
    // tg: code/modules/mob/living/basic/revolutionary.dm:27
    private static DmList phrases => TgState.Current.mob_living_basic_revolutionary__phrases ??= Dm.list(
        "The revolution will not be televized!",
        "VIVA!",
        "Dirty pig!",
        "Gondola meat is murder!",
        "Free Cargonia!",
        "Mime rights are human rights!",
        "猫娘 Free Terry!"
    );
    /// List of causes to #support
    // tg: code/modules/mob/living/basic/revolutionary.dm:37
    private static DmList causes => TgState.Current.mob_living_basic_revolutionary__causes ??= Dm.list(
        "Worker's rights",
        "Icemoon climate change",
        "Fair clown treatment",
        "Lizards",
        "Moths",
        "Stop Lavaland drilling",
        "The Captain has been replaced by a robot",
        "Free Cargonia",
        "Befriend all space dragons",
        "The Grey Tide",
        "Rising cost of medbay"
    );
    /// Monkey screeches
    // tg: code/modules/mob/living/basic/revolutionary.dm:51
    private static DmList monkey_screeches => TgState.Current.mob_living_basic_revolutionary__monkey_screeches ??= Dm.list(
        Dm.resource("sound/mobs/non-humanoids/monkey/monkey_screech_1.ogg"),
        Dm.resource("sound/mobs/non-humanoids/monkey/monkey_screech_2.ogg"),
        Dm.resource("sound/mobs/non-humanoids/monkey/monkey_screech_3.ogg"),
        Dm.resource("sound/mobs/non-humanoids/monkey/monkey_screech_4.ogg")
    );
    /// Male screams
    // tg: code/modules/mob/living/basic/revolutionary.dm:58
    private static DmList male_screams => TgState.Current.mob_living_basic_revolutionary__male_screams ??= Dm.list(
        Dm.resource("sound/mobs/humanoids/human/scream/malescream_1.ogg"),
        Dm.resource("sound/mobs/humanoids/human/scream/malescream_2.ogg"),
        Dm.resource("sound/mobs/humanoids/human/scream/malescream_3.ogg"),
        Dm.resource("sound/mobs/humanoids/human/scream/malescream_4.ogg"),
        Dm.resource("sound/mobs/humanoids/human/scream/malescream_5.ogg")
    );
    /// Female screams
    // tg: code/modules/mob/living/basic/revolutionary.dm:66
    private static DmList female_screams => TgState.Current.mob_living_basic_revolutionary__female_screams ??= Dm.list(
        Dm.resource("sound/mobs/humanoids/human/scream/femalescream_1.ogg"),
        Dm.resource("sound/mobs/humanoids/human/scream/femalescream_2.ogg"),
        Dm.resource("sound/mobs/humanoids/human/scream/femalescream_3.ogg"),
        Dm.resource("sound/mobs/humanoids/human/scream/femalescream_4.ogg"),
        Dm.resource("sound/mobs/humanoids/human/scream/femalescream_5.ogg")
    );

    // tg: code/modules/mob/living/basic/revolutionary.dm:75
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        shuffle_inplace(phrases);
        // tg: var/static/list/display_emote (the proc's static)
        DmList display_emote = TgState.Current.mob_living_basic_revolutionary__Initialize__display_emote ??= Dm.list(
            Dm.kv(BB_EMOTE_SAY, phrases),
            Dm.kv(BB_EMOTE_SOUND, monkey_screeches),
            Dm.kv(BB_SPEAK_CHANCE, 15)
        );
        ((datum_ai_controller)ai_controller).set_blackboard_key(BB_BASIC_MOB_SPEAK_LINES, display_emote);
        Type weapon_of_choice = pick(possible_weapons) as Type;
        attack_sound = Dm.initial(weapon_of_choice, (obj_item p) => p.hitsound);
        attack_verb_simple = Dm.str(possible_weapons[weapon_of_choice]);
        attack_verb_continuous = Dm.text("[]s", attack_verb_simple);

        Waiting.NoSleep(AddElement(typeof(datum_element_death_drops), typeof(obj_effect_mob_spawn_corpse_human_revolutionary)));
        apply_dynamic_human_appearance(this, mob_spawn_path: typeof(obj_effect_mob_spawn_corpse_human_revolutionary), l_hand: weapon_of_choice);

        gender = Dm.str(pick(MALE, FEMALE, PLURAL));
        object first_name = null;
        object sw__1 = gender;
        if (Dm.eq(sw__1, MALE))
        {
            first_name = pick(GLOB.first_names_male);
            death_sound = pick(Dm.add(male_screams, monkey_screeches));
        }
        else if (Dm.eq(sw__1, FEMALE))
        {
            first_name = pick(GLOB.first_names_female);
            death_sound = pick(Dm.add(female_screams, monkey_screeches));
        }
        else if (Dm.eq(sw__1, PLURAL))
        {
            first_name = pick(GLOB.first_names);
            death_sound = pick(Dm.add(Dm.add(male_screams, female_screams), monkey_screeches));
        }

        Waiting.NoSleep(fully_replace_character_name(name, Dm.text("[] []", first_name, pick(GLOB.last_names))));
        desc += span_infoplain(I18n.T("\nToday, that cause is: "));
        shuffle_inplace(causes);
        desc += span_notice(Dm.text("#[].", pick(causes)));
        return dot;
    }

    // tg: code/modules/mob/living/basic/revolutionary.dm:110
    public override object get_unconscious_appearance()
    {
        return get_generic_humanoid_static_appearance();
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/basic/revolutionary.dm:19-73  var/static/list/possible_weapons, phrases, causes, monkey_screeches,
    // male_screams, female_screams
    internal DmList mob_living_basic_revolutionary__possible_weapons;
    internal DmList mob_living_basic_revolutionary__phrases;
    internal DmList mob_living_basic_revolutionary__causes;
    internal DmList mob_living_basic_revolutionary__monkey_screeches;
    internal DmList mob_living_basic_revolutionary__male_screams;
    internal DmList mob_living_basic_revolutionary__female_screams;
    // tg: code/modules/mob/living/basic/revolutionary.dm:78  the Initialize's var/static/list/display_emote
    internal DmList mob_living_basic_revolutionary__Initialize__display_emote;
}
