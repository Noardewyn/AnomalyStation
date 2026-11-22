// Translated from tgstation (snapshot 1d99bd7f): code/modules/power/singularity/containment_field.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E7 (singularity containment): the whole file. The abstract field (the shock that throws a living thing back, the
// bump that repels a machine or a structure) and the containment field two field generators make between them: it blocks the
// singularity and the energy ball (COMSIG_ATOM_SINGULARITY_TRY_MOVE and its turf's TRAIT_CONTAINMENT_FIELD), the air and
// explosions, and shocks whoever walks into it or touches it. blob_act stays in docs/ai/specs/live-partial.txt (blobs). The var
// blocks are generated (D111).

// tg: code/modules/power/singularity/containment_field.dm:4
public partial class obj_machinery_field_containment : obj_machinery_field
{
    // tg: code/modules/power/singularity/containment_field.dm:23
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        Waiting.NoSleep(AddElement(typeof(datum_element_blocks_explosives)));
        dot = base.Initialize(mapload, __extra);
        air_update_turf(TRUE, TRUE);
        RegisterSignal(this, COMSIG_ATOM_SINGULARITY_TRY_MOVE, nameof(block_singularity));
        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), Initialize__loc_connections));
        Waiting.NoSleep(AddElement(typeof(datum_element_give_turf_traits), string_list(Dm.list(TRAIT_CONTAINMENT_FIELD))));
        return dot;
    }

    // tg: code/modules/power/singularity/containment_field.dm:28  var/static/list/loc_connections (a proc-level static, rules 10.6)
    private static ref DmList Initialize__loc_connections => ref TgState.Current.obj_machinery_field_containment__Initialize__loc_connections;

    // tg: code/modules/power/singularity/containment_field.dm:34
    public override object Destroy(float? force = null)
    {
        if (field_gen_1 != null)
        {
            Dm.remove(ref field_gen_1.fields, this);
            field_gen_1 = null;
        }
        if (field_gen_2 != null)
        {
            Dm.remove(ref field_gen_2.fields, this);
            field_gen_2 = null;
        }
        can_atmos_pass = ATMOS_PASS_YES;
        air_update_turf(TRUE, FALSE);
        return base.Destroy(force);
    }

    //ATTACK HAND IGNORING PARENT RETURN VALUE
    // tg: code/modules/power/singularity/containment_field.dm:46
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (get_dist(this, user) > 1)
            return FALSE;
        else
        {
            await yeet_shock(user as mob_living);
            return TRUE;
        }
    }

    // tg: code/modules/power/singularity/containment_field.dm:53
    public override async DmTask<object> attackby(obj_item attacking_item = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        await yeet_shock(user as mob_living);
        return TRUE;
    }

    // tg: code/modules/power/singularity/containment_field.dm:57
    public override object play_attack_sound(object damage_amount = null, string damage_type = null, object damage_flag = null, params object[] __extra)
    {
        damage_type ??= BRUTE;
        damage_flag ??= 0;
        switch (damage_type)
        {
            case BURN:
                playsound(loc, Dm.resource("sound/effects/empulse.ogg"), 75, TRUE);
                break;
            case BRUTE:
                playsound(loc, Dm.resource("sound/effects/empulse.ogg"), 75, TRUE);
                break;
        }
        return null;
    }

    // tg: code/modules/power/singularity/containment_field.dm:67
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        return FALSE;
    }

    // tg: code/modules/power/singularity/containment_field.dm:70
    public override async DmTask<object> attack_animal(mob user = null, DmList modifiers = null)
    {
        if (field_gen_1 == null || field_gen_2 == null)
        {
            qdel(this);
            return null;
        }
        if (ismegafauna(user))
        {
            user.visible_message(span_warning(I18n.T("[] glows fiercely as the containment field flickers out!", user)));
            field_gen_1.calc_power(INFINITY); //rip that 'containment' field
            await ((mob_living)user).adjust_brute_loss(-((mob_living_simple_animal)user).obj_damage);
        }
        else
            return await base.attack_animal(user, modifiers);
        return null;
    }

    // tg: code/modules/power/singularity/containment_field.dm:81
    [SignalHandler]
    public virtual void on_entered(datum source = null, atom_movable considered_atom = null)
    {
        if (isliving(considered_atom))
        {
            mob_living living_moving_through_field = (mob_living)considered_atom;
            if (!Dm.truthy(living_moving_through_field.incorporeal_move))
                Waiting.NoSleep(yeet_shock(living_moving_through_field));
        }

        if (ismachinery(considered_atom) || isstructure(considered_atom) || ismecha(considered_atom))
            Waiting.NoSleep(bump_field(considered_atom));
    }

    // tg: code/modules/power/singularity/containment_field.dm:91
    public virtual object set_master(object master1 = null, object master2 = null)
    {
        if (!Dm.truthy(master1) || !Dm.truthy(master2))
            return FALSE;
        field_gen_1 = (obj_machinery_field_generator)master1;
        field_gen_2 = (obj_machinery_field_generator)master2;
        return TRUE;
    }

    // tg: code/modules/power/singularity/containment_field.dm:98
    [SignalHandler]
    public virtual object block_singularity()
    {
        return SINGULARITY_TRY_MOVE_BLOCK;
    }

    // tg: code/modules/power/singularity/containment_field.dm:103
    public override async DmTask<object> yeet_shock(mob_living user = null)
    {
        if (field_gen_1 == null || field_gen_2 == null)
        {
            qdel(this);
            return FALSE;
        }
        await base.yeet_shock(user);
        return null;
    }

    // tg: code/modules/power/singularity/containment_field.dm:109
    public override async DmTask<object> Move(atom newloc = null, object direction = null, float? glide_size_override = null, float? update_dir = null)
    {
        qdel(this);
        return FALSE;
    }

    // tg: code/modules/power/singularity/containment_field.dm:64
    public override async DmTask<object> blob_act(obj_structure_blob attacking_blob = null)
    {
        return FALSE;
    }
}

