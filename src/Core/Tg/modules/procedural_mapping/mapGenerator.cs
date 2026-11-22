// Translated from tgstation (snapshot 1d99bd7f): code/modules/procedural_mapping/mapGenerator.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The generator base the areas' cave generators build on (D304); since gaps S4d (D327) the generator modules too (the mass delete
// that clears the Mafia map). The debug admin verb (:139) is not ported (admin tools, D88).

// tg: code/modules/procedural_mapping/mapGenerator.dm:3
[DmPath("/datum/map_generator")]
public partial class datum_map_generator : datum
{
    // tg: code/modules/procedural_mapping/mapGenerator.dm:13
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (buildmode_name == "Undocumented")
            buildmode_name = copytext_char(Dm.text("[]", type), 20); // / d a t u m / m a p g e n e r a t o r / = 20 characters.
        initialiseModules();
    }

    // tg: code/modules/procedural_mapping/mapGenerator.dm:19
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        QDEL_LIST(modules);
        return dot;
    }

    ///This proc will be ran by areas on Initialize, and provides the areas turfs as argument to allow for generation.
    // tg: code/modules/procedural_mapping/mapGenerator.dm:24
    public virtual async DmTask generate_terrain(DmList turfs = null, area generate_in = null)
    {
        await DmTask.CompletedTask;
    }

    /// Populate terrain with flora, fauna, features and basically everything that isn't a turf.
    // tg: code/modules/procedural_mapping/mapGenerator.dm:28
    public virtual async DmTask populate_terrain(DmList turfs = null, area generate_in = null)
    {
        await DmTask.CompletedTask;
    }

    //Defines the region the map represents, sets map
    //Returns the map
    // tg: code/modules/procedural_mapping/mapGenerator.dm:33
    public virtual object defineRegion(turf Start = null, turf End = null, object replace = null)
    {
        replace ??= 0;
        if (!Dm.truthy(checkRegion(Start, End)))
            return 0;

        if (Dm.truthy(replace))
            undefineRegion();
        Dm.combine(ref map, block(Start, End));
        return map;
    }

    //Defines the region the map represents, as a CIRCLE!, sets map
    //Returns the map
    // tg: code/modules/procedural_mapping/mapGenerator.dm:45
    public virtual object defineCircularRegion(turf Start = null, turf End = null, object replace = null)
    {
        replace ??= 0;
        if (!Dm.truthy(checkRegion(Start, End)))
            return 0;

        float centerX = Dm.max(Math.Abs((End.x + Start.x) / 2f), 1);
        float centerY = Dm.max(Math.Abs((End.y + Start.y) / 2f), 1);

        float lilZ = Dm.min(Start.z, End.z);
        float bigZ = Dm.max(Start.z, End.z);

        float sphereMagic = Dm.max(Math.Abs(bigZ - (lilZ / 2f)), 1); //Spherical maps! woo!

        float radius = Math.Abs(Dm.max(centerX, centerY)); //take the biggest displacement as the radius

        if (Dm.truthy(replace))
            undefineRegion();

        //Even sphere correction engage
        float offByOneOffset = 1;
        if (bigZ % 2 == 0)
            offByOneOffset = 0;

        for (float i = lilZ; i <= bigZ + offByOneOffset; i++)
        {
            float theRadius = radius;
            if (i != sphereMagic)
                theRadius = Dm.max(radius / Dm.max(2 * Math.Abs(sphereMagic - i), 1), 1);

            Dm.combine(ref map, circle_range(locate(centerX, centerY, i), theRadius));
        }

        return map;
    }

    //Empties the map list, he's dead jim.
    // tg: code/modules/procedural_mapping/mapGenerator.dm:80
    public virtual void undefineRegion()
    {
        map = Dm.list(); //bai bai
    }

    //Checks for and Rejects bad region coordinates
    //Returns 1/0
    // tg: code/modules/procedural_mapping/mapGenerator.dm:86
    public virtual object checkRegion(turf Start = null, turf End = null)
    {
        if (Start == null || End == null)
            return FALSE; //Just bail

        if (Start.x > Dm.world.maxx || End.x > Dm.world.maxx)
            return FALSE;
        if (Start.y > Dm.world.maxy || End.y > Dm.world.maxy)
            return FALSE;
        if (Start.z > Dm.world.maxz || End.z > Dm.world.maxz)
            return FALSE;
        return TRUE;
    }

    //Requests the mapGeneratorModule(s) to (re)generate
    // tg: code/modules/procedural_mapping/mapGenerator.dm:100
    public virtual void generate()
    {
        syncModules();
        if (modules == null || !Dm.truthy(length(modules)))
            return;
        foreach (datum_map_generator_module mod in Dm.each_cast<datum_map_generator_module>(modules))
            Waiting.Detach(mod.generate()); // INVOKE_ASYNC
    }

    //Requests the mapGeneratorModule(s) to (re)generate this one turf
    // tg: code/modules/procedural_mapping/mapGenerator.dm:109
    public virtual void generateOneTurf(turf T = null)
    {
        if (T == null)
            return;
        syncModules();
        if (modules == null || !Dm.truthy(length(modules)))
            return;
        foreach (datum_map_generator_module mod in Dm.each_cast<datum_map_generator_module>(modules))
            Waiting.Detach(mod.place(T)); // INVOKE_ASYNC
    }

    //Replaces all paths in the module list with actual module datums
    // tg: code/modules/procedural_mapping/mapGenerator.dm:120
    public virtual void initialiseModules()
    {
        foreach (object path in Dm.each(modules))
        {
            if (ispath(path))
            {
                modules.Remove(path);
                Dm.combine(ref modules, Dm.@new(path as Type));
            }
        }
        syncModules();
    }

    //Sync mapGeneratorModule(s) to mapGenerator
    // tg: code/modules/procedural_mapping/mapGenerator.dm:129
    public virtual void syncModules()
    {
        foreach (datum_map_generator_module mod in Dm.each_cast<datum_map_generator_module>(modules))
            mod.sync(this);
    }
}
