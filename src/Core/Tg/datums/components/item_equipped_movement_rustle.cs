// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/item_equipped_movement_rustle.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps J4, D298): the riot suit's rustle.

// tg: code/datums/components/item_equipped_movement_rustle.dm:1
public partial class datum_component_item_equipped_movement_rustle : datum_component
{
    // tg: code/datums/components/item_equipped_movement_rustle.dm:22
    [DmParams("custom_sounds", "move_delay_override", "volume_override", "extrarange", "falloff_exponent", "falloff_distance")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object custom_sounds = Dm.arg(__extra, 0);
        object move_delay_override = Dm.arg(__extra, 1);
        object volume_override = Dm.arg(__extra, 2);
        object extrarange = Dm.arg(__extra, 3);
        object falloff_exponent = Dm.arg(__extra, 4);
        object falloff_distance = Dm.arg(__extra, 5);
        object dot = await base.Initialize(Dm.forward(__extra, custom_sounds, move_delay_override, volume_override, extrarange, falloff_exponent, falloff_distance));
        if (!isitem(parent))
            return COMPONENT_INCOMPATIBLE;

        RegisterSignal(parent, COMSIG_ITEM_EQUIPPED, nameof(on_equip));
        RegisterSignal(parent, COMSIG_ITEM_DROPPED, nameof(on_unequip));

        if (Dm.truthy(custom_sounds))
            rustle_sounds = Dm.str(custom_sounds);
        if (isnum(volume_override))
            volume = Dm.num(volume_override);
        if (isnum(move_delay_override))
            move_delay = Dm.num(move_delay_override);
        if (isnum(extrarange))
            sound_extra_range = Dm.num(extrarange);
        if (isnum(falloff_exponent))
            sound_falloff_exponent = Dm.num(falloff_exponent);
        if (isnum(falloff_distance))
            sound_falloff_distance = Dm.num(falloff_distance);
        return dot;
    }

    // tg: code/datums/components/item_equipped_movement_rustle.dm:44
    [SignalHandler]
    public virtual void on_equip(datum source = null, mob equipper = null, object slot = null)
    {
        obj_item our_item = parent as obj_item;
        if (!Dm.truthy(Dm.bitand(slot, our_item.slot_flags)))
            return;
        RegisterSignal(equipper, COMSIG_MOVABLE_MOVED, nameof(try_step));
    }

    // tg: code/datums/components/item_equipped_movement_rustle.dm:51
    [SignalHandler]
    public virtual void on_unequip(datum source = null, mob dropped = null, params object[] __extra)
    {
        move_counter = 0;
        UnregisterSignal(dropped, COMSIG_MOVABLE_MOVED);
    }

    // tg: code/datums/components/item_equipped_movement_rustle.dm:56
    [SignalHandler]
    public virtual void try_step(mob source = null, params object[] __extra)
    {
        if (source.moving_diagonally == FIRST_DIAG_STEP)
            return;
        move_counter++;
        if (move_counter >= move_delay)
        {
            play_rustle_sound(source);
            move_counter = 0;
        }
    }

    // tg: code/datums/components/item_equipped_movement_rustle.dm:64
    public virtual void play_rustle_sound(mob source = null)
    {
        playsound(source, rustle_sounds, volume, sound_vary, sound_extra_range, sound_falloff_exponent, falloff_distance: sound_falloff_distance);
    }
}
