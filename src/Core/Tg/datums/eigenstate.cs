// Translated from tgstation (snapshot 1d99bd7f): code/datums/eigenstate.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps RG3 (D339): the whole file (the eigenlinked lockers: eigenstasium splashed on lockers links them, a thing put in one comes out
// of the next, the violet tint, the sparks, a welder refused; the linked closets station trait and the rare roundstart maintenance
// link; the admin-spawned eigenlinked closets). The var blocks are generated (D111).

// tg: code/datums/eigenstate.dm:1  GLOBAL_DATUM_INIT(closet_teleport_controller, /datum/closet_teleport_controller, new)
public partial class datum_controller_global_vars
{
    public datum_closet_teleport_controller closet_teleport_controller;

    // tg: code/datums/eigenstate.dm:1  (GLOBAL_MANAGED(closet_teleport_controller, new), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalcloset_teleport_controller()
    {
        closet_teleport_controller = Dm.@new<datum_closet_teleport_controller>();
        Dm.append(ref gvars_datum_init_order, "closet_teleport_controller");
    }
}

///A singleton used to teleport people to a linked web of itterative entries. If one entry is deleted, the 2 around it will forge a link instead.
// tg: code/datums/eigenstate.dm:4
public partial class datum_closet_teleport_controller : datum
{
    ///Creates a new link of targets unique to their own id
    // tg: code/datums/eigenstate.dm:17
    public virtual object create_new_link(object targets = null, float? subtle = null)
    {
        subtle ??= TRUE;
        if (length(targets) <= 1)
            return FALSE;
        DmList target_list = (DmList)targets;
        foreach (atom target in Dm.each_cast<atom>(target_list)) //Clear out any connected
        {
            object already_linked = eigen_id[target];
            if (!Dm.truthy(already_linked))
                continue;
            if (length(eigen_targets[already_linked]) > 1) //Eigenstates are notorious for having cliques!
            {
                if (!Dm.truthy(subtle))
                    target.visible_message(I18n.T("[] fizzes, it's already linked to something else!", target));
                Dm.remove(ref target_list, target);
                continue;
            }
            if (!Dm.truthy(subtle))
                target.visible_message(I18n.T("[] fizzes, collapsing its unique wavefunction into the others!", target)); //If we're in a eigenlink all on our own and are open to new friends
            remove_eigen_entry(target); //clearup for new stuff
        }
        //Do we still have targets?
        if (!Dm.truthy(length(target_list)))
            return FALSE;
        atom visible_atom = target_list[1] as atom; //The object that'll handle the messages
        if (length(target_list) == 1)
        {
            if (!Dm.truthy(subtle))
                visible_atom.visible_message(I18n.T("[] fizzes, there's nothing it can link to!", target_list[1]));
            return FALSE;
        }

        string subtle_keyword = Dm.truthy(subtle) ? "subtle" : "";
        eigen_targets[Dm.text("[][]", id_counter, subtle_keyword)] = Dm.list(); //Add to the master list
        foreach (atom target in Dm.each_cast<atom>(target_list))
        {
            Dm.append_at(eigen_targets, Dm.text("[][]", id_counter, subtle_keyword), target);
            eigen_id[target] = Dm.text("[][]", id_counter, subtle_keyword);
            RegisterSignal(target, COMSIG_CLOSET_INSERT, nameof(use_eigenlinked_atom));
            RegisterSignal(target, COMSIG_QDELETING, nameof(remove_eigen_entry));
            if (!Dm.truthy(subtle))
                RegisterSignal(target, COMSIG_ATOM_TOOL_ACT(TOOL_WELDER), nameof(tool_interact));
            ADD_TRAIT(target, TRAIT_BANNED_FROM_CARGO_SHUTTLE, REF(this));
            if (!Dm.truthy(subtle))
            {
                Waiting.NoSleep(target.add_atom_colour(COLOR_PERIWINKLEE, FIXED_COLOUR_PRIORITY)); //Tint the locker slightly.
                target.alpha = 200;
                do_sparks(3, FALSE, target);
            }
        }

        if (!Dm.truthy(subtle))
            visible_atom.visible_message(I18n.T("The items shimmer and fizzle, turning a shade of violet blue."));
        id_counter++;
        return TRUE;
    }

    ///removes an object reference from the master list
    // tg: code/datums/eigenstate.dm:72
    [SignalHandler]
    public virtual void remove_eigen_entry(atom entry = null, params object[] __extra)
    {
        object id = eigen_id[entry];
        Dm.remove_at(eigen_targets, id, entry);
        Dm.remove(ref eigen_id, entry);
        Waiting.NoSleep(entry.remove_atom_colour(FIXED_COLOUR_PRIORITY, COLOR_PERIWINKLEE));
        entry.alpha = 255;
        UnregisterSignal(entry, Dm.list(
            COMSIG_QDELETING,
            COMSIG_CLOSET_INSERT,
            COMSIG_ATOM_TOOL_ACT(TOOL_WELDER)
        ));
        REMOVE_TRAIT(entry, TRAIT_BANNED_FROM_CARGO_SHUTTLE, REF(this));
        ///Remove the current entry if we're empty
        foreach (object targets in Dm.each(eigen_targets))
        {
            if (!Dm.truthy(length(eigen_targets[targets])))
                Dm.remove(ref eigen_targets, targets);
        }
    }

