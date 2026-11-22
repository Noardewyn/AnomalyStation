// Translated from tgstation (snapshot 1d99bd7f): code/modules/recycling/disposal/multiz.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (D325: multi-z disposals).

// tg: code/modules/recycling/disposal/multiz.dm:5
public partial class obj_structure_disposalpipe_trunk_multiz : obj_structure_disposalpipe_trunk
{
    ///Defines for determining which way a multiz disposal element should travel
    // tg: code/modules/recycling/disposal/multiz.dm:1
    private const float MULTIZ_PIPE_UP = 1;
    // tg: code/modules/recycling/disposal/multiz.dm:2
    private const float MULTIZ_PIPE_DOWN = 2;

    // tg: code/modules/recycling/disposal/multiz.dm:15
    public override async DmTask<object> transfer(obj_structure_disposalholder H = null)
    {
        if (Dm.eq(H.dir, DOWN)) //Since we're a trunk, you can still place a chute / bin over us. If theyve entered from there, treat this as a normal trunk
            return await base.transfer(H);

        //If we for some reason do not have a multiz dir, just like, use the default logic
        if (!Dm.truthy(multiz_dir))
            return await base.transfer(H);

        //Are we a trunk that goes up? Or down?
        turf target = get_turf(this);
        if (Dm.eq(multiz_dir, MULTIZ_PIPE_UP))
            target = GET_TURF_ABOVE(target) as turf;
        if (Dm.eq(multiz_dir, MULTIZ_PIPE_DOWN))
            target = GET_TURF_BELOW(target) as turf;
        if (target == null) //Nothing located.
            return null;

        obj_structure_disposalpipe_trunk_multiz pipe = Dm.locate<obj_structure_disposalpipe_trunk_multiz>(target);
        if (pipe == null)
            return null;
        obj_structure_disposalholder destination = Dm.@new<obj_structure_disposalholder>(pipe); //For future reference, the disposal holder is the thing that carries mobs
        await destination.merge(H); //This takes the contents of H (Our disposal holder that's travelling into us) and puts them into the destination holder
        destination.active = TRUE; //Active allows it to process and move
        await destination.setDir(DOWN); //This tells the trunk above us NOT to loop it back down to us, or else you get an infinite loop
        destination.start_moving();
        return null;
    }
}
