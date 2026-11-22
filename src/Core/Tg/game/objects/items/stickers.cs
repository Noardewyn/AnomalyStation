// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/stickers.dm, code/datums/components/sticker.dm, code/datums/storage/subtypes/lockboxes.dm (lockbox, medal), the sticker box's storage
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/game/objects/items/stickers.dm:17
public partial class obj_item_sticker : obj_item
{
    private const int MAX_STICKER_COUNT = 15;

    // tg: code/game/objects/items/stickers.dm:38
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(length(icon_states)))
            icon_state = pick(icon_states) as string;
        return dot;
    }

    // tg: code/game/objects/items/stickers.dm:44
    public override async DmTask<object> Bump(atom bumped_atom = null)
    {
        if (prob(50) && Dm.truthy(await attempt_attach(bumped_atom)))
            bumped_atom.balloon_alert_to_viewers(I18n.T("sticker landed on sticky side!"));
        return null;
    }

    // tg: code/game/objects/items/stickers.dm:48
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!isatom(interacting_with))
            return NONE;

        // DV246: tg returns NONE without a cursor position; a first-person click has none, so the sticker lands where attempt_attach picks at random
        object cursor_x = text2num(LAZYACCESS(modifiers, ICON_X));
        object cursor_y = text2num(LAZYACCESS(modifiers, ICON_Y));

        if (Dm.truthy(await attempt_attach(interacting_with, user, cursor_x, cursor_y)))
            return ITEM_INTERACT_SUCCESS;

        return NONE;
    }

    /**
     * Attempts to attach sticker to an object. Returns `FALSE` if atom has more than
     * `MAX_STICKER_COUNT` stickers, `TRUE` otherwise. If no `px` or `py` were passed
     * picks random coordinates based on a `target`'s icon.
     */
    // tg: code/game/objects/items/stickers.dm:66
    public virtual async DmTask<object> attempt_attach(atom target = null, mob user = null, object px = null, object py = null)
    {
        if (Dm.num(COUNT_TRAIT_SOURCES(target, TRAIT_STICKERED)) >= MAX_STICKER_COUNT)
        {
            balloon_alert_to_viewers(I18n.T("sticker won't stick!"));
            return FALSE;
        }

        // tg reads the size of icon(target.icon, target.icon_state); every map sprite is a tile here
        if (px == null)
            px = rand(1, ICON_SIZE_X);
        if (py == null)
            py = rand(1, ICON_SIZE_Y);

        if (user != null)
        {
            await user.do_attack_animation(target, used_item: this);
            target.balloon_alert(user, I18n.T("sticker sticked"));
            mob_living victim = target as mob_living;
            if (victim != null && victim.client != null)
            {
                user.log_message(Dm.text("stuck [] to []", this, key_name(victim)), LOG_ATTACK);
                victim.log_message(Dm.text("had [] stuck to them by []", this, key_name(user)), LOG_ATTACK);
            }
        }

        await target.AddComponent(typeof(datum_component_sticker), this, get_dir(target, this), px, py, null, null, examine_text);
        return TRUE;
    }
}

/**
 * ### Sticker component
 *
 * Component that draws supplied atom's icon over parent object with specified offset,
 * icon centering is handled inside.
 */
// tg: code/datums/components/sticker.dm:7
public partial class datum_component_sticker : datum_component
{
    // tg: code/datums/components/sticker.dm:23
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        if (!isatom(parent))
            return COMPONENT_INCOMPATIBLE;

