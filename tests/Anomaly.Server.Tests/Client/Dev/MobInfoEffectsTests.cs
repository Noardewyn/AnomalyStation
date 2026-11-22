using Anomaly.Client.Shared.Dev;

namespace Anomaly.Server.Tests.Client.Dev;

/// <summary>
/// The dev menu's Player tab, the status effects section (the owner, 2026-10-15): the mobinfo answer's effect rows (the type
/// path, the id and the time left) and the live types of the add field, and the dev commands its buttons send.
/// </summary>
public class MobInfoEffectsTests
{
    private const string Answer =
        "players\t#7\tAlice\n"
        + "Mob\tname\tAlice\n"
        + "Status effects\t/datum/status_effect/incapacitating/knockdown\tknockdown: 4.5 s left\n"
        + "Status effects\t/datum/status_effect/grouped/blindness\tblindness: permanent from admin\n"
        + "Movement\tslowdown\t1\n"
        + "Effect types\tlive\tincapacitating/stun incapacitating/knockdown staggered grouped/blindness\n";

    [Fact]
    public void TheEffectRowsAndTheLiveTypesAreRead()
    {
        MobInfoModel model = MobInfoModel.Parse(Answer);
        Assert.Equal(new[] { "/datum/status_effect/incapacitating/knockdown", "/datum/status_effect/grouped/blindness" }, model.StatusEffects.Select(r => r.Key));
        Assert.Equal("knockdown: 4.5 s left", model.StatusEffects[0].Value);
        Assert.Equal(new[] { "incapacitating/stun", "incapacitating/knockdown", "staggered", "grouped/blindness" }, model.EffectTypes);
        Assert.Equal("Alice", model.Value("Mob", "name"));
    }

    [Fact]
    public void AnAnswerWithoutEffectsHasEmptyLists()
    {
        MobInfoModel model = MobInfoModel.Parse("players\t#7\tAlice\nMob\tname\tAlice\n");
        Assert.Empty(model.StatusEffects);
        Assert.Empty(model.EffectTypes);
    }

    [Fact]
    public void TheButtonsSendTheEffectCommands()
    {
        Assert.Equal("effect #7 add incapacitating/stun 2.5", MobInfoModel.AddEffectCommand("#7", "incapacitating/stun", 2.5f));
        Assert.Equal("effect me add staggered", MobInfoModel.AddEffectCommand("me", "staggered", 0f)); // tg's defaults
        Assert.Equal("effect me remove /datum/status_effect/grouped/blindness", MobInfoModel.RemoveEffectCommand("me", "/datum/status_effect/grouped/blindness"));
        // what the buttons send parses as the server's dev commands
        Assert.True(DevCommandLine.TryParse(MobInfoModel.AddEffectCommand("#7", "incapacitating/stun", 2.5f), out DevCommand add, out _));
        Assert.Equal(("add", "incapacitating/stun", 2.5f), (add.Mode, add.TypePath, add.Number));
        Assert.True(DevCommandLine.TryParse(MobInfoModel.RemoveEffectCommand("me", "/datum/status_effect/grouped/blindness"), out DevCommand remove, out _));
        Assert.Equal(("remove", "/datum/status_effect/grouped/blindness"), (remove.Mode, remove.TypePath));
    }
}
