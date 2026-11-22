// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/pet_commands/obeys_commands.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C2).

/**
 * # Obeys Commands Component
 * Manages a list of pet command datums, allowing you to boss it around
 * Creates a radial menu of pet commands when this creature is alt-clicked, if it has any
 */
// tg: code/datums/components/pet_commands/obeys_commands.dm:9
public partial class datum_component_obeys_commands : datum_component
{
    // tg: code/datums/components/pet_commands/obeys_commands.dm:6
    private const float DEFAULT_RADIAL_VIEWING_DISTANCE = 9;

    /// The available_commands parameter should be passed as a list of typepaths
    // tg: code/datums/components/pet_commands/obeys_commands.dm:22
    [DmParams("command_typepaths", "radial_menu_offset", "radial_menu_lifetime", "radial_relative_to_user")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        DmList command_typepaths = Dm.arg<DmList>(__extra, 0) ?? Dm.list();
        DmList radial_menu_offset = Dm.arg<DmList>(__extra, 1) ?? Dm.list(0, 0);
        object radial_menu_lifetime = Dm.arg(__extra, 2) ?? 7 * SECONDS;
        object radial_relative_to_user = Dm.arg(__extra, 3) ?? FALSE;
        if (!isliving(parent))
            return COMPONENT_INCOMPATIBLE;
        mob_living living_parent = parent as mob_living;
        if (!Dm.truthy(living_parent.ai_controller))
            return COMPONENT_INCOMPATIBLE;
        if (!Dm.truthy(length(command_typepaths)))
            throw Dm.CRASH("Initialised obedience component with no commands.", "code/datums/components/pet_commands/obeys_commands.dm", 30);
        this.radial_menu_offset = radial_menu_offset;
        this.radial_relative_to_user = Dm.num(radial_relative_to_user);
        this.radial_menu_lifetime = Dm.num(radial_menu_lifetime);
        foreach (object command_path in Dm.each<object>(command_typepaths))
        {
            datum_pet_command new_command = Dm.@new((Type)command_path, parent) as datum_pet_command;
            available_commands[new_command.command_name] = new_command;
        }
        return null;
    }

    // tg: code/datums/components/pet_commands/obeys_commands.dm:38
    public override object Destroy(float? force = null)
    {
        QDEL_LIST_ASSOC_VAL(available_commands);
        return base.Destroy(force);
    }

