// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/secure_safe.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10 slice G2 (D274): the whole file (the captain's spare ID safe on the bridge, the wall safes and their frame). The rust
// heretic's act on the spare's safe came with gaps ST5. The var blocks are generated (D111).

// tg: code/game/objects/structures/secure_safe.dm:1
public partial class obj_item_wallframe_secure_safe : obj_item_wallframe
{
    // tg: code/game/objects/structures/secure_safe.dm:20
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(create_storage(
            max_specific_storage: WEIGHT_CLASS_GIGANTIC,
            max_total_storage: 20));
        if (Dm.truthy(stored_lock_code) && !Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            atom_storage.set_locked(STORAGE_FULLY_LOCKED);
        update_appearance();
        return dot;
    }

    // tg: code/game/objects/structures/secure_safe.dm:29
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            icon_state = Dm.text("[]_broken", base_icon_state);
        else
            icon_state = Dm.text("[][]", base_icon_state, Dm.truthy(stored_lock_code) ? "_locked" : null);
        return dot;
    }

    // tg: code/game/objects/structures/secure_safe.dm:38
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        await base.emag_act(user, emag_card, __extra);
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;

        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        visible_message(span_warning(I18n.T("Sparks fly from []!", this)), blind_message: span_hear(I18n.T("You hear a faint electrical spark.")));
        balloon_alert(user, "lock destroyed");
        playsound(this, SFX_SPARKS, 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        stored_lock_code = null;
        atom_storage.locked = STORAGE_NOT_LOCKED;
        update_appearance();
        return TRUE;
    }

    // tg: code/game/objects/structures/secure_safe.dm:52
    public override void after_attach(obj attached_to = null)
    {
        if (attached_to is not obj_structure_secure_safe safe)
        {
            base.after_attach(attached_to);
            return;
        }

        foreach (obj_item item in Dm.each<obj_item>(contents.Copy()))
            Waiting.NoSleep(item.forceMove(safe));

        var storage_component = safe.GetComponent(typeof(datum_component_lockable_storage)) as datum_component_lockable_storage;
        if (Dm.truthy(stored_lock_code))
            storage_component?.set_lock_code(stored_lock_code);

        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            storage_component?.break_lock();

        base.after_attach(attached_to);
    }
}

/**
 * Wall safes
 * Holds items and uses the lockable storage component
 * to allow people to lock items up.
 */
// tg: code/game/objects/structures/secure_safe.dm:81
public partial class obj_structure_secure_safe : obj_structure
{
    // tg: code/game/objects/structures/secure_safe.dm:101
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        //this will create the storage for us.
        Waiting.NoSleep(AddComponent(typeof(datum_component_lockable_storage), stored_lock_code));
        if (Dm.truthy(mapload))
        {
            Waiting.NoSleep(PopulateContents());
            Waiting.NoSleep(find_and_mount_on_atom());
        }
        RegisterSignal(this, COMSIG_LOCKABLE_STORAGE_SET_CODE, nameof(update_lock_code));
        return dot;
    }

    // tg: code/game/objects/structures/secure_safe.dm:110
    public override async DmTask<object> find_and_mount_on_atom(float? mark_for_late_init = null, float? late_init = null)
    {
        if (!Dm.truthy(density))
            return await base.find_and_mount_on_atom(mark_for_late_init, late_init);
        return null;
    }

    // tg: code/game/objects/structures/secure_safe.dm:114
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        if (!Dm.truthy(density)) //if we're a wall item, we'll drop a wall frame.
        {
            var new_safe = Dm.@new<obj_item_wallframe_secure_safe>(get_turf(this));

            // Transfer lock code to the wallframe
            var storage_component = GetComponent(typeof(datum_component_lockable_storage)) as datum_component_lockable_storage;
            if (storage_component != null)
                new_safe.stored_lock_code = storage_component.lock_code;

            if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
                new_safe.obj_flags = Dm.bitor(new_safe.obj_flags, EMAGGED);

            new_safe.update_appearance();

            foreach (obj_item item in Dm.each<obj_item>(contents.Copy()))
                await item.forceMove(new_safe);
        }
    }

    // tg: code/game/objects/structures/secure_safe.dm:132
    public virtual async DmTask PopulateContents()
    {
        Dm.@new<obj_item_paper>(this);
        Dm.@new<obj_item_pen>(this);
        await DmTask.CompletedTask;
    }

    // tg: code/game/objects/structures/secure_safe.dm:136
    [SignalHandler]
    public virtual void update_lock_code(obj_structure_secure_safe safe = null, object new_code = null)
    {
        stored_lock_code = new_code;
    }

    // tg: code/game/objects/structures/secure_safe.dm:141
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        if (Dm.num(severity) <= EXPLODE_LIGHT)
            return FALSE;
        return base.ex_act(severity, target, __extra);
    }
}

// tg: code/game/objects/structures/secure_safe.dm:158
public partial class obj_structure_secure_safe_caps_spare : obj_structure_secure_safe
{
    // tg: code/game/objects/structures/secure_safe.dm:183
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        atom_storage.set_holdable(typeof(obj_item_card_id));
        Waiting.NoSleep(AddComponent(typeof(datum_component_lockable_storage), SSid_access?.spare_id_safe_code, FALSE)); // lock_code, can_hack_open (a host-built room without the Master has no SSid_access)
        return dot;
    }

    // tg: code/game/objects/structures/secure_safe.dm:191
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_card_id_advanced_gold_captains_spare>(this);
        await DmTask.CompletedTask;
    }
}

// Gaps ST5 (D333): the rust heretic's touch on the captain's spare safe.
public partial class obj_structure_secure_safe_caps_spare : obj_structure_secure_safe
{
    // tg: code/game/objects/structures/secure_safe.dm:199
    public override async DmTask<object> rust_heretic_act(object rust_strength = null)
    {
        await take_damage(damage_amount: 100, damage_type: BRUTE, damage_flag: MELEE, armour_penetration: 100);
        return TRUE;
    }
}