        this.stick_callback = Dm.arg(__extra, 4) as datum_callback;
        this.peel_callback = Dm.arg(__extra, 5) as datum_callback;
        this.examine_text = Dm.arg(__extra, 6);
        await stick(Dm.arg(__extra, 0) as atom_movable, Dm.arg(__extra, 2) ?? 0, Dm.arg(__extra, 3) ?? 0);
        register_turf_signals(Dm.arg(__extra, 1) ?? NORTH);
        return null;
    }

    // tg: code/datums/components/sticker.dm:34
    public override object Destroy(float? force = null)
    {
        atom parent_atom = parent as atom;
        parent_atom.cut_overlay(sticker_overlay);

        unregister_turf_signals();

        REMOVE_TRAIT(parent, TRAIT_STICKERED, REF(this));

        our_sticker = null;
        sticker_overlay = null;
        stick_callback = null;
        peel_callback = null;
        return base.Destroy(force);
    }

    // tg: code/datums/components/sticker.dm:47
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_LIVING_IGNITED, nameof(on_ignite));
        RegisterSignal(parent, COMSIG_COMPONENT_CLEAN_ACT, nameof(on_clean));
        RegisterSignal(parent, COMSIG_ATOM_EXAMINE, nameof(on_examine));
    }

    // tg: code/datums/components/sticker.dm:52
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(COMSIG_LIVING_IGNITED, COMSIG_COMPONENT_CLEAN_ACT, COMSIG_ATOM_EXAMINE));
    }

    /// Subscribes to `COMSIG_TURF_EXPOSE` if parent atom is a turf. If turf is closed - subscribes to signal
    // tg: code/datums/components/sticker.dm:56
    public virtual void register_turf_signals(object dir = null)
    {
        if (!isturf(parent))
            return;

        listening_turf = isclosedturf(parent) ? get_step(parent as atom, Dm.num(dir)) as turf : parent as turf;
        RegisterSignal(listening_turf, COMSIG_TURF_EXPOSE, nameof(on_turf_expose));
    }

    /// Unsubscribes from `COMSIG_TURF_EXPOSE` if `listening_turf` is not `null`.
    // tg: code/datums/components/sticker.dm:64
    public virtual void unregister_turf_signals()
    {
        if (listening_turf == null)
            return;

        UnregisterSignal(listening_turf, COMSIG_TURF_EXPOSE);
    }

    // tg: code/datums/components/sticker.dm:71
    [SignalHandler]
    public virtual void sticker_gone(params object[] __extra)
    {
        UnregisterSignal(our_sticker, Dm.list(COMSIG_QDELETING, COMSIG_MOVABLE_MOVED));
        our_sticker = null;
        qdel(this);
    }

    /// Handles overlay creation from supplied atom, adds created icon to the parent object, moves source atom to the nullspace.
    // tg: code/datums/components/sticker.dm:79
    public virtual async DmTask stick(atom_movable stickering_atom = null, object px = null, object py = null)
    {
        our_sticker = stickering_atom;
        await our_sticker.moveToNullspace();
        RegisterSignals(our_sticker, Dm.list(COMSIG_QDELETING, COMSIG_MOVABLE_MOVED), nameof(sticker_gone));

        atom parent_atom = parent as atom;

        sticker_overlay = mutable_appearance(our_sticker.icon, our_sticker.icon_state, Dm.num(parent_atom.layer) + 0.1f, parent_atom, appearance_flags: Dm.bitor(RESET_COLOR, KEEP_APART)) as mutable_appearance;
        sticker_overlay.color = our_sticker.color;
        sticker_overlay.pixel_w = Dm.num(px) - ICON_SIZE_X / 2;
        sticker_overlay.pixel_z = Dm.num(py) - ICON_SIZE_Y / 2;

        parent_atom.add_overlay(sticker_overlay);
        if (stick_callback != null)
            await stick_callback.Invoke(parent);
        ADD_TRAIT(parent, TRAIT_STICKERED, REF(this));
    }

    /// Moves stickered atom from the nullspace, deletes component.
    // tg: code/datums/components/sticker.dm:95
    public virtual async DmTask peel()
    {
        atom parent_atom = parent as atom;
        atom drop_location = listening_turf ?? parent_atom.drop_location() as atom;

        UnregisterSignal(our_sticker, Dm.list(COMSIG_QDELETING, COMSIG_MOVABLE_MOVED));
        await our_sticker.forceMove(drop_location);
        our_sticker = null;
        if (peel_callback != null)
            await peel_callback.Invoke(parent);

        qdel(this);
    }

    // tg: code/datums/components/sticker.dm:106
    [SignalHandler]
    public virtual void on_ignite(datum source = null)
    {
        qdel(our_sticker); // which qdels us
    }

    // tg: code/datums/components/sticker.dm:111
    [SignalHandler]
    public virtual object on_clean(datum source = null, object clean_types = null)
    {
        Waiting.NoSleep(peel());
        return Dm.bitor(COMPONENT_CLEANED, COMPONENT_CLEANED_GAIN_XP);
    }

    // tg: code/datums/components/sticker.dm:120
    [SignalHandler]
    public virtual void on_turf_expose(datum source = null, datum_gas_mixture air = null, object exposed_temperature = null)
    {
        if (Dm.num(exposed_temperature) >= FIRE_MINIMUM_TEMPERATURE_TO_EXIST)
            qdel(our_sticker); // which qdels us
    }

    // tg: code/datums/components/sticker.dm:126
    [SignalHandler]
    public virtual void on_examine(atom source = null, mob user = null, DmList examine_list = null)
    {
        if (examine_text != null)
            examine_list.Add(span_warning(examine_text as string));
    }
}

// tg: code/game/objects/items/stickers.dm:183
public partial class obj_item_sticker_syndicate : obj_item_sticker
{
    // tg: code/game/objects/items/stickers.dm:188
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_CONTRABAND, INNATE_TRAIT);
        return dot;
    }
}

// tg: code/game/objects/items/storage/boxes/service_boxes.dm:203
public partial class obj_item_storage_box_stickers : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/service_boxes.dm:221
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (illustration == null)
        {
            illustration = pick(Dm.list("smile", "frown", "heart", "silentman", "tider", "star")) as string;
            update_appearance();
        }
        return dot;
    }

    // tg: code/game/objects/items/storage/boxes/service_boxes.dm:227
    public virtual object generate_non_contraband_stickers_list()
    {
        DmList allowed_stickers = Dm.list();

        foreach (object sticker_type in Dm.each(subtypesof(typeof(obj_item_sticker))))
        {
            if (!Dm.truthy(Dm.initial(sticker_type, nameof(obj_item_sticker.exclude_from_random))))
                allowed_stickers.Add(sticker_type);
        }

        return allowed_stickers;
    }

    // tg: code/game/objects/items/storage/boxes/service_boxes.dm:235
    public override async DmTask PopulateContents()
    {
        DmList non_contraband = generate_non_contraband_stickers_list() as DmList; // tg caches it in a static, the list never changes

        float count = rand(4, 8);
        for (int i = 1; i <= count; i++)
        {
            Type picked = pick(non_contraband) as Type;
            Dm.@new(picked, this);
        }
    }
}

// tg: code/datums/storage/subtypes/boxes.dm:110
public partial class datum_storage_box_stickers : datum_storage_box
{
    // tg: code/datums/storage/subtypes/boxes.dm:116
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(typeof(obj_item_sticker));
    }
}