    ///Finds the object within the master list, then sends the thing to the object's location
    // tg: code/datums/eigenstate.dm:91
    [SignalHandler]
    public virtual object use_eigenlinked_atom(atom object_sent_from = null, atom_movable thing_to_send = null)
    {
        object tp_flags = !Dm.truthy(object_sent_from._listen_lookup?[COMSIG_CLOSET_TELEPORTER_PRE_SENDING]) ? NONE : object_sent_from._SendSignal(COMSIG_CLOSET_TELEPORTER_PRE_SENDING, Dm.list(object_sent_from, thing_to_send));
        if (Dm.truthy(Dm.bitand(tp_flags, CLOSET_TELEPORT_BLOCKED)))
            return FALSE;
        object id = eigen_id[object_sent_from];
        if (!Dm.truthy(id))
        {
            stack_trace(Dm.text("[] attempted to eigenlink to something that didn't have a valid id!", object_sent_from), "code/datums/eigenstate.dm", 99);
            return FALSE;
        }
        float subtle = findtext(id, "subtle");
        DmList items = eigen_targets[id] as DmList;
        float index = (items.Find(object_sent_from)) + 1; //index + 1
        if (!Dm.truthy(index))
        {
            stack_trace(Dm.text("[] attempted to eigenlink to something that didn't contain it!", object_sent_from), "code/datums/eigenstate.dm", 106);
            return FALSE;
        }
        if (index > length(eigen_targets[id])) //If we're at the end of the list (or we're 1 length long)
            index = 1;
        atom eigen_target = ((DmList)eigen_targets[id])[index] as atom;
        if (eigen_target == null)
        {
            stack_trace("No eigen target set for the eigenstate component!", "code/datums/eigenstate.dm", 112);
            return FALSE;
        }
        if (Dm.truthy(Dm.bitand(tp_flags, CLOSET_TELEPORT_FORCED)) || Dm.truthy(check_teleport_valid(thing_to_send, eigen_target, TELEPORT_CHANNEL_QUANTUM)))
            Waiting.NoSleep(thing_to_send.forceMove(eigen_target));
        else
        {
            if (!Dm.truthy(subtle))
                object_sent_from.balloon_alert(thing_to_send as mob, I18n.T("nothing happens!"));
            return FALSE;
        }
        //Create ONE set of sparks for ALL times in iteration
        if (!Dm.truthy(subtle) && spark_time != world.time)
        {
            do_sparks(5, FALSE, eigen_target);
            do_sparks(5, FALSE, object_sent_from);
            spark_time = world.time;
        }
        return COMPONENT_CLOSET_INSERT_INTERRUPT;
    }

    ///Prevents tool use on the item
    // tg: code/datums/eigenstate.dm:127
    [SignalHandler]
    public virtual object tool_interact(atom source = null, mob user = null, obj_item item = null)
    {
        to_chat(user, span_notice(I18n.T("The unstable nature of [] makes it impossible to use [] on []!", source, item, source.p_them())));
        return ITEM_INTERACT_BLOCKING;
    }
}

///reverts everything back to start
// tg: code/datums/eigenstate.dm:62
public partial class datum_closet_teleport_controller_eigenstates : datum_closet_teleport_controller
{
    // tg: code/datums/eigenstate.dm:62
    public override object Destroy(float? force = null)
    {
        for (float index = 1; index <= id_counter; index++)
        {
            foreach (atom entry in Dm.each_cast<atom>(eigen_targets[Dm.text("[]", index)]))
                remove_eigen_entry(entry);
        }
        eigen_targets = null;
        eigen_id = null;
        id_counter = 1;
        return base.Destroy(force);
    }
}

// For testing purposes, primarily
// Spawns a closet that will auto-link to the next one spawned
// tg: code/datums/eigenstate.dm:134
public partial class obj_structure_closet_eigenlinked : obj_structure_closet
{
    // tg: code/datums/eigenstate.dm:138
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(mapload))
        {
            // i'm making no effort to ensure these link properly for mappers.
            // add a mapping helper which uses editable ids if you want that
            log_mapping(Dm.text("Don't map in [], it likely won't link properly as it is intended for debug. If you want this on your map you will need to add a mapping helper.", type));
            return INITIALIZE_HINT_QDEL;
        }

        var other = GLOB.closet_teleport_controller.admin_link?.resolve() as obj_structure_closet;
        if (other == null)
        {
            GLOB.closet_teleport_controller.admin_link = WEAKREF(this) as datum_weakref;
            return dot;
        }

        GLOB.closet_teleport_controller.create_new_link(Dm.list(this, other), subtle: this.subtle);
        return dot;
    }
}
