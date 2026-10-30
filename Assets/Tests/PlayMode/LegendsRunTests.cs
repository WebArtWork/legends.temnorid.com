using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using ITKamianets.Engine.Combat;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Temnorid.Legends.Tests
{
	public class LegendsRunTests
	{
		string _saveFolder;

		[SetUp]
		public void SetUp()
		{
			_saveFolder = Path.Combine(Path.GetTempPath(), "legends-playmode-" + Guid.NewGuid());
			LegendsGame.SaveRootOverride = _saveFolder;
			LegendsGame.AutoContinue = false;
		}

		[TearDown]
		public void TearDown()
		{
			LegendsGame.SaveRootOverride = null;
			LegendsGame.AutoContinue = false;
			Time.timeScale = 1f;
			if (Directory.Exists(_saveFolder)) Directory.Delete(_saveFolder, true);
		}

		static IEnumerator Load()
		{
			yield return SceneManager.LoadSceneAsync("Main");
			yield return null;
			yield return null;
		}

		static Interactable Find(string id)
		{
			foreach (var item in Interactable.All) if (item.Id == id) return item;
			Assert.Fail("No available interactable: " + id);
			return null;
		}

		static IEnumerator WaitUntil(Func<bool> condition, float seconds, string what)
		{
			var start = Time.realtimeSinceStartup;
			while (!condition())
			{
				if (Time.realtimeSinceStartup - start > seconds) Assert.Fail("Timed out waiting for: " + what);
				yield return null;
			}
		}

		[UnityTest]
		public IEnumerator Every_place_and_object_of_the_story_exists_and_the_game_waits_on_the_menu()
		{
			yield return Load();
			var game = LegendsGame.Instance;
			game.SaveEnabled = false;

			Assert.AreEqual(LegendsPhase.Menu, game.Phase);
			Assert.AreEqual(7, game.Checkpoints.Length);
			Assert.IsNotNull(game.Ambusher);
			Assert.IsNotNull(game.Boss);
			Assert.IsFalse(game.Boss.gameObject.activeSelf, "the guardian sleeps until the altar is touched");
			Assert.IsTrue(game.Ambusher.Agent().isOnNavMesh, "the villager stands on the baked street");

			var expected = new[] { "cart", "villager", "key", "potion-shed", "house-door", "symbol-0", "symbol-1", "symbol-2", "journal-1", "journal-2", "journal-3", "carving-moon", "carving-crown", "carving-sun", "torch" };
			foreach (var id in expected) Assert.IsNotNull(Find(id), id);

			var hidden = new List<string>();
			foreach (var item in UnityEngine.Object.FindObjectsByType<Interactable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
				if (!item.gameObject.activeSelf) hidden.Add(item.Id);
			CollectionAssert.IsSupersetOf(hidden, new[] { "dungeon-stairs", "altar", "artifact" }, "these show up only when the story reaches them");
		}

		[UnityTest]
		public IEnumerator Every_fixed_camera_sits_in_open_air_and_sees_the_middle_of_its_place()
		{
			yield return Load();
			var problems = new List<string>();
			foreach (var zone in UnityEngine.Object.FindObjectsByType<CameraZone>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
			{
				var anchor = zone.Anchor.position;
				var box = zone.GetComponent<BoxCollider>();
				var middle = box.bounds.center;
				middle.y = 1.3f;

				if (Physics.CheckSphere(anchor, 0.3f, ~0, QueryTriggerInteraction.Ignore)) problems.Add(zone.name + ": the camera is inside something solid");
				if (Physics.Linecast(anchor, middle, out var hit, ~0, QueryTriggerInteraction.Ignore))
					problems.Add($"{zone.name}: the view of the middle is blocked by {hit.collider.name}");
				if (!box.bounds.Contains(new Vector3(middle.x, box.bounds.center.y, middle.z))) problems.Add(zone.name + ": bad volume");
			}
			Assert.IsEmpty(problems, string.Join("\n", problems));
		}

		[UnityTest]
		[Timeout(300000)]
		public IEnumerator The_whole_demo_can_be_played_from_the_road_to_the_ending()
		{
			yield return Load();
			var game = LegendsGame.Instance;
			var hero = game.Hero;
			game.Begin(true);
			Assert.AreEqual(LegendsPhase.Playing, game.Phase);
			StringAssert.Contains("forest road", game.Objective);

			// Scene 1: the road and the cart.
			hero.Teleport(new Vector3(3f, 0.1f, -36f), 0f);
			game.OnInteract(Find("cart"));
			Assert.AreEqual(LegendsPhase.Reading, game.Phase);
			Assert.IsTrue(game.Session.State.Has("note:cart"));
			game.CloseReading();
			game.OnTrigger("village-gate");
			Assert.AreEqual(1, game.Session.Checkpoint);

			// Scene 2: the wounded villager.
			hero.Teleport(new Vector3(-20f, 0.1f, 17f), 90f);
			game.OnInteract(Find("villager"));
			Assert.AreEqual(LegendsPhase.Dialogue, game.Phase);
			for (var i = 0; i < Story.VillagerLines.Length; i++) game.AdvanceDialogue();
			Assert.AreEqual(1, game.Session.PotionCount);
			Assert.IsTrue(game.Session.State.Has("talked:villager"));
			Time.timeScale = 6f;
			yield return WaitUntil(() => game.Phase == LegendsPhase.Playing, 40f, "the window scene to end");

			// Scene 3: the first fight.
			hero.Teleport(new Vector3(0f, 0.1f, 36f), 0f);
			game.OnTrigger("street-ambush");
			yield return WaitUntil(() => game.Ambusher.Engaged, 20f, "the villager to attack");
			Assert.IsTrue(game.InCombat || game.Ambusher.Engaged);
			var start = Time.realtimeSinceStartup;
			while (game.Ambusher.Health.IsAlive && Time.realtimeSinceStartup - start < 60f)
			{
				if (hero.Health.Fraction < 0.5f) hero.Health.Heal(100f);
				hero.Swing();
				yield return null;
			}
			Assert.IsFalse(game.Ambusher.Health.IsAlive, "the sword killed the villager");
			Assert.IsTrue(game.Session.State.Has("dead:ambush"));
			Assert.AreEqual(2, game.Session.Checkpoint);
			yield return WaitUntil(() => !game.InCombat, 10f, "the fight to end");

			// Scene 4: the key, the door, the documents, the lock.
			hero.Teleport(new Vector3(-9f, 0.1f, 65f), 0f);
			game.OnInteract(Find("key"));
			Assert.IsTrue(game.Session.Has(Items.HouseKey));
			Assert.IsTrue(game.Session.State.Has("got:key"));
			hero.Teleport(new Vector3(0f, 0.1f, 68.5f), 0f);
			game.OnInteract(Find("house-door"));
			Assert.IsTrue(game.Session.State.Has("open:house-door"));
			foreach (var id in new[] { "journal-1", "journal-2", "journal-3", "carving-moon", "carving-crown", "carving-sun" })
			{
				game.OnInteract(Find(id));
				game.CloseReading();
				Assert.IsTrue(game.Session.State.Has("note:" + id), id);
			}
			game.OnInteract(Find("symbol-1"));
			Assert.IsFalse(game.Session.State.Has("puzzle-solved"));
			for (var i = 0; i < 3; i++) game.OnInteract(Find("symbol-0"));
			for (var i = 0; i < 2; i++) game.OnInteract(Find("symbol-2"));
			Assert.IsTrue(game.Session.State.Has("puzzle-solved"));
			Assert.AreEqual(3, game.Session.Checkpoint);

			// Scene 5: the torch, the stairs, the ritual room.
			game.OnInteract(Find("torch"));
			Assert.IsTrue(game.Session.Has(Items.Torch));
			game.OnInteract(Find("dungeon-stairs"));
			yield return WaitUntil(() => game.Phase == LegendsPhase.Playing && game.InDungeon, 30f, "the descent");
			Assert.AreEqual(4, game.Session.Checkpoint);
			yield return null;
			Assert.IsTrue(hero.Torch.enabled, "the torch lights the dark");

			hero.Teleport(new Vector3(322f, 0.1f, 50f), 0f);
			game.OnTrigger("ritual-room");
			Assert.AreEqual(5, game.Session.Checkpoint);
			yield return WaitUntil(() => game.Phase == LegendsPhase.Playing, 40f, "the ritual scene to end");

			// Scene 6: the guardian.
			hero.Teleport(new Vector3(323f, 0.1f, 60f), 0f);
			game.OnInteract(Find("altar"));
			yield return WaitUntil(() => game.Boss.Engaged, 40f, "the guardian to rise");
			Assert.IsTrue(game.BossVisible);
			hero.Health.MaxHealth = 100000f;
			hero.Health.Restore();
			game.Boss.Health.ApplyDamage(new DamageInfo(100000f));
			Assert.IsTrue(game.Session.State.Has("dead:boss"));
			yield return WaitUntil(() => game.Phase == LegendsPhase.Playing, 10f, "control to return");
			StringAssert.Contains("artifact", game.Objective);

			// Scene 7: the ending.
			Time.timeScale = 8f;
			game.OnInteract(Find("artifact"));
			Assert.IsTrue(game.Session.Has(Items.Artifact));
			yield return WaitUntil(() => game.Phase == LegendsPhase.Ended, 90f, "the ending");
			Assert.IsTrue(game.Session.State.Has("done:ending"));
		}

		[UnityTest]
		[Timeout(120000)]
		public IEnumerator A_villager_attacks_and_the_hero_can_block_roll_or_be_hit()
		{
			yield return Load();
			var game = LegendsGame.Instance;
			game.SaveEnabled = false;
			var hero = game.Hero;
			game.Begin(true);
			hero.Teleport(new Vector3(0f, 0.1f, 36f), 0f);
			yield return null;

			var before = hero.Health.Current;
			Assert.AreEqual(HitResult.Hit, hero.TakeHit(20f, hero.transform.position + Vector3.forward * 2f, Fighting.BlockStrength));
			Assert.AreEqual(before - 20f, hero.Health.Current, 0.01f, "an open hit lands in full");

			hero.BlockOverride = true;
			yield return null;
			before = hero.Health.Current;
			Assert.IsTrue(hero.Blocking);
			Assert.AreEqual(HitResult.Blocked, hero.TakeHit(20f, hero.transform.position + hero.transform.forward * 2f, Fighting.BlockStrength));
			Assert.AreEqual(before - 3f, hero.Health.Current, 0.01f, "a block stops most of it");
			Assert.AreEqual(HitResult.Hit, hero.TakeHit(20f, hero.transform.position - hero.transform.forward * 2f, Fighting.BlockStrength), "a blow from behind is not blocked");

			hero.BlockOverride = false;
			hero.Health.Heal(200f);
			yield return null;
			hero.RequestRoll();
			yield return null;
			yield return null;
			Assert.IsTrue(hero.Rolling);
			before = hero.Health.Current;
			Assert.AreEqual(HitResult.Dodged, hero.TakeHit(40f, hero.transform.position + Vector3.forward, Fighting.BlockStrength));
			Assert.AreEqual(before, hero.Health.Current, 0.01f);

			// A woken villager comes and hurts a hero who stands still.
			yield return new WaitForSeconds(Fighting.RollSeconds + 0.2f);
			hero.Health.Heal(200f);
			hero.Teleport(new Vector3(0f, 0.1f, 40f), 180f);
			game.Ambusher.Wake(hero);
			var health = hero.Health.Current;
			var start = Time.time;
			while (hero.Health.Current >= health && Time.time - start < 15f) yield return null;
			Assert.Less(hero.Health.Current, health, "the villager reached the hero and struck");
		}

		[UnityTest]
		[Timeout(120000)]
		public IEnumerator The_camera_follows_the_places_and_goes_over_the_shoulder_in_a_fight()
		{
			yield return Load();
			var game = LegendsGame.Instance;
			game.SaveEnabled = false;
			var hero = game.Hero;
			game.Begin(true);

			hero.Teleport(new Vector3(0f, 0.1f, -60f), 0f);
			yield return new WaitForFixedUpdate();
			yield return new WaitForFixedUpdate();
			yield return null;
			Assert.AreEqual("RoadHigh", game.Rig.ActiveZone != null ? game.Rig.ActiveZone.name : "none");
			Assert.AreEqual(CameraMode.Exploration, game.Rig.Mode);

			hero.Teleport(new Vector3(0f, 0.1f, -37f), 0f);
			yield return new WaitForFixedUpdate();
			yield return new WaitForFixedUpdate();
			yield return null;
			Assert.AreEqual("CartLow", game.Rig.ActiveZone.name, "the camera changes to the low shot at the cart");

			hero.Teleport(new Vector3(0f, 0.1f, 40f), 180f);
			yield return new WaitForFixedUpdate();
			game.Ambusher.Wake(hero);
			yield return null;
			yield return null;
			Assert.AreEqual(CameraMode.Combat, game.Rig.Mode);

			game.Ambusher.Health.ApplyDamage(new DamageInfo(1000f));
			var start = Time.time;
			while (game.Rig.Mode == CameraMode.Combat && Time.time - start < 10f) yield return null;
			Assert.AreEqual(CameraMode.Exploration, game.Rig.Mode, "the camera goes back to the fixed shots when the danger is over");
		}

		[UnityTest]
		[Timeout(120000)]
		public IEnumerator Dying_sends_the_hero_back_to_the_last_checkpoint_with_the_story_as_it_was()
		{
			yield return Load();
			var game = LegendsGame.Instance;
			game.SaveEnabled = true;
			game.Begin(true);
			game.Hero.Teleport(new Vector3(-9f, 0.1f, 65f), 0f);
			game.OnInteract(Find("key"));
			game.OnTrigger("village-gate"); // checkpoint 1 saves the key too

			game.Hero.Health.ApplyDamage(new DamageInfo(100000f));
			Assert.AreEqual(LegendsPhase.Dead, game.Phase);
			game.Retry();
			yield return null;
			yield return null;
			yield return null;

			game = LegendsGame.Instance;
			Assert.AreEqual(LegendsPhase.Playing, game.Phase, "retry skips the menu");
			Assert.AreEqual(1, game.Session.Checkpoint);
			Assert.IsTrue(game.Session.Has(Items.HouseKey));
			Assert.Less(Vector3.Distance(game.Hero.transform.position, game.Checkpoints[1].position), 1.5f);
			Assert.IsTrue(game.Hero.Health.IsAlive);
			Assert.IsNull(FindOrNull("key"), "the key stays picked up");
		}

		static Interactable FindOrNull(string id)
		{
			foreach (var item in Interactable.All) if (item.Id == id) return item;
			return null;
		}

		[UnityTest]
		[Timeout(240000)]
		public IEnumerator The_guardian_uses_all_three_of_its_attacks()
		{
			yield return Load();
			var game = LegendsGame.Instance;
			game.SaveEnabled = false;
			var hero = game.Hero;
			game.Begin(true);
			hero.Health.MaxHealth = 1000000f;
			hero.Health.Restore();
			hero.Teleport(new Vector3(323f, 0.1f, 58f), 180f);

			var boss = game.Boss;
			boss.gameObject.SetActive(true);
			boss.Agent().Warp(new Vector3(323f, 0f, 66f));
			boss.Wake(hero);
			Time.timeScale = 4f;

			var seen = new HashSet<string>();
			var start = Time.realtimeSinceStartup;
			while (seen.Count < 3 && Time.realtimeSinceStartup - start < 100f)
			{
				if (!string.IsNullOrEmpty(boss.CurrentAttack)) seen.Add(boss.CurrentAttack);
				yield return null;
			}
			CollectionAssert.IsSupersetOf(seen, new[] { "Overhead", "Sweep", "Dash" });
		}
	}

	static class FoeExtensions
	{
		public static UnityEngine.AI.NavMeshAgent Agent(this Foe foe) => foe.GetComponent<UnityEngine.AI.NavMeshAgent>();
	}
}
