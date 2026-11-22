// Translated from tgstation (snapshot 1d99bd7f): code/modules/assembly/holder.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps AS1, D333): the assembly holder, two (up to twelve) assemblies attached into one item: built from two
// unsecured assemblies (assembly.dm's item_interaction), a pulse of one pulsing the others and the thing it is attached to
// (process_activation, its master's receive_signal), its overlays (each assembly's "_left" state, the even ones mirrored to the
// right, with their attached overlays), its use in hand, its rotation, the screwdriver that takes it apart, and the on_attach
// that tells its assemblies when it goes onto a special assembly or a holder. The var block is generated (D111).

// tg: code/modules/assembly/holder.dm:1
public partial class obj_item_assembly_holder : obj_item
{
    // tg: code/modules/assembly/holder.dm:16
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_rotation)));
        return dot;
    }

    // tg: code/modules/assembly/holder.dm:20
    public override object Destroy(float? force = null)
    {
        QDEL_LAZYLIST(ref assemblies);
        return base.Destroy(force);
    }

    // tg: code/modules/assembly/holder.dm:24
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        LAZYREMOVE(ref assemblies, gone);
        return dot;
    }

    // tg: code/modules/assembly/holder.dm:28
    public override object IsAssemblyHolder()
    {
        return TRUE;
    }

    // tg: code/modules/assembly/holder.dm:31
    public virtual async DmTask assemble(obj_item_assembly A = null, obj_item_assembly A2 = null, mob user = null)
    {
        await attach(A, user);
        await attach(A2, user);
        name = I18n.T("[]-[] assembly", A.name, A2.name);
        update_appearance();
        SSblackbox.record_feedback("tally", "assembly_made", 1, Dm.text("[]-[]", Dm.initial(A, (atom p) => p.name), Dm.initial(A2, (atom p) => p.name)));
    }

    /**
     * on_attach: Pass on_attach message to child assemblies
     *
     */
    // tg: code/modules/assembly/holder.dm:42
    public virtual async DmTask on_attach()
    {
        obj newloc = (obj)loc;
        if (!Dm.truthy(newloc.IsSpecialAssembly()) && !Dm.truthy(newloc.IsAssemblyHolder()))
            return;
        foreach (obj_item_assembly assembly in Dm.each<obj_item_assembly>(assemblies))
            await assembly.on_attach();
    }

    // tg: code/modules/assembly/holder.dm:49
    public virtual async DmTask<object> try_add_assembly(obj_item_assembly attached_assembly = null, mob user = null)
    {
        if (Dm.truthy(attached_assembly.secured))
        {
            balloon_alert(user, I18n.T("not attachable!"));
            return FALSE;
        }

        if (LAZYLEN(assemblies) >= HOLDER_MAX_ASSEMBLIES)
        {
            balloon_alert(user, I18n.T("too many assemblies!"));
            return FALSE;
        }

        if (Dm.truthy(Dm.bitand(attached_assembly.assembly_flags, ASSEMBLY_NO_DUPLICATES)))
        {
            if (Dm.locate_type(attached_assembly.type, assemblies) != null)
            {
                balloon_alert(user, I18n.T("can't attach another of that!"));
                return FALSE;
            }
        }

        await add_assembly(attached_assembly, user);
        balloon_alert(user, I18n.T("part attached"));
        return TRUE;
    }

    /**
     * Adds an assembly to the assembly holder
     *
     * This proc is used to add an assembly to the assembly holder, update the appearance, and the name of it.
     * Arguments:
     * * attached_assembly - assembly we are adding to the assembly holder
     * * user - user we pass into attach()
     */
    // tg: code/modules/assembly/holder.dm:75
    public virtual async DmTask add_assembly(obj_item_assembly attached_assembly = null, mob user = null)
    {
        await attach(attached_assembly, user);
        name = "";
        foreach (obj_item_assembly assembly in Dm.each_cast<obj_item_assembly>(assemblies))
            name += I18n.T("[]-", assembly.name);
        name = splicetext(name, length(name), length(name) + 1, "");
        name += I18n.T(" assembly");
        update_appearance();
    }

    // tg: code/modules/assembly/holder.dm:84
    public virtual async DmTask attach(obj_item_assembly A = null, mob user = null)
    {
        if (!Dm.truthy(await A.remove_item_from_storage(this, user)))
        {
            if (user != null)
                await user.transferItemToLoc(A, this);
            else
                await A.forceMove(this);
        }
        A.holder = this;
        await A.toggle_secure();
        LAZYADD(ref assemblies, A);
        A.holder_movement();
        await A.on_attach();
    }

    // tg: code/modules/assembly/holder.dm:96
    public override object update_appearance(float? updates = null)
    {
        updates ??= ALL;
        object dot = base.update_appearance(updates);
        master?.update_appearance(updates);
        return dot;
    }

    // tg: code/modules/assembly/holder.dm:100
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        for (float i = 1; i <= LAZYLEN(assemblies); i++)
        {
            if (IS_LEFT_INDEX(i))
            {
                obj_item_assembly assembly = (obj_item_assembly)assemblies[i];
                mutable_appearance left;
                if (Dm.truthy(assembly.use_base_icon_for_holder))
                {
                    left = mutable_appearance(assembly.icon, assembly.icon_state) as mutable_appearance;
                    left.transform = matrix(1, 0, -0.1f, 0, 1, 0.1f);
                }
                else
                    left = mutable_appearance(assembly.icon, Dm.text("[]_left", assembly.icon_state)) as mutable_appearance;
                Dm.append(ref dot, left);
                foreach (object left_overlay in Dm.each(assembly.attached_overlays))
                    Dm.append(ref dot, Dm.text("[]_l", left_overlay));
            }
            if (IS_RIGHT_INDEX(i))
            {
                obj_item_assembly assembly = (obj_item_assembly)assemblies[i];
                mutable_appearance right;
                if (Dm.truthy(assembly.use_base_icon_for_holder))
                {
                    right = mutable_appearance(assembly.icon, assembly.icon_state) as mutable_appearance;
                    right.transform = matrix(-1, 0, 0.1f, 0, 1, 0.1f);
                }
                else
                {
                    right = mutable_appearance(assembly.icon, Dm.text("[]_left", assembly.icon_state)) as mutable_appearance;
                    right.transform = matrix(-1, 0, 0, 0, 1, 0);
                }
                // right.add_overlay("[right_overlay]_l"): /image/proc/add_overlay(x) is `overlays |= x` (controllers/subsystem/overlays.dm:113)
                foreach (object right_overlay in Dm.each(assembly.attached_overlays))
                    Dm.combine(ref right.overlays, Dm.text("[]_l", right_overlay));
                Dm.append(ref dot, right);
            }
        }
        return dot;
    }

    // tg: code/modules/assembly/holder.dm:127
    public override async DmTask<object> on_found(mob finder = null)
    {
        foreach (obj_item_assembly assembly in Dm.each_cast<obj_item_assembly>(assemblies))
            await assembly.on_found(finder);
        return null;
    }

    // tg: code/modules/assembly/holder.dm:131
    public override async DmTask setDir(object newdir = null)
    {
        await base.setDir(newdir);
        foreach (obj_item_assembly assembly in Dm.each_cast<obj_item_assembly>(assemblies))
            assembly.holder_movement();
    }

    // tg: code/modules/assembly/holder.dm:136
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        foreach (obj_item_assembly assembly in Dm.each_cast<obj_item_assembly>(assemblies))
            await assembly.dropped(user);
        return dot;
    }

    // tg: code/modules/assembly/holder.dm:141
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra) //Perhapse this should be a holder_pickup proc instead, can add if needbe I guess
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        foreach (obj_item_assembly assembly in Dm.each_cast<obj_item_assembly>(assemblies))
            await assembly.attack_hand(user, modifiers); // Note override in assembly.dm to prevent side effects here
        return dot;
    }

    // tg: code/modules/assembly/holder.dm:148
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        // isassembly(tool) (__DEFINES/is_helpers.dm), inline (rules 11.1 (a))
        if (tool is not obj_item_assembly)
            return NONE;

        await try_add_assembly((obj_item_assembly)tool, user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/assembly/holder.dm:156
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        loc.balloon_alert(user, I18n.T("disassembled"));

        await deconstruct(TRUE);

        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/assembly/holder.dm:163
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        foreach (obj_item_assembly assembly in Dm.each_cast<obj_item_assembly>(assemblies))
        {
            await assembly.on_detach();
            LAZYREMOVE(ref assemblies, assembly);
        }
    }

    // tg: code/modules/assembly/holder.dm:168
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        add_fingerprint(user);
        if (LAZYLEN(assemblies) == 1)
        {
            balloon_alert(user, I18n.T("part missing!"));
            return null;
        }

        foreach (obj_item_assembly assembly in Dm.each_cast<obj_item_assembly>(assemblies))
            await assembly.attack_self(user);
        return null;
    }

    /**
     * this proc is used to process the activation of the assembly holder
     *
     * This proc is usually called by signalers, timers, or anything that can trigger and
     * send a pulse to the assembly holder, which then calls this proc that actually activates the assemblies
     * Arguments:
     * * /obj/device - the device we sent the pulse from which called this proc
     */
    // tg: code/modules/assembly/holder.dm:185
    public virtual object process_activation(obj device = null)
    {
        if (device == null)
            return FALSE;
        if (LAZYLEN(assemblies) >= 2)
        {
            foreach (obj_item_assembly assembly in Dm.each_cast<obj_item_assembly>(assemblies))
            {
                if (assembly != device)
                    assembly.pulsed();
            }
        }
        if (master != null)
            master.receive_signal();
        return TRUE;
    }
}
