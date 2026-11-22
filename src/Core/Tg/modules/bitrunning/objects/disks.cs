// Translated from tgstation (snapshot 1d99bd7f): code/modules/bitrunning/objects/disks.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps BR1 (D339, D341): the whole file, the bitrunning tech disks: a choice made in hand, then loaded onto the avatar by the gear
// signal (components/avatar_gear.dm) when its carrier enters a domain. The var blocks are generated (D111), but for the item disk's
// granted_item: tg declares it /obj and stores the chosen typepath in it, so the hand part declares it Type (rules 3.2, V3) and keeps
// the generated constructor and list default (types-converter.md 3.2: a hand-declared var puts the class in hand mode).

/**
 * Bitrunning tech disks which let you load items or programs into the vdom on first avatar generation.
 * For the record: Balance shouldn't be a primary concern.
 * You can make the custom cheese spells you've always wanted.
 * Just make it fun and engaging, it's PvE content.
 */
// tg: code/modules/bitrunning/objects/disks.dm:7
public partial class obj_item_disk_bitrunning : obj_item_disk
{
    // tg: code/modules/bitrunning/objects/disks.dm:16
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        icon_state = Dm.text("[][]", base_icon_state, rand(0, 7));
        Waiting.NoSleep(update_icon());

        Waiting.NoSleep(AddComponent(typeof(datum_component_loads_avatar_gear),
            Dm.named("load_callback", Dm.@new<datum_callback>(this, nameof(load_onto_avatar)))));
        return dot;
    }

    // tg: code/modules/bitrunning/objects/disks.dm:26
    public override async DmTask setup_reskins()
    {
        await DmTask.CompletedTask;
    }

    // tg: code/modules/bitrunning/objects/disks.dm:29
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        Dm.append(ref dot, span_infoplain(I18n.T("This disk must be carried on your person into a netpod to be used.")));

        if (choice_made == null)
        {
            Dm.append(ref dot, span_notice(I18n.T("To make a selection, toggle the disk in hand.")));
            return dot;
        }

        Dm.append(ref dot, span_info(I18n.T("It has been used to select: <b>[]</b>.", choice_made)));
        Dm.append(ref dot, span_notice(I18n.T("It cannot make another selection.")));
        return dot;
    }

    /// Handles loading our stuff onto avatars
    // tg: code/modules/bitrunning/objects/disks.dm:41
    public virtual async DmTask<object> load_onto_avatar(mob_living_carbon_human neo = null, mob_living_carbon_human avatar = null, object domain_flags = null)
    {
        await DmTask.CompletedTask;
        return NONE;
    }
}

// tg: code/modules/bitrunning/objects/disks.dm:45
public partial class obj_item_disk_bitrunning_ability : obj_item_disk_bitrunning
{
    // tg: code/modules/bitrunning/objects/disks.dm:51
    public override async DmTask<object> load_onto_avatar(mob_living_carbon_human neo = null, mob_living_carbon_human avatar = null, object domain_flags = null)
    {
        if (Dm.truthy(Dm.bitand(domain_flags, DOMAIN_FORBIDS_ABILITIES)))
            return BITRUNNER_GEAR_LOAD_BLOCKED;

        if (granted_action == null)
            return BITRUNNER_GEAR_LOAD_FAILED;

        if (Dm.locate_type(granted_action, avatar.actions) != null)
            return BITRUNNER_GEAR_LOAD_FAILED;

        datum_action our_action = await Dm.new_async(granted_action) as datum_action;
        await our_action.Grant(avatar);
        return NONE;
    }

    // tg: code/modules/bitrunning/objects/disks.dm:65
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        // Not calling parent to not flip the protection tab

        if (Dm.truthy(choice_made))
            return null;

        DmList names = Dm.list();
        foreach (Type thing in Dm.each<Type>(selectable_actions))
            Dm.append(ref names, Dm.initial(thing, (datum_action p) => p.name));

        object choice = await tgui_input_list(user, message: I18n.T("Select an ability"), title: I18n.T("Bitrunning Program"), items: names);
        if (choice == null || !Dm.truthy(user.is_holding(this)))
            return null;

        foreach (Type thing in Dm.each<Type>(selectable_actions))
        {
            if (Dm.eq(Dm.initial(thing, (datum_action p) => p.name), choice))
                granted_action = thing;
        }

        if (granted_action == null)
            return null;

        balloon_alert(user, I18n.T("selected"));
        playsound(user, Dm.resource("sound/items/click.ogg"), 50, TRUE);
        choice_made = choice;
        return null;
    }
}

// tg: code/modules/bitrunning/objects/disks.dm:117
public partial class obj_item_disk_bitrunning_item : obj_item_disk_bitrunning
{
    /// The selected item that this grants
    // tg: code/modules/bitrunning/objects/disks.dm:119  (var/obj/granted_item: a typepath, rules 3.2 V3)
    public Type granted_item;

    // tg: code/modules/bitrunning/objects/disks.dm:117 (the generated constructor, kept: the hand-declared var above)
    public obj_item_disk_bitrunning_item()
    {
        desc = "A disk containing source code. It can be used to preload items into the virtual domain.";
    }

    // tg: code/modules/bitrunning/objects/disks.dm:121 (the generated list default, kept)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        selectable_items = Dm.list(); // tg: code/modules/bitrunning/objects/disks.dm:121
    }

    // tg: code/modules/bitrunning/objects/disks.dm:123
    public override async DmTask<object> load_onto_avatar(mob_living_carbon_human neo = null, mob_living_carbon_human avatar = null, object domain_flags = null)
    {
        if (Dm.truthy(Dm.bitand(domain_flags, DOMAIN_FORBIDS_ITEMS)))
            return BITRUNNER_GEAR_LOAD_BLOCKED;

        if (granted_item == null)
            return BITRUNNER_GEAR_LOAD_FAILED;

        await avatar.put_in_hands(await Dm.new_async(granted_item) as obj_item);
        return NONE;
    }

    // tg: code/modules/bitrunning/objects/disks.dm:133
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);

        if (Dm.truthy(choice_made))
            return dot;

        DmList names = Dm.list();
        foreach (Type thing in Dm.each<Type>(selectable_items))
            Dm.append(ref names, Dm.initial(thing, (atom p) => p.name));

        object choice = await tgui_input_list(user, message: I18n.T("Select an ability"), title: I18n.T("Bitrunning Program"), items: names);
        if (choice == null || !Dm.truthy(user.is_holding(this)))
            return dot;

        foreach (Type thing in Dm.each<Type>(selectable_items))
        {
            if (Dm.eq(Dm.initial(thing, (atom p) => p.name), choice))
                granted_item = thing;
        }

        balloon_alert(user, I18n.T("selected"));
        playsound(user, Dm.resource("sound/items/click.ogg"), 50, TRUE);
        choice_made = choice;
        return dot;
    }
}
