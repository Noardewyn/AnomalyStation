// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/choice_beacon.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the beacon and the heroic beacon (gaps J3, D298); the instrument, augment and chaplain's armaments beacons (gaps OD1, D333).
// The var blocks are generated.

// tg: code/game/objects/items/choice_beacon.dm:2
public partial class obj_item_choice_beacon : obj_item
{
    // tg: code/game/objects/items/choice_beacon.dm:16
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        object dot = await base.interact(user, __extra);
        if (!Dm.truthy(await can_use_beacon(user as mob_living)))
            return dot;

        await open_options_menu(user as mob_living);
        return dot;
    }

    /// Return the list that will be used in the choice selection.
    /// Entries should be in (type.name = type) fashion.
    // tg: code/game/objects/items/choice_beacon.dm:25
    public virtual object generate_display_names()
    {
        return Dm.list();
    }

    /// Checks if this mob can use the beacon, returns TRUE if so or FALSE otherwise.
    // tg: code/game/objects/items/choice_beacon.dm:29
    public virtual async DmTask<object> can_use_beacon(mob_living user = null)
    {
        if (Dm.truthy(await user.can_perform_action(this, FORBID_TELEKINESIS_REACH)))
            return TRUE;

        playsound(this, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 40, TRUE);
        return FALSE;
    }

    /// Opens a menu and allows the mob to pick an option from the list
    // tg: code/game/objects/items/choice_beacon.dm:37
    public virtual async DmTask open_options_menu(mob_living user = null)
    {
        DmList display_names = generate_display_names() as DmList;
        if (!Dm.truthy(Dm.length(display_names)))
            return;
        object choice = await tgui_input_list(user, I18n.T("Which item would you like to order?"), I18n.T("Select an Item"), display_names);
        if (choice == null || display_names[choice] == null)
            return;
        if (!Dm.truthy(await can_use_beacon(user)))
            return;

        await consume_use(display_names[choice] as Type, user);
    }

    /// Consumes a use of the beacon, sending the user a message and creating their item in the process
    // tg: code/game/objects/items/choice_beacon.dm:50
    public virtual async DmTask consume_use(Type choice_path = null, mob_living user = null)
    {
        to_chat(user, span_hear(I18n.T("You hear something crackle from the beacon for a moment before a voice speaks. \"Please stand by for a message from []. Message as follows: [] Message ends.\"", company_source, company_message)));

        await spawn_option(choice_path, user);
        uses--;
        if (uses <= 0)
        {
            do_sparks(3, source: this);
            qdel(this);
            return;
        }

        to_chat(user, span_notice(I18n.T("[] use[] remain[] on [].", uses, uses > 1 ? "s" : "", uses > 1 ? "" : "s", this)));
    }

    /// Actually spawns the item selected by the user
    // tg: code/game/objects/items/choice_beacon.dm:64
    public virtual async DmTask spawn_option(Type choice_path = null, mob_living user = null)
    {
        await podspawn(Dm.list(
            Dm.kv("target", get_turf(this)),
            Dm.kv("style", typeof(datum_pod_style_advanced)),
            Dm.kv("spawn", choice_path)));
    }
}

// tg: code/game/objects/items/choice_beacon.dm:104
public partial class obj_item_choice_beacon_hero : obj_item_choice_beacon
{
    // tg: code/game/objects/items/choice_beacon.dm:111
    public override object generate_display_names()
    {
        if (TgState.Current.obj_item_choice_beacon_hero__hero_item_list == null)
        {
            DmList hero_item_list = Dm.list();
            foreach (Type box in Dm.each<Type>(typesof(typeof(obj_item_storage_box_hero))))
                hero_item_list[Dm.initial(box, (obj_item_storage_box_hero p) => p.name)] = box;
            TgState.Current.obj_item_choice_beacon_hero__hero_item_list = hero_item_list;
        }
        return TgState.Current.obj_item_choice_beacon_hero__hero_item_list;
    }
}

// tg: code/game/objects/items/choice_beacon.dm:72
public partial class obj_item_choice_beacon_music : obj_item_choice_beacon
{
    // tg: code/game/objects/items/choice_beacon.dm:80
    public override object generate_display_names()
    {
        if (TgState.Current.obj_item_choice_beacon_music__instruments == null)
        {
            DmList instruments = Dm.list();
            DmList possible_instruments = Dm.list(
                typeof(obj_item_instrument_violin),
                typeof(obj_item_instrument_piano_synth),
                typeof(obj_item_instrument_banjo),
                typeof(obj_item_instrument_guitar),
                typeof(obj_item_instrument_eguitar),
                typeof(obj_item_instrument_glockenspiel),
                typeof(obj_item_instrument_accordion),
                typeof(obj_item_instrument_trumpet),
                typeof(obj_item_instrument_saxophone),
                typeof(obj_item_instrument_trombone),
                typeof(obj_item_instrument_recorder),
                typeof(obj_item_instrument_harmonica),
                typeof(obj_item_instrument_piano_synth_headphones));
            foreach (Type instrument in Dm.each<Type>(possible_instruments))
                instruments[Dm.initial(instrument, (obj_item_instrument p) => p.name)] = instrument;
            TgState.Current.obj_item_choice_beacon_music__instruments = instruments;
        }
        return TgState.Current.obj_item_choice_beacon_music__instruments;
    }
}

