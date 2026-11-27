// Translated from tgstation (snapshot 1d99bd7f): code/modules/mapfluff/ruins/lavalandruin_code/biodome_winter.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the winter biodome's freeze cube, which freezes what it hits and comes back. The var blocks are generated.

// tg: code/modules/mapfluff/ruins/lavalandruin_code/biodome_winter.dm:6
public partial class obj_item_freeze_cube : obj_item
{
    // tg: code/modules/mapfluff/ruins/lavalandruin_code/biodome_winter.dm:17
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        Dm.append(ref dot, span_notice(I18n.T("Throw this at objects or creatures to freeze them, it will boomerang back so be cautious!")));
        return dot;
    }

    // tg: code/modules/mapfluff/ruins/lavalandruin_code/biodome_winter.dm:21
    public override async DmTask<object> throw_at(atom target = null, object range = null, object speed = null, atom thrower = null, float? spin = null, float? diagonals_first = null, datum_callback callback = null, float? force = null, float? gentle = null, object quickstart = null, Type throw_datum_typepath = null, params object[] __extra)
    {
        spin ??= 1;
        diagonals_first ??= 0;
        quickstart ??= TRUE;
        throw_datum_typepath ??= typeof(datum_thrownthing);
        object dot = await base.throw_at(target, range, speed, thrower, spin, diagonals_first, callback, force, gentle, quickstart, throw_datum_typepath, __extra);
        if (!Dm.truthy(dot))
            return dot;
        icon_state = "freeze_cube_thrown";
        // VARSET_CALLBACK(src, icon_state, initial(icon_state)): ___callbackvarset is not declared (vars is not translated, rules 10.7),
        // so the timer calls icon_state__varset (as urinal.cs's)
        _addtimer(Dm.@new<datum_callback>(this, nameof(icon_state__varset), Dm.initial(this, (obj_item_freeze_cube f) => f.icon_state)), 1 * SECONDS, file: "code/modules/mapfluff/ruins/lavalandruin_code/biodome_winter.dm", line: 26);
        return dot;
    }

    // tg: code/modules/mapfluff/ruins/lavalandruin_code/biodome_winter.dm:28
    public override object throw_impact(atom hit_atom = null, datum_thrownthing throwingdatum = null, params object[] __extra)
    {
        icon_state = Dm.initial(this, (obj_item_freeze_cube f) => f.icon_state);
        object caught = Waiting.NoSleep(hit_atom.hitby(this, FALSE, FALSE, throwingdatum: throwingdatum));
        var thrown_by = throwingdatum?.get_thrower() as mob;
        if (ismovable(hit_atom) && !Dm.truthy(caught) && (thrown_by == null || (thrown_by != null && freeze_cooldown <= world.time)))
            freeze_hit_atom((atom_movable)hit_atom);
        if (thrown_by != null && !Dm.truthy(caught))
            _addtimer(Dm.@new<datum_callback>(this, nameof(throw_at), thrown_by, throw_range + 2, throw_speed, null, TRUE), 0.1f * SECONDS, file: "code/modules/mapfluff/ruins/lavalandruin_code/biodome_winter.dm", line: 35);
        return null;
    }

    // tg: code/modules/mapfluff/ruins/lavalandruin_code/biodome_winter.dm:37
    public virtual void freeze_hit_atom(atom_movable hit_atom = null)
    {
        playsound(this, Dm.resource("sound/effects/glass/glassbr3.ogg"), 50, TRUE);
        freeze_cooldown = world.time + Dm.num(cooldown_time); // COOLDOWN_START(src, freeze_cooldown, cooldown_time)
        if (hit_atom is obj hit_object)
        {
            object success = Waiting.NoSleep(hit_object.freeze());
            if (!Dm.truthy(success) && Dm.truthy(Dm.bitand(hit_object.resistance_flags, FREEZE_PROOF)))
                hit_object.visible_message(span_warning(I18n.T("[] is freeze-proof!", hit_object)));
        }
        else if (hit_atom is mob_living hit_mob)
        {
            GLOB.move_manager.stop_looping(hit_mob); //stops them mid pathing even if they're stunimmune
            Waiting.NoSleep(hit_mob.apply_status_effect(typeof(datum_status_effect_ice_block_talisman), 3 * SECONDS));
        }
    }

    // tg: code/__HELPERS/varset_callback.dm:3  VARSET_CALLBACK(src, icon_state, value)
    public virtual void icon_state__varset(object value = null) => icon_state = value as string;
}