    // tg: code/datums/components/pet_commands/obeys_commands.dm:42
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_LIVING_BEFRIENDED, nameof(add_friend));
        RegisterSignal(parent, COMSIG_LIVING_UNFRIENDED, nameof(remove_friend));
        RegisterSignal(parent, COMSIG_ATOM_EXAMINE, nameof(on_examine));
    }

    // tg: code/datums/components/pet_commands/obeys_commands.dm:47
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(COMSIG_LIVING_BEFRIENDED, COMSIG_LIVING_UNFRIENDED, COMSIG_ATOM_EXAMINE, COMSIG_CLICK_ALT));
    }

    /// Add someone to our friends list
    // tg: code/datums/components/pet_commands/obeys_commands.dm:51
    [SignalHandler]
    public virtual void add_friend(datum source = null, mob_living new_friend = null)
    {
        RegisterSignal(new_friend, COMSIG_KB_LIVING_VIEW_PET_COMMANDS, nameof(on_key_pressed));
        RegisterSignal(new_friend, DEACTIVATE_KEYBIND(COMSIG_KB_LIVING_VIEW_PET_COMMANDS), nameof(on_key_unpressed));
        foreach (object command_name in Dm.each<object>(available_commands))
        {
            datum_pet_command command = available_commands[command_name] as datum_pet_command;
            Waiting.Detach(command.add_new_friend(new_friend));
        }
    }

    // tg: code/datums/components/pet_commands/obeys_commands.dm:59
    [SignalHandler]
    public virtual void on_key_unpressed(mob_living source = null)
    {
        UnregisterSignal(source, COMSIG_ATOM_MOUSE_ENTERED);
        remove_from_viewers(source);
    }

    // tg: code/datums/components/pet_commands/obeys_commands.dm:64
    public virtual void remove_from_viewers(mob_living source = null)
    {
        Dm.remove(ref radial_viewers, REF(source));
    }

    /// Remove someone from our friends list
    // tg: code/datums/components/pet_commands/obeys_commands.dm:68
    [SignalHandler]
    public virtual void remove_friend(datum source = null, mob_living old_friend = null)
    {
        UnregisterSignal(old_friend, Dm.list(COMSIG_KB_LIVING_VIEW_PET_COMMANDS, DEACTIVATE_KEYBIND(COMSIG_KB_LIVING_VIEW_PET_COMMANDS)));
        foreach (object command_name in Dm.each<object>(available_commands))
        {
            datum_pet_command command = available_commands[command_name] as datum_pet_command;
            Waiting.Detach(command.remove_friend(old_friend));
        }
    }

    /// Add a note about whether they will follow the instructions of the inspecting mob
    // tg: code/datums/components/pet_commands/obeys_commands.dm:79
    [SignalHandler]
    public virtual void on_examine(mob_living source = null, mob user = null, DmList examine_list = null)
    {
        if (Dm.truthy(source.incapacitated))
            return;
        if (!Dm.@in(user, (source.ai_controller as datum_ai_controller)?.blackboard[BB_FRIENDS_LIST]))
            return;
        Dm.append(ref examine_list, span_notice(Dm.text("[] seem[] happy to see you!", source.p_They(), source.p_s())));
    }

    // tg: code/datums/components/pet_commands/obeys_commands.dm:88
    [SignalHandler]
    public virtual void on_key_pressed(mob_living friend = null)
    {
        RegisterSignal(friend, COMSIG_ATOM_MOUSE_ENTERED, nameof(on_mouse_hover));
    }

    // tg: code/datums/components/pet_commands/obeys_commands.dm:92
    [SignalHandler]
    public virtual void on_mouse_hover(mob_living friend = null, atom mouse_hovered = null)
    {
        if (mouse_hovered == parent)
        {
            display_menu(friend);
            return;
        }

        mob_living owner = parent as mob_living;
        if (isliving(mouse_hovered) && mouse_hovered.loc != owner.loc)
            remove_from_viewers(friend);
    }

    /// Displays a radial menu of commands
    // tg: code/datums/components/pet_commands/obeys_commands.dm:103
    public virtual void display_menu(mob_living friend = null)
    {
        mob_living living_parent = parent as mob_living;
        if (Dm.truthy(living_parent.incapacitated) || IS_UNCONSCIOUS_OR_CRIT(friend))
            return;
        if (!Dm.@in(friend, (living_parent.ai_controller as datum_ai_controller)?.blackboard[BB_FRIENDS_LIST]))
            return; // Not our friend, can't boss us around
        if (Dm.truthy(radial_viewers[REF(friend)]))
            return;
        if (!Dm.truthy(can_see(friend, parent as atom, DEFAULT_RADIAL_VIEWING_DISTANCE)))
            return;
        Waiting.Detach(display_radial_menu(friend));
    }

    /// Actually display the radial menu and then do something with the result
    // tg: code/datums/components/pet_commands/obeys_commands.dm:117
    public virtual async DmTask display_radial_menu(mob_living friend = null)
    {
        DmList radial_options = Dm.list();
        foreach (object command_name in Dm.each<object>(available_commands))
        {
            datum_pet_command command = available_commands[command_name] as datum_pet_command;
            object choice = command.provide_radial_data();
            if (!Dm.truthy(choice))
                continue;
            Dm.append(ref radial_options, choice);
        }
        radial_viewers[REF(friend)] = world.time + radial_menu_lifetime;
        object pick = await show_radial_menu(friend, parent as atom, radial_options, radius: radial_menu_radius, button_animation_flags: BUTTON_FADE_IN | BUTTON_FADE_OUT, custom_check: Dm.@new<datum_callback>(this, nameof(check_menu_viewer), friend), check_delay: 0.15f * SECONDS, display_close_button: FALSE, radial_menu_offset: radial_menu_offset, user_space: Dm.num(radial_relative_to_user));
        remove_from_viewers(friend);
        if (!Dm.truthy(pick))
            return;
        datum_pet_command picked_command = available_commands[pick] as datum_pet_command;
        await picked_command.try_activate_command(friend, radial_command: TRUE);
    }

    // tg: code/datums/components/pet_commands/obeys_commands.dm:133
    public virtual object check_menu_viewer(mob_living user = null)
    {
        if (QDELETED(user) || !Dm.truthy(radial_viewers[REF(user)]))
            return FALSE;
        if (world.time > Dm.num(radial_viewers[REF(user)]))
            return FALSE;
        float viewing_distance = DEFAULT_RADIAL_VIEWING_DISTANCE;
        if (!Dm.truthy(can_see(user, parent as atom, viewing_distance)))
            return FALSE;
        return TRUE;
    }
}
