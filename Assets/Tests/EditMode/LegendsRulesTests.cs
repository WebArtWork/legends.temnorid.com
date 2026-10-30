using System.IO;
using NUnit.Framework;

namespace Temnorid.Legends.Tests
{
	public class LegendsRulesTests
	{
		[Test]
		public void A_new_game_starts_empty_at_the_first_checkpoint()
		{
			var session = new LegendsSession();
			session.AddItem(Items.Potion);
			session.State.Set("done:village-gate");
			session.SetCheckpoint(2);
			session.StartNew();

			Assert.AreEqual(0, session.Checkpoint);
			Assert.AreEqual(0, session.State.Count);
			Assert.AreEqual(0, session.PotionCount);
		}

		[Test]
		public void Checkpoints_only_move_forward()
		{
			var session = new LegendsSession();
			session.SetCheckpoint(3);
			session.SetCheckpoint(1);
			Assert.AreEqual(3, session.Checkpoint);
			session.SetCheckpoint(99);
			Assert.AreEqual(LegendsSession.CheckpointCount - 1, session.Checkpoint);
		}

		[Test]
		public void A_potion_is_used_once_and_only_when_there_is_one()
		{
			var session = new LegendsSession();
			Assert.IsFalse(session.UsePotion());
			session.AddItem(Items.Potion, 2);
			Assert.IsTrue(session.UsePotion());
			Assert.AreEqual(1, session.PotionCount);
			Assert.IsTrue(session.UsePotion());
			Assert.IsFalse(session.UsePotion());
		}

		[Test]
		public void The_objective_follows_the_story_from_the_road_to_the_artifact()
		{
			var state = new WorldState();
			StringAssert.Contains("forest road", Story.Objective(state));

			var steps = new[]
			{
				("done:village-gate", "knocking"),
				("talked:villager", "elder's house"),
				("dead:ambush", "key"),
				("open:house-door", "cellar door"),
				("puzzle-solved", "Descend"),
				("done:descended", "Explore the dungeon"),
				("done:ritual-room", "altar"),
				("done:altar", "Survive"),
				("dead:boss", "artifact"),
				("got:artifact", "Leave the dungeon"),
			};
			foreach (var (flag, expected) in steps)
			{
				state.Set(flag);
				StringAssert.Contains(expected, Story.Objective(state), "after " + flag);
			}
		}

		[Test]
		public void The_symbol_lock_opens_only_with_the_carved_order()
		{
			var puzzle = new SymbolPuzzle();
			Assert.IsFalse(puzzle.IsSolved);

			// Moon, Crown, Sun, starting from Star on each pedestal.
			for (var i = 0; i < 3; i++) puzzle.Cycle(0);
			Assert.AreEqual("Moon", puzzle.Name(0));
			puzzle.Cycle(1);
			Assert.AreEqual("Crown", puzzle.Name(1));
			Assert.IsFalse(puzzle.IsSolved, "two of three is not enough");
			puzzle.Cycle(2);
			puzzle.Cycle(2);
			Assert.AreEqual("Sun", puzzle.Name(2));
			Assert.IsTrue(puzzle.IsSolved);

			puzzle.Cycle(0);
			Assert.IsFalse(puzzle.IsSolved, "turning one pedestal breaks it");
		}

		[Test]
		public void The_clues_name_the_three_symbols_in_order()
		{
			StringAssert.Contains("night", Story.Note("carving-moon").Text);
			StringAssert.Contains("king", Story.Note("carving-crown").Text);
			StringAssert.Contains("sun", Story.Note("carving-sun").Text);
			Assert.IsNull(Story.Note("nothing"));
			foreach (var note in Story.Notes) Assert.IsNotEmpty(note.Text, note.Id);
		}

		[Test]
		public void A_roll_dodges_a_block_stops_most_and_an_open_hit_lands_in_full()
		{
			Assert.AreEqual(0f, Blows.Resolve(30f, false, true, true, Fighting.BlockStrength, out var result));
			Assert.AreEqual(HitResult.Dodged, result);

			var blocked = Blows.Resolve(20f, true, true, false, Fighting.BlockStrength, out result);
			Assert.AreEqual(HitResult.Blocked, result);
			Assert.AreEqual(20f * (1f - Fighting.BlockStrength), blocked, 0.001f);

			Assert.AreEqual(30f, Blows.Resolve(30f, true, false, false, Fighting.BlockStrength, out result), "a blow from behind is not blocked");
			Assert.AreEqual(HitResult.Hit, result);
			Assert.AreEqual(30f, Blows.Resolve(30f, false, true, false, Fighting.BlockStrength, out result));
		}

		[Test]
		public void A_heavy_blow_gets_through_a_block_more_than_a_light_one()
		{
			var light = Blows.Resolve(30f, true, true, false, Fighting.BlockStrength, out _);
			var heavy = Blows.Resolve(30f, true, true, false, Fighting.HeavyBlockStrength, out _);
			Assert.Greater(heavy, light);
		}

		[Test]
		public void The_checkpoint_survives_a_save_and_load()
		{
			var folder = Path.Combine(Path.GetTempPath(), "legends-save-" + System.Guid.NewGuid());
			try
			{
				var session = new LegendsSession();
				session.AddItem(Items.HouseKey);
				session.AddItem(Items.Potion, 2);
				session.State.Set("got:key");
				session.State.Set("dead:ambush");
				session.SetCheckpoint(2);
				new LegendsSaveStore(folder).Save(session.ToSave());

				var loaded = new LegendsSession();
				loaded.Load(new LegendsSaveStore(folder).Load());
				Assert.AreEqual(2, loaded.Checkpoint);
				Assert.IsTrue(loaded.State.Has("dead:ambush"));
				Assert.IsTrue(loaded.Has(Items.HouseKey));
				Assert.AreEqual(2, loaded.PotionCount);
			}
			finally
			{
				if (Directory.Exists(folder)) Directory.Delete(folder, true);
			}
		}

		[Test]
		public void A_missing_save_loads_as_nothing()
		{
			var folder = Path.Combine(Path.GetTempPath(), "legends-save-" + System.Guid.NewGuid());
			Assert.IsNull(new LegendsSaveStore(folder).Load());
			var session = new LegendsSession();
			session.Load(null);
			Assert.AreEqual(0, session.Checkpoint);
		}
	}
}
