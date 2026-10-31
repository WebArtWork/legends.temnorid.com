using System;
using System.Collections;
using System.IO;
using ITKamianets.Engine.Combat;
using ITKamianets.Engine.Net;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Temnorid.Legends.Tests
{
	/// <summary>
	/// The real village with this machine as the host and a second player (a detached session in the same process) as the guest:
	/// the guest's hero is shown where the guest says, the host's foes reach the guest as entities, and the guest's requests
	/// (use, trigger, hit) change the host's story and come back as the shared state.
	/// </summary>
	public class OnlineLegendsTests
	{
		NetSession _guest;
		string _saveFolder;

		static IEnumerator Until(Func<bool> condition, string what, float seconds = 10f)
		{
			var end = Time.realtimeSinceStartup + seconds;
			while (!condition())
			{
				if (Time.realtimeSinceStartup > end) Assert.Fail("Timed out waiting for " + what);
				yield return null;
			}
		}

		[SetUp]
		public void SetUp()
		{
			_saveFolder = Path.Combine(Path.GetTempPath(), "legends-online-" + Guid.NewGuid());
			LegendsGame.SaveRootOverride = _saveFolder;
			LegendsGame.AutoContinue = false;
		}

		[TearDown]
		public void TearDown()
		{
			Time.timeScale = 1f;
			LegendsGame.SaveRootOverride = null;
			LegendsGame.ForceTouch = false;
			if (_guest != null) { _guest.Leave(); UnityEngine.Object.Destroy(_guest.gameObject); }
			NetSession.Instance.Leave();
			if (Directory.Exists(_saveFolder)) Directory.Delete(_saveFolder, true);
		}

		IEnumerator StartOnline()
		{
			var host = NetSession.Instance;
			host.Leave();
			host.GameId = "legends";
			host.GameVersion = Application.version;
			host.LocalName = "Hosty";
			host.MaxPlayers = 2;
			Assert.IsTrue(host.HostDirect(7805), host.LastError);

			_guest = NetSession.CreateDetached();
			_guest.GameId = "legends";
			_guest.GameVersion = Application.version;
			_guest.LocalName = "Guesty";
			_guest.LocalColour = 3;
			Assert.IsTrue(_guest.JoinDirect("127.0.0.1:7805"), _guest.LastError);
			yield return Until(() => host.Players.Count == 2 && _guest.Players.Count == 2 && !_guest.Connecting, "the guest to join");
			yield return SceneManager.LoadSceneAsync("Main");
			yield return null;
			yield return null;
		}

		IEnumerator GuestStandsAt(Vector3 position, float yaw = 0f, int state = 0)
		{
			var host = NetSession.Instance;
			for (var i = 0; i < 6; i++)
			{
				_guest.PublishPose(new NetPose { Position = position, Yaw = yaw, Speed = 0f, State = state, A = 1f });
				yield return new WaitForSeconds(0.1f);
			}
			yield return Until(() => host.TryGetPose(_guest.LocalId, out var pose) && (pose.Position - position).magnitude < 0.1f, "the host to know where the guest is");
		}

		[UnityTest]
		[Timeout(120000)]
		public IEnumerator A_guest_hero_is_shown_foes_are_published_and_the_guests_requests_change_the_shared_story()
		{
			yield return StartOnline();
			var host = NetSession.Instance;
			var game = LegendsGame.Instance;
			game.SaveEnabled = false;
			Assert.IsNotNull(game.Net, "an online village has the link to the other player");
			Assert.IsTrue(game.Net.IsHost);

			string state = null;
			string began = null;
			_guest.On(LegendsNet.StateEvent, (who, text) => state = text);
			_guest.On(LegendsNet.BeginEvent, (who, text) => began = "yes");

			// The guest's hero is shown on the host from its pose.
			var cart = new Vector3(3f, 0.1f, -36f);
			yield return GuestStandsAt(cart, 40f, RemoteHero.Blocking);
			yield return Until(() => game.Net.Heroes.Count == 1, "the guest's hero to be built");
			RemoteHero shown = null;
			foreach (var hero in game.Net.Heroes.Values) shown = hero;
			yield return Until(() => (shown.transform.position - cart).magnitude < 0.5f, "the guest's hero to be shown");
			Assert.IsTrue(shown.Alive);
			Assert.IsNull(shown.GetComponent<Collider>(), "the other hero is a picture, not a body");

			// The host starts the story: the guest is told, and gets the shared state first.
			game.Begin(true);
			yield return Until(() => began != null && state != null, "the guest to hear the story begin");

			// The host's foes are published, in ids that are the same everywhere. The sleeping guardian is not.
			yield return Until(() => _guest.Entities.ContainsKey(1), "the villager's state");
			Assert.IsFalse(_guest.Entities.ContainsKey(2), "the guardian is not in the world yet");
			Assert.AreEqual(1f, _guest.Entities[1].Value, 0.01f);

			// A shared-state round trip: the guest asks to read the cart's note, the host records it and everybody hears.
			state = null;
			_guest.Send(LegendsNet.UseEvent, "cart", true);
			yield return Until(() => game.Session.State.Has("note:cart"), "the host to record the note");
			yield return Until(() => state != null, "the shared state to come back");
			StringAssert.Contains("note:cart", state);

			// A request from too far away is refused.
			_guest.Send(LegendsNet.UseEvent, "key", true);
			yield return new WaitForSeconds(0.5f);
			Assert.IsFalse(game.Session.Has(Items.HouseKey));

			// Either hero reaching a place counts for both.
			state = null;
			_guest.Send(LegendsNet.TriggerEvent, "village-gate", true);
			yield return Until(() => game.Session.Checkpoint == 1, "the checkpoint the guest reached");
			yield return Until(() => state != null && state.Contains("\"checkpoint\":1"), "the checkpoint to reach the guest");

			// The guest's sword hurts the host's foe, and only the host decides.
			var foe = game.Ambusher;
			var before = foe.Health.Current;
			var foePos = foe.transform.position;
			yield return GuestStandsAt(foePos + new Vector3(0f, 0f, -1.6f));
			_guest.Send(LegendsNet.HitEvent, "0", true);
			yield return Until(() => foe.Health.Current < before, "the guest's blow to land");
			Assert.AreEqual(before - Fighting.SwordDamage, foe.Health.Current, 0.01f);

			// A foe's blow on the guest's hero is sent to its owner to resolve.
			string blow = null;
			_guest.On(LegendsNet.DamageEvent, (who, text) => blow = text);
			((IPrey)shown).Strike(14f, foePos, 0.85f);
			yield return Until(() => blow != null, "the blow to reach the guest");
			StringAssert.StartsWith("14", blow);
		}

		[UnityTest]
		[Timeout(120000)]
		public IEnumerator A_guest_keeps_its_own_save_and_a_fallen_hero_gets_up_again()
		{
			yield return StartOnline();
			var game = LegendsGame.Instance;
			game.SaveEnabled = false;
			game.Begin(true);
			game.Hero.Health.ApplyDamage(new DamageInfo(100000f));
			Assert.AreEqual(LegendsPhase.Dead, game.Phase);
			yield return Until(() => game.Phase == LegendsPhase.Playing, "the fallen hero to rise again", 12f);
			Assert.IsTrue(game.Hero.Health.IsAlive);
			Assert.Less(Vector3.Distance(game.Hero.transform.position, game.Checkpoints[0].position), 1.5f);
		}

		[UnityTest]
		[Timeout(60000)]
		public IEnumerator Touch_controls_exist_on_a_phone_and_feed_the_hero()
		{
			LegendsGame.ForceTouch = true;
			yield return SceneManager.LoadSceneAsync("Main");
			yield return null;
			yield return null;
			var game = LegendsGame.Instance;
			game.SaveEnabled = false;
			game.Begin(true);
			yield return null;
			yield return null;
			Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<TouchStick>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
			Assert.AreEqual(5, UnityEngine.Object.FindObjectsByType<ITKamianets.Engine.Shell.TouchButton>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
			Assert.AreEqual(Vector2.zero, game.Hero.MoveOverride, "the stick drives the hero, and it is at rest");
		}
	}
}