// tg: code/game/objects/items/choice_beacon.dm:120
public partial class obj_item_choice_beacon_augments : obj_item_choice_beacon
{
    // tg: code/game/objects/items/choice_beacon.dm:128
    public override object generate_display_names()
    {
        if (TgState.Current.obj_item_choice_beacon_augments__augment_list == null)
        {
            DmList augment_list = Dm.list();
            // cyberimplants range from a nice bonus to fucking broken bullshit so no subtypesof
            DmList selectable_types = Dm.list(
                typeof(obj_item_organ_cyberimp_brain_anti_drop),
                typeof(obj_item_organ_cyberimp_arm_toolkit_toolset),
                typeof(obj_item_organ_cyberimp_arm_toolkit_surgery),
                typeof(obj_item_organ_cyberimp_chest_thrusters),
                typeof(obj_item_organ_lungs_cybernetic_tier3),
                typeof(obj_item_organ_liver_cybernetic_tier3));
            foreach (Type organ in Dm.each<Type>(selectable_types))
                augment_list[Dm.initial(organ, (obj_item_organ p) => p.name)] = organ;
            TgState.Current.obj_item_choice_beacon_augments__augment_list = augment_list;
        }

        return TgState.Current.obj_item_choice_beacon_augments__augment_list;
    }

    // just drops the box at their feet, "quiet" and "sneaky"
    // tg: code/game/objects/items/choice_beacon.dm:147
    public override DmTask spawn_option(Type choice_path = null, mob_living user = null)
    {
        Dm.@new(choice_path, get_turf(user));
        playsound(this, Dm.resource("sound/items/weapons/emitter2.ogg"), 50, extrarange: SILENCED_SOUND_EXTRARANGE);
        return DmTask.CompletedTask;
    }
}

// tg: code/game/objects/items/choice_beacon.dm:152
public partial class obj_item_choice_beacon_holy : obj_item_choice_beacon
{
    // tg: code/game/objects/items/choice_beacon.dm:159
    public override async DmTask<object> can_use_beacon(mob_living user = null)
    {
        if (Dm.truthy(user.mind?.holy_role))
            return await base.can_use_beacon(user);

        playsound(this, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 40, TRUE);
        return FALSE;
    }

    // Overrides generate options so that we can show a neat radial instead
    // tg: code/game/objects/items/choice_beacon.dm:167
    public override async DmTask open_options_menu(mob_living user = null)
    {
        if (GLOB.holy_armor_type != null)
        {
            await consume_use(GLOB.holy_armor_type, user);
            return;
        }

        // Not bothering to cache this stuff because it'll only even be used once
        DmList armament_names_to_images = Dm.list();
        DmList armament_names_to_typepaths = Dm.list();
        foreach (Type holy_box in Dm.each<Type>(typesof(typeof(obj_item_storage_box_holy))))
        {
            object box_name = Dm.initial(holy_box, (obj_item_storage_box_holy p) => p.name);
            Type preview_item = Dm.initial(holy_box, (obj_item_storage_box_holy p) => p.typepath_for_preview);
            armament_names_to_typepaths[box_name] = holy_box;
            armament_names_to_images[box_name] = image(icon: Dm.initial(preview_item, (obj_item p) => p.icon), icon_state: Dm.initial(preview_item, (obj_item p) => p.icon_state));
        }

        object chosen_name = await show_radial_menu(
            user: user,
            anchor: this,
            choices: armament_names_to_images,
            custom_check: Dm.@new<datum_callback>(this, nameof(can_use_beacon), user),
            require_near: TRUE);
        if (!Dm.truthy(await can_use_beacon(user)))
            return;
        object chosen_type = armament_names_to_typepaths[chosen_name];
        if (!ispath(chosen_type, typeof(obj_item_storage_box_holy)))
            return;

        await consume_use(chosen_type as Type, user);
    }

    // tg: code/game/objects/items/choice_beacon.dm:196
    public override async DmTask spawn_option(Type choice_path = null, mob_living user = null)
    {
        playsound(this, Dm.resource("sound/effects/pray_chaplain.ogg"), 40, TRUE);
        SSblackbox.record_feedback("tally", "chaplain_armor", 1, Dm.text("[]", choice_path));
        GLOB.holy_armor_type = choice_path;
        await base.spawn_option(choice_path, user);
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/items/choice_beacon.dm:112  generate_display_names' var/static/list/hero_item_list
    internal DmList obj_item_choice_beacon_hero__hero_item_list;

    // tg: code/game/objects/items/choice_beacon.dm:81  /obj/item/choice_beacon/music/generate_display_names' var/static/list/instruments
    internal DmList obj_item_choice_beacon_music__instruments;

    // tg: code/game/objects/items/choice_beacon.dm:129  /obj/item/choice_beacon/augments/generate_display_names' var/static/list/augment_list
    internal DmList obj_item_choice_beacon_augments__augment_list;
}