// Abstract Field Class
// Used for overriding certain procs

// tg: code/modules/power/singularity/containment_field.dm:117
public partial class obj_machinery_field : obj_machinery
{
    // tg: code/modules/power/singularity/containment_field.dm:121
    public override object Bumped(atom_movable bumped_atom = null)
    {
        atom_movable mover = bumped_atom;
        if (Dm.truthy(has_shocked))
            return null;
        if (isliving(mover))
        {
            Waiting.NoSleep(yeet_shock((mob_living)mover));
            return null;
        }
        if (ismachinery(mover) || isstructure(mover) || isvehicle(mover))
        {
            Waiting.NoSleep(bump_field(mover));
            return null;
        }
        return null;
    }

    // tg: code/modules/power/singularity/containment_field.dm:132
    public override async DmTask<object> CanAllowThrough(atom_movable mover = null, object border_dir = null)
    {
        object dot = null;
        dot = await base.CanAllowThrough(mover, border_dir);
        if (Dm.truthy(has_shocked) || isliving(mover) || ismachinery(mover) || isstructure(mover) || ismecha(mover))
            return FALSE;
        return dot;
    }

    // tg: code/modules/power/singularity/containment_field.dm:137
    public virtual async DmTask<object> yeet_shock(mob_living user = null)
    {
        float shock_damage = min(rand(30, 40), rand(30, 40));

        if (iscarbon(user))
        {
            await user.Paralyze(10 * SECONDS);
            await user.electrocute_act(shock_damage, this, 1);
        }

        else if (issilicon(user))
        {
            if (prob(20))
                await user.Stun(40);
            await user.take_overall_damage(burn: shock_damage);
            user.visible_message(span_danger(I18n.T("[] is shocked by \\the []!", user.name, this)),
                span_userdanger(I18n.T("Energy pulse detected, system damaged!")),
                span_hear(I18n.T("You hear an electrical crack.")));
        }

        await user.updatehealth();
        await bump_field(user);
        return null;
    }

    // tg: code/modules/power/singularity/containment_field.dm:155
    public virtual void clear_shock()
    {
        has_shocked = FALSE;
    }

    // tg: code/modules/power/singularity/containment_field.dm:158
    public virtual async DmTask<object> bump_field(atom_movable considered_atom = null)
    {
        if (Dm.truthy(has_shocked))
            return FALSE;
        has_shocked = TRUE;
        do_sparks(5, TRUE, considered_atom.loc);
        atom target = get_edge_target_turf(considered_atom, get_dir(this, get_step_away(considered_atom, this))) as atom;
        if (isliving(considered_atom))
            to_chat(considered_atom, span_userdanger(I18n.T("The field repels you with tremendous force!")));
        playsound(this, Dm.resource("sound/effects/gravhit.ogg"), 50, TRUE);
        await considered_atom.throw_at(target, 200, 4);
        _addtimer(Dm.@new<datum_callback>(this, nameof(clear_shock)), 0.5f * SECONDS, file: "code/modules/power/singularity/containment_field.dm", line: 168);
        return null;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/power/singularity/containment_field.dm:28  var/static/list/loc_connections = list(COMSIG_ATOM_ENTERED = PROC_REF(on_entered))
    internal DmList obj_machinery_field_containment__Initialize__loc_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_ENTERED, nameof(obj_machinery_field_containment.on_entered)));
}
