using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.Tools;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// The theatre's leftovers (spells/self/forcewall.dm, pointed/finger_guns.dm, effects/forcefields.dm): the mime's advanced guides
/// teach the invisible blockade, three tiles of invisible wall in front of him that last a minute, and the finger guns, which need his
/// hands empty and fire mimed bullets that silence; the wizard's forcewall lets its caster through and stops the others; a mime leans
/// on his invisible wall.
/// </summary>
public class TheatreTests
{
    private static AudioRoom Room(ulong seed)
    {
        var r = new AudioRoom(16, 11, seed: seed);
        r.In(() => TgStrings.Mount(DmWorldState.Of(r.World).Files, TgClone.Root));
        return r;
    }

    private static mob_living_carbon_human Mime(AudioRoom r, int x, int y, string key)
    {
        mob_living_carbon_human who = r.Join(x, y, key);
        r.In(() => Waiting.Detach(SSjob.equip_rank(who, SSjob.get_job_type(typeof(datum_job_mime)) as datum_job, who.client)));
        r.Step(5);
        r.In(() =>
        {
            foreach (obj_item held in Dm.each<obj_item>(who.held_items).ToList())
                Waiting.NoSleep(who.dropItemToGround(held));
        });
        return who;
    }

    private static T Learn<T>(AudioRoom r, mob_living_carbon_human mime, Type guide) where T : datum_action
    {
        T spell = null;
        r.In(() =>
        {
            var book = Dm.@new(guide, mime.loc) as obj_item_book_granter_action_spell_mime;
            Assert.True(Dm.truthy(book.can_learn(mime)));
            Waiting.Detach(book.on_reading_finished(mime));
            spell = Dm.locate<T>(mime.actions);
        });
        Assert.NotNull(spell);
        return spell;
    }

    // a chat line goes to the mob or to its client
    private static bool Told(AudioRoom r, object target, mob who) => target == who || target == r.In(() => who.client);

