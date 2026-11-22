// Translated from tgstation (snapshot 1d99bd7f): code/modules/procedural_mapping/mapGeneratorModule.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S4d, D327, with Mafia's mass delete): a map generator's module, synced to its generator, placing its turfs
// and atoms over the region with tg's clustering checks. The var blocks are generated (D111).

// tg: code/modules/procedural_mapping/mapGeneratorModule.dm:2
public partial class datum_map_generator_module : datum
{
    // tg: code/modules/procedural_mapping/mapGeneratorModule.dm:11
    public override object Destroy(float? force = null)
    {
        mother = null;
        return base.Destroy(force);
    }

    //Syncs the module up with its mother
    // tg: code/modules/procedural_mapping/mapGeneratorModule.dm:16
    public virtual void sync(datum_map_generator mum = null)
    {
        mother = null;
        if (mum != null)
            mother = mum;
    }

    //Generates its spawnable atoms and turfs
    // tg: code/modules/procedural_mapping/mapGeneratorModule.dm:23
    public virtual async DmTask generate()
    {
        if (mother == null)
            return;
        DmList map = mother.map;
        foreach (turf T in Dm.each<turf>(map))
            await place(T);
    }

    //Place a spawnable atom or turf on this turf
    // tg: code/modules/procedural_mapping/mapGeneratorModule.dm:32
    public virtual async DmTask<object> place(turf T = null)
    {
        if (T == null)
            return 0;

        float clustering = 0;
        bool skipLoopIteration = false;

        //Turfs don't care whether atoms can be placed here
        foreach (object turfPath in Dm.each(spawnableTurfs))
        {
            //Clustering!
            if (Dm.truthy(clusterMax) && Dm.truthy(clusterMin))
            {
                //You're the same as me? I hate you I'm going home
                if (Dm.truthy(Dm.bitand(clusterCheckFlags, CLUSTER_CHECK_SAME_TURFS)))
                {
                    clustering = rand(clusterMin, clusterMax);
                    foreach (turf F in Dm.each<turf>(RANGE_TURFS(clustering, T)))
                    {
                        if (istype(F, turfPath as Type))
                        {
                            skipLoopIteration = true;
                            break;
                        }
                    }
                    if (skipLoopIteration)
                    {
                        skipLoopIteration = false;
                        continue;
                    }
                }

                //You're DIFFERENT to me? I hate you I'm going home
                if (Dm.truthy(Dm.bitand(clusterCheckFlags, CLUSTER_CHECK_DIFFERENT_TURFS)))
                {
                    clustering = rand(clusterMin, clusterMax);
                    foreach (turf F in Dm.each<turf>(RANGE_TURFS(clustering, T)))
                    {
                        if (!istype(F, turfPath as Type))
                        {
                            skipLoopIteration = true;
                            break;
                        }
                    }
                    if (skipLoopIteration)
                    {
                        skipLoopIteration = false;
                        continue;
                    }
                }
            }

            //Success!
            if (prob(Dm.num(spawnableTurfs[turfPath])))
                await T.ChangeTurf(turfPath);
        }

        //Atoms DO care whether atoms can be placed here
        if (Dm.truthy(checkPlaceAtom(T)))
        {
            foreach (object atomPath in Dm.each(spawnableAtoms))
            {
                //Clustering!
                if (Dm.truthy(clusterMax) && Dm.truthy(clusterMin))
                {
                    //You're the same as me? I hate you I'm going home
                    if (Dm.truthy(Dm.bitand(clusterCheckFlags, CLUSTER_CHECK_SAME_ATOMS)))
                    {
                        clustering = rand(clusterMin, clusterMax);
                        foreach (atom_movable M in Dm.each<atom_movable>(range(clustering, T)))
                        {
                            if (istype(M, atomPath as Type))
                            {
                                skipLoopIteration = true;
                                break;
                            }
                        }
                        if (skipLoopIteration)
                        {
                            skipLoopIteration = false;
                            continue;
                        }
                    }

                    //You're DIFFERENT from me? I hate you I'm going home
                    if (Dm.truthy(Dm.bitand(clusterCheckFlags, CLUSTER_CHECK_DIFFERENT_ATOMS)))
                    {
                        clustering = rand(clusterMin, clusterMax);
                        foreach (atom_movable M in Dm.each<atom_movable>(range(clustering, T)))
                        {
                            if (!istype(M, atomPath as Type))
                            {
                                skipLoopIteration = true;
                                break;
                            }
                        }
                        if (skipLoopIteration)
                        {
                            skipLoopIteration = false;
                            continue;
                        }
                    }
                }

                //Success!
                if (prob(Dm.num(spawnableAtoms[atomPath])))
                    Dm.@new(atomPath as Type, T);
            }
        }

        return 1;
    }

    //Checks and Rejects dense turfs
    // tg: code/modules/procedural_mapping/mapGeneratorModule.dm:110
    public virtual object checkPlaceAtom(turf T = null)
    {
        if (T == null || Dm.truthy(Dm.bitand(T.turf_flags, TURF_BLOCKS_POPULATE_TERRAIN_FLORAFEATURES)))
            return FALSE;
        if (Dm.truthy(T.density))
            return FALSE;
        foreach (atom A in Dm.each<atom>(T))
        {
            if (Dm.truthy(A.density))
                return FALSE;
        }
        if (!Dm.truthy(allowAtomsOnSpace) && isspaceturf(T))
            return FALSE;
        return TRUE;
    }
}
