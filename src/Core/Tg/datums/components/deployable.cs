// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/deployable.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: an item used in hand deploys its object on the tile in front of its user after a delay (the folded heavy machine
// gun's turret, game/machinery/deployable.dm). The var block is generated; thing_to_be_deployed holds the deployed object's typepath
// (the converter's TypeHoldingVars).

/**
 * Deployable - Bring your big guns with you, and smack em' down where you want.
 *
 * Allows items to spawn other items (usually objects) in front of the user after a short delay.
 * If attaching this to something:
 * Set deploy_time to a number in seconds for the deploy delay
 * Set thing_to_be_deployed to an obj path for the thing that gets spawned
 * Multiple deployments and deployments work together to allow a thing to be placed down several times. If multiple deployments is false then don't worry about deployments
 * Direction setting true means the object spawned will face the direction of the person who deployed it, false goes to the default direction
 */

// tg: code/datums/components/deployable.dm:12
public partial class datum_component_deployable : datum_component
{
    // tg: code/datums/components/deployable.dm:29
    [DmParams("deploy_time", "thing_to_be_deployed", "multiple_deployments", "deployments", "add_description_hint", "direction_setting")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object deploy_time = Dm.arg(__extra, 0);
        Type thing_to_be_deployed = Dm.arg(__extra, 1) as Type;
        object multiple_deployments = Dm.arg(__extra, 2);
        object deployments = Dm.arg(__extra, 3);
        object add_description_hint = Dm.arg(__extra, 4);
        object direction_setting = Dm.arg(__extra, 5);
        deploy_time ??= 5 * SECONDS;
        multiple_deployments ??= FALSE;
        deployments ??= 1;
        add_description_hint ??= TRUE;
        direction_setting ??= TRUE;
        object dot = await base.Initialize(Dm.forward(__extra, deploy_time, thing_to_be_deployed, multiple_deployments, deployments, add_description_hint, direction_setting));
        if (!isitem(parent))
            return COMPONENT_INCOMPATIBLE;

        this.deploy_time = deploy_time;
        this.thing_to_be_deployed = thing_to_be_deployed;
        this.add_description_hint = add_description_hint;
        this.direction_setting = direction_setting;
        this.deployments = deployments;
        this.multiple_deployments = multiple_deployments;

        if (Dm.truthy(add_description_hint))
            RegisterSignal(parent, COMSIG_ATOM_EXAMINE, nameof(examine));
        RegisterSignal(parent, COMSIG_ITEM_ATTACK_SELF, nameof(on_attack_hand));

        Type typecast = thing_to_be_deployed;
        deployed_name = Dm.initial(typecast, (obj_item p) => p.name);
        return dot;
    }

    // tg: code/datums/components/deployable.dm:48
    [SignalHandler]
    public virtual void examine(datum source = null, mob user = null, DmList examine_list = null)
    {
        Dm.append(ref examine_list, span_notice(I18n.T("It can be used <b>in hand</b> to deploy into [] [].",
            Dm.gt(deployments, 1) && Dm.truthy(multiple_deployments) ? Dm.text("[]", deployments) : I18n.T("a"), deployed_name)));
    }

    // tg: code/datums/components/deployable.dm:52
    [SignalHandler]
    public virtual void on_attack_hand(datum source = null, mob user = null, object location = null, object direction = null)
    {
        Waiting.Detach(deploy(source as obj, user, location, direction)); // INVOKE_ASYNC
    }

    // tg: code/datums/components/deployable.dm:56
    public virtual async DmTask deploy(obj source = null, mob user = null, object location = null, object direction = null) //If there's no user, location and direction are used
    {
        // The object we are going to create
        atom deployed_object;
        // The turf our object is going to be deployed to
        turf deploy_location;
        // What direction will the deployed object be placed facing
        object new_direction;

        if (Dm.truthy(user))
        {
            deploy_location = get_step(user, user.dir) as turf; //Gets spawn location for thing_to_be_deployed if there is a user
            if (Dm.truthy(await deploy_location.is_blocked_turf(TRUE, parent)))
            {
                source.balloon_alert(user, I18n.T("insufficient room to deploy here."));
                return;
            }
            new_direction = user.dir; //Gets the direction for thing_to_be_deployed if there is a user
            source.balloon_alert(user, I18n.T("deploying..."));
            playsound(source, Dm.resource("sound/items/tools/ratchet.ogg"), 50, TRUE);
            if (!Dm.truthy(await do_after(user, deploy_time)))
                return;
        }
        else // If there is for some reason no user, then the location and direction are set here
        {
            deploy_location = location as turf;
            new_direction = direction;
        }

        deployed_object = Dm.@new(thing_to_be_deployed, deploy_location) as atom;
        if (Dm.truthy(direction_setting))
        {
            await deployed_object.setDir(new_direction);
            await deployed_object.update_icon_state();
        }

        deployments = Dm.sub(deployments, 1);

        if (!Dm.truthy(multiple_deployments) || Dm.lt(deployments, 1))
            qdel(source);
    }
}