    [Fact]
    public void TheBlockadeRaisesThreeInvisibleWallsInFrontOfTheMimeForAMinute()
    {
        AudioRoom r = Room(14130);
        Feed feed = Listen(r);
        mob_living_carbon_human mime = Mime(r, 6, 5, "Bob");
        mob_living_carbon_human onlooker = r.Join(6, 8, "Alice");
        var blockade = Learn<datum_action_cooldown_spell_forcewall_mime>(r, mime, typeof(obj_item_book_granter_action_spell_mime_mimery_blockade));
        r.In(() => Waiting.NoSleep(mime.setDir(NORTH)));
        r.Step(1);
        Assert.True(r.In(() => Dm.truthy(Waiting.NoSleep(blockade.IsAvailable()))));
        r.Command(() => Waiting.Detach(blockade.Trigger(mime)));
        r.Step(2);

        // the mime's tile and the tiles to his east and west (turn(NORTH, 90) and turn(NORTH, 270))
        foreach ((int x, int y) in new[] { (6, 5), (5, 5), (7, 5) })
        {
            var wall = r.In(() => Dm.locate<obj_effect_forcefield_mime_advanced>(locate(x, y, 1)));
            Assert.NotNull(wall);
            r.In(() =>
            {
                Assert.True(Dm.truthy(wall.density));
                Assert.Equal(0, wall.alpha);
                Assert.Equal("invisible blockade", wall.name);
            });
        }
        // the onlooker sees the emote, its pronoun filled in; the mime reads his own line
        string pronoun = r.In(() => Dm.str(mime.p_them()));
        Assert.Contains(feed.Chat, c => Told(r, c.Target, onlooker) && c.Html.Contains("looks as if a blockade is in front of " + pronoun + ".", StringComparison.Ordinal));
        Assert.Contains(feed.Chat, c => Told(r, c.Target, mime) && c.Html.Contains("You form a blockade in front of yourself.", StringComparison.Ordinal));
        Assert.False(feed.Any("%PRONOUN"));
        Assert.False(r.In(() => Dm.truthy(Waiting.NoSleep(blockade.IsAvailable()))), "on its minute of cooldown");

        // someone walking into it is stopped
        var dummy = r.Dummy(5, 6);
        r.Step(1);
        r.In(() => Waiting.NoSleep(dummy.Move(locate(5, 5, 1), SOUTH)));
        Assert.Same(r.In(() => locate(5, 6, 1)), r.In(() => dummy.loc));

        // a minute later the walls are gone
        r.Step(61 * Second);
        Assert.Null(r.In(() => Dm.locate<obj_effect_forcefield_mime_advanced>(locate(6, 5, 1))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void WithoutTheVowTheBlockadeCannotBeCast()
    {
        AudioRoom r = Room(14131);
        mob_living_carbon_human mime = Mime(r, 6, 5, "Bob");
        var blockade = Learn<datum_action_cooldown_spell_forcewall_mime>(r, mime, typeof(obj_item_book_granter_action_spell_mime_mimery_blockade));
        r.In(() => REMOVE_TRAIT(mime.mind, TRAIT_MIMING, Dm.text("[]", typeof(datum_action_cooldown_spell_vow_of_silence))));
        r.In(() => REMOVE_TRAIT(mime, TRAIT_MIMING, Dm.text("[]", typeof(datum_action_cooldown_spell_vow_of_silence))));
        Assert.False(r.In(() => HAS_MIND_TRAIT(mime, TRAIT_MIMING)));
        r.Command(() => Waiting.Detach(blockade.Trigger(mime)));
        r.Step(2);
        Assert.Null(r.In(() => Dm.locate<obj_effect_forcefield_mime_advanced>(locate(6, 5, 1))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheFingerGunsNeedEmptyHandsAndSilenceTheirTarget()
    {
        AudioRoom r = Room(14132);
        Feed feed = Listen(r);
        mob_living_carbon_human mime = Mime(r, 4, 5, "Bob");
        var guns = Learn<datum_action_cooldown_spell_pointed_projectile_finger_guns>(r, mime, typeof(obj_item_book_granter_action_spell_mime_mimery_guns));
        var target = r.Dummy(9, 5);
        mob_living_carbon_human onlooker = r.Join(4, 8, "Alice");
        r.Step(2);

        // something in his hand: he cannot point
        r.In(() =>
        {
            var crayon = Dm.@new<obj_item_toy_crayon_mime>(mime.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(mime.put_in_active_hand(crayon))));
            Assert.False(Dm.truthy(Waiting.NoSleep(guns.try_invoke(mime))));
        });
        Assert.True(feed.Any("You can't properly fire your finger guns with something in your hand."));

        r.In(() =>
        {
            foreach (obj_item held in Dm.each<obj_item>(mime.held_items).ToList())
                Waiting.NoSleep(mime.dropItemToGround(held));
            Assert.True(Dm.truthy(Waiting.NoSleep(guns.try_invoke(mime))));
        });

        // drawn, then fired at the target: the mimed bullet hurts and silences
        float health = r.In(() => mime.health);
        r.Command(() => Waiting.Detach(guns.Trigger(mime)));
        r.Step(2);
        Assert.True(feed.Any("You draw your fingers!"));
        r.Command(() => Waiting.Detach(guns.InterceptClickOn(mime, null, target)));
        r.Step(Second);
        string pronoun = r.In(() => Dm.str(mime.p_their()));
        Assert.Contains(feed.Chat, c => Told(r, c.Target, onlooker) && c.Html.Contains("fires " + pronoun + " finger gun!", StringComparison.Ordinal));
        Assert.Contains(feed.Chat, c => Told(r, c.Target, mime) && c.Html.Contains("You fire your finger gun!", StringComparison.Ordinal));
        Assert.True(r.In(() => Dm.truthy(target.has_status_effect(typeof(datum_status_effect_silenced)))), "the target is silenced");
        Assert.True(r.In(() => Dm.num(target.get_brute_loss()) > 0), "the bullet hurt");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheWizardsForcewallLetsItsCasterThroughAndStopsTheOthers()
    {
        AudioRoom r = Room(14133);
        mob_living_carbon_human wizard = r.Join(6, 5, "Merlin");
        var other = r.Dummy(5, 6);
        r.Step(2);
        datum_action_cooldown_spell_forcewall spell = null;
        r.In(() =>
        {
            spell = Dm.@new<datum_action_cooldown_spell_forcewall>(wizard);
            spell.spell_requirements = 0; // no robes needed here
            Waiting.NoSleep(spell.Grant(wizard));
            Waiting.NoSleep(wizard.setDir(NORTH));
        });
        r.Command(() => Waiting.Detach(spell.Trigger(wizard)));
        r.Step(2);
        var field = r.In(() => Dm.locate<obj_effect_forcefield_wizard>(locate(5, 5, 1)));
        Assert.NotNull(field);
        Assert.NotNull(r.In(() => Dm.locate<obj_effect_forcefield_wizard>(locate(6, 5, 1))));
        Assert.NotNull(r.In(() => Dm.locate<obj_effect_forcefield_wizard>(locate(7, 5, 1))));
        Assert.True(r.In(() => Dm.truthy(Waiting.NoSleep(field.CanAllowThrough(wizard, WEST)))), "the caster passes");
        Assert.False(r.In(() => Dm.truthy(Waiting.NoSleep(field.CanAllowThrough(other, SOUTH)))), "the others are stopped");
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/effects/magic/forcewall.ogg");
        r.Step(31 * Second);
        Assert.True(r.In(() => QDELETED(field)), "thirty seconds later the wall is gone");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AMimeLeansOnHisInvisibleWall()
    {
        AudioRoom r = Room(14134);
        mob_living_carbon_human mime = Mime(r, 6, 5, "Bob");
        var wall = r.In(() => Dm.@new<obj_effect_forcefield_mime>(locate(6, 6, 1)));
        r.Step(2);
        r.In(() => Waiting.NoSleep(wall.mouse_drop_receive(mime, mime, null)));
        r.Step(2);
        Assert.NotNull(r.In(() => wall.GetComponent(typeof(datum_component_leanable))));
        r.AssertNoRuntimes();
    }
}
