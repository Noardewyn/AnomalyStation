using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M4aAccess.P5;

/// <summary>
/// M4a P5 on Meta's boot: a joining assistant wears tg's ID card with the assistant trim (SSid_access, SSjob, the outfit's
/// ID slot and post_equip), its access opens Meta's maintenance airlocks and not security's, and give's debug card holds
/// every access (D130; the retirement of M3's dev access list, DV23).
/// </summary>
public class AssistantIdTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;

    public AssistantIdTests(MetaWorldFixture meta) => _meta = meta;

    [MetaFact]
    public void AnAssistantWearsTgsCardAndOpensMaintenanceButNotSecurity()
    {
        using (_meta.World.Enter())
        {
            obj_machinery_door_airlock maint = null, security = null;
            foreach (obj_machinery_door_airlock door in Dm.each<obj_machinery_door_airlock>())
            {
                bool maintOnly = Dm.eq(door.req_access?.len ?? 0, 0) && door.req_one_access != null && door.req_one_access.len == 1 && Dm.@in(ACCESS_MAINT_TUNNELS, door.req_one_access)
                    || door.req_access != null && door.req_access.len == 1 && Dm.@in(ACCESS_MAINT_TUNNELS, door.req_access) && Dm.eq(door.req_one_access?.len ?? 0, 0);
                if (maint == null && maintOnly)
                    maint = door;
                if (security == null && (Dm.@in(ACCESS_SECURITY, door.req_access) || Dm.@in(ACCESS_BRIG, door.req_access)) && Dm.eq(door.req_one_access?.len ?? 0, 0))
                    security = door;
            }
            Assert.NotNull(maint);
            Assert.NotNull(security);

            mob_living_carbon_human assistant = DevCommands.SpawnHuman(get_turf(maint));
            var card = assistant.wear_id as obj_item_card_id_advanced;
            Assert.NotNull(card);
            Assert.IsType<datum_id_trim_job_assistant>(card.trim);
            Assert.Equal(Dm.str(assistant.real_name), Dm.str(card.registered_name));
            Assert.Equal("Assistant", Dm.str(card.assignment));
            Assert.Contains("ID Card (Assistant)", Dm.str(card.name));
            DmList access = (DmList)assistant.get_access();
            Assert.True(Dm.@in(ACCESS_MAINT_TUNNELS, access), "tg's assistant trim: maintenance (extra_access, jobs_have_minimal_access off)");
            Assert.False(Dm.@in(ACCESS_SECURITY, access));
            Assert.True(Dm.truthy(Waiting.NoSleep(maint.allowed__proc(assistant))), "the assistant opens a maintenance airlock");
            Assert.False(Dm.truthy(Waiting.NoSleep(security.allowed__proc(assistant))), "the assistant does not open security");

            // give's debug card: the admin trim, every access, held in a hand (ITEM_SLOT_HANDS gives access, cards_ids.dm:190)
            mob_living_carbon_human admin = DevCommands.SpawnHuman(get_turf(security));
            string line = DevCommands.Give(admin, "/obj/item/card/id/advanced/debug");
            Assert.Contains("in hand", line);
            Assert.True(Dm.truthy(Waiting.NoSleep(security.allowed__proc(admin))), "the debug card opens security");
            Assert.True(Dm.@in(ACCESS_CAPTAIN, (DmList)admin.get_access()));
            Assert.Contains("is not an /obj/item", DevCommands.Give(admin, "/obj/structure/table"));
            Assert.Contains("tg has no type", DevCommands.Give(admin, "/obj/item/no_such_thing"));
        }
        Assert.Empty(_meta.Log.Runtimes.Where(e => (e.StackTrace ?? "").Contains("card", StringComparison.OrdinalIgnoreCase) || (e.StackTrace ?? "").Contains("id_trim", StringComparison.Ordinal) || (e.StackTrace ?? "").Contains("subsystem_job", StringComparison.Ordinal)).Select(e => e.Message + "\n" + e.StackTrace));
    }
}
