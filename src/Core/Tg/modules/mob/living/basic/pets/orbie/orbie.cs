// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/pets/orbie/orbie.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S4d, D327): Orbie, the Virtual Pet app's hologram: its commands, its virtual chocolate, its eyes, flame and light
// overlays, its happy eyes for half a minute after a nuzzle, and its health growing with its level. The var block is generated (D111).

// tg: code/modules/mob/living/basic/pets/orbie/orbie.dm:3
public partial class mob_living_basic_orbie : mob_living_basic
{
    // tg: code/modules/mob/living/basic/pets/orbie/orbie.dm:1  #define ORBIE_MAXIMUM_HEALTH 300 (the file's define)
    private const float ORBIE_MAXIMUM_HEALTH = 300;

    ///overlay for our neutral eyes
    // tg: code/modules/mob/living/basic/pets/orbie/orbie.dm:33  var/static/mutable_appearance (rules 10.6)
    private static mutable_appearance eyes_overlay => TgState.Current.mob_living_basic_orbie__eyes_overlay ??= mutable_appearance(Dm.resource("icons/mob/simple/pets.dmi"), "orbie_eye_overlay") as mutable_appearance;
    ///overlay for when our eyes are emitting light
    // tg: code/modules/mob/living/basic/pets/orbie/orbie.dm:35
    private static mutable_appearance orbie_light_overlay => TgState.Current.mob_living_basic_orbie__orbie_light_overlay ??= mutable_appearance(Dm.resource("icons/mob/simple/pets.dmi"), "orbie_light_overlay") as mutable_appearance;
    ///overlay for the flame propellar
    // tg: code/modules/mob/living/basic/pets/orbie/orbie.dm:37
    private static mutable_appearance flame_overlay => TgState.Current.mob_living_basic_orbie__flame_overlay ??= mutable_appearance(Dm.resource("icons/mob/simple/pets.dmi"), "orbie_flame_overlay") as mutable_appearance;
    ///overlay for our happy eyes
    // tg: code/modules/mob/living/basic/pets/orbie/orbie.dm:39
    private static mutable_appearance happy_eyes_overlay => TgState.Current.mob_living_basic_orbie__happy_eyes_overlay ??= mutable_appearance(Dm.resource("icons/mob/simple/pets.dmi"), "orbie_happy_eye_overlay") as mutable_appearance;
    ///commands we can give orbie
    // tg: code/modules/mob/living/basic/pets/orbie/orbie.dm:41
    private static DmList pet_commands => TgState.Current.mob_living_basic_orbie__pet_commands ??= Dm.list(
        typeof(datum_pet_command_idle),
        typeof(datum_pet_command_free),
        typeof(datum_pet_command_move),
        typeof(datum_pet_command_untargeted_ability_pet_lights),
        typeof(datum_pet_command_use_ability_take_photo),
        typeof(datum_pet_command_follow_orbie),
        typeof(datum_pet_command_perform_trick_sequence));

    // tg: code/modules/mob/living/basic/pets/orbie/orbie.dm:51
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_holographic_nature)));
        DmList food_types = TgState.Current.mob_living_basic_orbie__food_types ??= Dm.list(typeof(obj_item_food_virtual_chocolate));
        Waiting.NoSleep(AddComponent(typeof(datum_component_obeys_commands), pet_commands));
        Waiting.NoSleep(AddElement(typeof(datum_element_basic_eating), null, null, null, null, null, null, food_types));
        ADD_TRAIT(this, TRAIT_SILICON_EMOTES_ALLOWED, INNATE_TRAIT);

        RegisterSignal(this, COMSIG_VIRTUAL_PET_LEVEL_UP, nameof(on_level_up));
        RegisterSignal(this, COMSIG_ATOM_UPDATE_LIGHT_ON, nameof(on_lights));
        (ai_controller as datum_ai_controller).set_blackboard_key(BB_BASIC_FOODS, typecacheof(food_types));
        update_appearance();
        return dot;
    }

    // tg: code/modules/mob/living/basic/pets/orbie/orbie.dm:64
    public override async DmTask<object> early_melee_attack(atom target = null, DmList modifiers = null, float? ignore_cooldown = null)
    {
        object dot = await base.early_melee_attack(target, modifiers, ignore_cooldown);
        if (Dm.truthy(dot))
            return dot;
        if (this == target || Dm.truthy(happy_state) || target == null)
            return BASIC_MOB_CONTINUE_ATTACK_CHAIN;

        toggle_happy_state();
        _addtimer(Dm.@new<datum_callback>(this, nameof(toggle_happy_state)), 30 * SECONDS, file: "code/modules/mob/living/basic/pets/orbie/orbie.dm", line: 72);
        return BASIC_MOB_END_ATTACK_CHAIN_COOLDOWN;
    }

    // tg: code/modules/mob/living/basic/pets/orbie/orbie.dm:75
    [SignalHandler]
    public virtual void on_lights(datum source = null)
    {
        update_appearance();
    }

    // tg: code/modules/mob/living/basic/pets/orbie/orbie.dm:80
    public virtual void toggle_happy_state()
    {
        happy_state = !Dm.truthy(happy_state) ? TRUE : FALSE;
        update_appearance();
    }

    // tg: code/modules/mob/living/basic/pets/orbie/orbie.dm:84
    public override object can_be_pulled(object user = null, object force = null)
    {
        return FALSE;
    }

    // tg: code/modules/mob/living/basic/pets/orbie/orbie.dm:87
    [SignalHandler]
    public virtual void on_level_up(datum source = null, object new_level = null)
    {
        if (Dm.num(maxHealth) >= ORBIE_MAXIMUM_HEALTH)
        {
            UnregisterSignal(this, COMSIG_VIRTUAL_PET_LEVEL_UP);
            return;
        }

        maxHealth = Dm.num(maxHealth) + 100;
        Waiting.NoSleep(heal_overall_damage(Dm.num(maxHealth) - Dm.num(health)));
    }

    // tg: code/modules/mob/living/basic/pets/orbie/orbie.dm:98
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (stat == DEAD)
            return dot;
        Dm.append(ref dot, flame_overlay);
        if (Dm.truthy(happy_state))
            Dm.append(ref dot, happy_eyes_overlay);
        else if (Dm.truthy(light_on))
            Dm.append(ref dot, orbie_light_overlay);
        else
            Dm.append(ref dot, eyes_overlay);
        return dot;
    }

    // tg: code/modules/mob/living/basic/pets/orbie/orbie.dm:110
    public override async DmTask gib(float? drop_bitflags = null)
    {
        await death(TRUE);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/basic/pets/orbie/orbie.dm:33-49, :53  Orbie's static overlays and lists (rules 10.6)
    internal mutable_appearance mob_living_basic_orbie__eyes_overlay;
    internal mutable_appearance mob_living_basic_orbie__orbie_light_overlay;
    internal mutable_appearance mob_living_basic_orbie__flame_overlay;
    internal mutable_appearance mob_living_basic_orbie__happy_eyes_overlay;
    internal DmList mob_living_basic_orbie__pet_commands;
    internal DmList mob_living_basic_orbie__food_types;
}
