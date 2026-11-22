// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/boomerang.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7x slice E (the OZtek boomerang): the whole file. The var block is generated (D111).

///The cooldown period between last_boomerang_throw and its methods of implementing a rebound proc.
// tg: code/datums/components/boomerang.dm:7  #define BOOMERANG_REBOUND_INTERVAL (1 SECONDS)

/**
 * If an ojvect is given the boomerang component, it should be thrown back to the thrower after either hitting its target, or landing on the thrown tile.
 * Thrown objects should be thrown back to the original thrower with this component, a number of tiles defined by boomerang_throw_range.
 */
// tg: code/datums/components/boomerang.dm:7
public partial class datum_component_boomerang : datum_component
{
    // tg: code/datums/components/boomerang.dm:2  (a file-local define)
    private const float BOOMERANG_REBOUND_INTERVAL = 1 * SECONDS;

    // tg: code/datums/components/boomerang.dm:17
    [DmParams("boomerang_throw_range", "thrower_easy_catch_enabled", "examine_message")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object boomerang_throw_range = Dm.arg(__extra, 0);
        object thrower_easy_catch_enabled = Dm.arg(__extra, 1);
        object examine_message = Dm.arg(__extra, 2);
        object dot = await base.Initialize(Dm.forward(__extra, boomerang_throw_range, thrower_easy_catch_enabled, examine_message));
        if (!isitem(parent)) //Only items support being thrown around like a boomerang, feel free to make this apply to humans later on.
            return COMPONENT_INCOMPATIBLE;

        //Assignments
        if (Dm.truthy(boomerang_throw_range))
            this.boomerang_throw_range = Dm.num(boomerang_throw_range);
        if (Dm.truthy(thrower_easy_catch_enabled))
            this.thrower_easy_catch_enabled = Dm.num(thrower_easy_catch_enabled);
        if (Dm.truthy(examine_message))
            this.examine_message = examine_message;
        return dot;
    }

    // tg: code/datums/components/boomerang.dm:30
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_MOVABLE_POST_THROW, nameof(prepare_throw)); //Collect data on current thrower and the throwing datum
        RegisterSignal(parent, COMSIG_MOVABLE_THROW_LANDED, nameof(return_missed_throw));
        RegisterSignal(parent, COMSIG_MOVABLE_IMPACT, nameof(return_hit_throw));
        if (Dm.truthy(examine_message))
            RegisterSignal(parent, COMSIG_ATOM_EXAMINE, nameof(on_examine));
    }

    // tg: code/datums/components/boomerang.dm:37
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(COMSIG_MOVABLE_POST_THROW, COMSIG_MOVABLE_THROW_LANDED, COMSIG_MOVABLE_IMPACT, COMSIG_ATOM_EXAMINE));
    }

    /**
     * Proc'd before the first thrown is performed in order to gather information regarding each throw as well as handle throw_mode as necessary.
     * * source: Datum src from original signal call.
     * * thrown_thing: The thrownthing datum from the parent object's latest throw. Updates thrown_boomerang.
     * * spin: Carry over from POST_THROW, the speed of rotation on the boomerang when thrown.
     */
    // tg: code/datums/components/boomerang.dm:46
    [SignalHandler]
    public virtual void prepare_throw(datum source = null, datum_thrownthing throwingdatum = null, object spin = null)
    {
        mob thrower = throwingdatum?.get_thrower() as mob;
        if (Dm.truthy(thrower_easy_catch_enabled) && iscarbon(thrower))
        {
            mob_living_carbon carbon_mob = thrower as mob_living_carbon;
            carbon_mob.throw_mode_on(THROW_MODE_TOGGLE);
        }
    }

    // tg: code/datums/components/boomerang.dm:53
    [SignalHandler]
    public virtual void on_examine(datum source = null, mob user = null, DmList examine_list = null)
    {
        Dm.append(ref examine_list, examine_message);
    }

    /**
     * Proc that triggers when the thrown boomerang hits an object.
     * * source: Datum src from original signal call.
     * * hit_atom: The atom that has been hit by the boomerang component.
     * * init_throwing_datum: The thrownthing datum that originally impacted the object, that we use to build the new throwing datum for the rebound.
     */
    // tg: code/datums/components/boomerang.dm:63
    [SignalHandler]
    public virtual void return_hit_throw(datum source = null, atom hit_atom = null, datum_thrownthing init_throwing_datum = null, object caught = null)
    {
        if (!(last_boomerang_throw <= world.time) || Dm.truthy(caught))
            return;
        aerodynamic_swing(init_throwing_datum);
    }

    /**
     * Proc that triggers when the thrown boomerang does not hit a target.
     * * source: Datum src from original signal call.
     * * throwing_datum: The thrownthing datum that originally impacted the object, that we use to build the new throwing datum for the rebound.
     */
    // tg: code/datums/components/boomerang.dm:74
    [SignalHandler]
    public virtual void return_missed_throw(datum source = null, datum_thrownthing throwingdatum = null)
    {
        if (!(last_boomerang_throw <= world.time))
            return;
        aerodynamic_swing(throwingdatum);
    }

    /**
     * Proc that triggers when the thrown boomerang has been fully thrown, rethrowing the boomerang back to the thrower, and producing visible feedback.
     * * throwing_datum: The thrownthing datum that originally impacted the object, that we use to build the new throwing datum for the rebound.
     * * hit_atom: The atom that has been hit by the boomerang'd object.
     */
    // tg: code/datums/components/boomerang.dm:85
    public virtual void aerodynamic_swing(datum_thrownthing throwingdatum = null)
    {
        mob thrown_by = throwingdatum?.get_thrower() as mob;
        obj_item true_parent = parent as obj_item;
        if (thrown_by != null)
        {
            _addtimer(Dm.@new<datum_callback>(true_parent, nameof(atom_movable.throw_at), thrown_by, boomerang_throw_range, throwingdatum.speed, thrown_by, TRUE), 0.1f * SECONDS, file: "code/datums/components/boomerang.dm", line: 88);
            last_boomerang_throw = world.time + BOOMERANG_REBOUND_INTERVAL;
        }
        true_parent.visible_message(
            span_danger(I18n.T("[] is flying back at []!", true_parent, thrown_by)),
            span_danger(I18n.T("You see [] fly back at you!", true_parent)),
            span_hear(I18n.T("You hear an aerodynamic woosh!"))
        );
    }
}
