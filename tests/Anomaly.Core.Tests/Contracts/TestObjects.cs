using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;

namespace Anomaly.Core.Tests.Contracts;

/// <summary>A minimal datum for shim tests (stands in for tg's datum until the pilot translates it).</summary>
internal class TestDatum : IDmObject
{
    public long dm_id { get; private set; }

    public void dm_assign_id(long id)
    {
        if (dm_id != 0) throw new InvalidOperationException("id assigned twice");
        dm_id = id;
    }

    public void dm_pre_new(DmArgs args)
    {
    }

    /// <summary>Gives the object a world id and registers it, as Dm.@new does (object-model.md section 4, step 3).</summary>
    public T Register<T>(World world) where T : TestDatum
    {
        dm_assign_id(world.AllocateId());
        world.Objects.Register(dm_id, this);
        return (T)this;
    }
}

/// <summary>A non-atom datum subtype (like tg's <c>/datum/light_source</c>): no atom can be one.</summary>
internal sealed class TestThing : TestDatum
{
}

/// <summary>A minimal atom for shim tests: a contents list, a name and a gender.</summary>
internal class TestAtom : TestDatum, IDmAtom
{
    public TestAtom(string name = "thing") => this.name = name;

    public DmList contents { get; } = new DmList();

    public string name { get; set; }

    public string gender { get; set; } = Dm.NEUTER;
}

internal sealed class TestMob : TestAtom
{
    public TestMob(string name = "mob") : base(name)
    {
    }
}
