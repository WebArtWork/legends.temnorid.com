using System.Collections;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Temnorid.Legends.Tests
{
	/// <summary>
	/// A bot that fights like a careful player: it keeps the camera on the foe, swings when close, blocks the
	/// sweep, rolls out of the overhead blow and the dash, and drinks a potion when low. Explicit: run it by name
	/// (<c>-testFilter Temnorid.Legends.Tests.BalanceTests.Bot_fights_the_guardian</c>); it writes <c>Builds/balance.txt</c>.
	/// </summary>
	public class BalanceTests
	{
		const float ReactionSeconds = 0.5f;

		[UnityTest]
		[Explicit]
		[Timeout(1500000)]
		public IEnumerator Bot_fights_the_guardian()
		{
			var report = new StringBuilder();
			var wins = 0;
			const int Runs = 6;
			for (var run = 0; run < Runs; run++)
			{
				LegendsGame.SaveRootOverride = Path.Combine(Path.GetTempPath(), "legends-balance-" + System.Guid.NewGuid());
				yield return SceneManager.LoadSceneAsync("Main");
				yield return null;
				var game = LegendsGame.Instance;
				game.SaveEnabled = false;
				var hero = game.Hero;
				game.Begin(true);
				game.Session.AddItem(Items.Potion, 2); // the potion from the villager and the one in the shed
				Random.InitState(run * 31 + 7);

				hero.Teleport(new Vector3(323f, 0.1f, 57f), 0f);
				var boss = game.Boss;
				boss.gameObject.SetActive(true);
				boss.GetComponent<UnityEngine.AI.NavMeshAgent>().Warp(new Vector3(323f, 0f, 67f));
				boss.Wake(hero);
				Time.timeScale = 3f;

				var windupStart = -1f;
				var lastState = FoeState.Dormant;
				var acted = false;
				var potions = 0;
				var start = Time.realtimeSinceStartup;
				while (boss.Health.IsAlive && hero.Health.IsAlive && Time.realtimeSinceStartup - start < 200f)
				{
					var toBoss = boss.transform.position - hero.transform.position;
					toBoss.y = 0f;
					game.Rig.SetMode(CameraMode.Combat);
					game.Rig.AimAt(boss.transform.position);

					if (hero.Health.Fraction < 0.4f && game.Session.PotionCount > 0 && game.UseHeal()) potions++;

					if (boss.State == FoeState.Windup)
					{
						if (lastState != FoeState.Windup)
						{
							windupStart = Time.time;
							acted = false;
						}
						var elapsed = Time.time - windupStart;
						switch (boss.CurrentAttack)
						{
							case "Sweep":
								hero.BlockOverride = elapsed > ReactionSeconds;
								hero.MoveOverride = Vector2.zero;
								break;
							case "Overhead":
								hero.BlockOverride = false;
								if (!acted && elapsed > 0.85f)
								{
									acted = true;
									hero.MoveOverride = InputFor(game, Vector3.Cross(Vector3.up, toBoss).normalized);
									hero.RequestRoll();
								}
								break;
							case "Dash":
								hero.BlockOverride = false;
								if (!acted && elapsed > 0.6f)
								{
									acted = true;
									hero.MoveOverride = InputFor(game, Vector3.Cross(Vector3.up, toBoss).normalized);
									hero.RequestRoll();
								}
								break;
						}
					}
					else
					{
						hero.BlockOverride = false;
						if (boss.State == FoeState.Strike && boss.CurrentAttack == "Sweep") hero.BlockOverride = true;
						var near = toBoss.magnitude < Fighting.SwordRange + 0.9f;
						hero.MoveOverride = near || hero.Rolling ? Vector2.zero : InputFor(game, toBoss.normalized);
						if (near && boss.State != FoeState.Strike) hero.RequestSwing();
					}
					lastState = boss.State;
					yield return null;
				}
				hero.BlockOverride = null;
				hero.MoveOverride = null;
				var won = !boss.Health.IsAlive;
				if (won) wins++;
				else
				{
					var agent = boss.GetComponent<UnityEngine.AI.NavMeshAgent>();
					report.AppendLine($"  stuck: boss {boss.State}/{boss.CurrentAttack} at {boss.transform.position}, hero at {hero.transform.position}, distance {Vector3.Distance(boss.transform.position, hero.transform.position):0.0}, onMesh {agent.isOnNavMesh}, path {agent.pathStatus}, speed {agent.velocity.magnitude:0.0}, rolling {hero.Rolling}, blocking {hero.Blocking}");
				}
				report.AppendLine($"run {run}: {(won ? "WON" : "LOST")}, hero {hero.Health.Fraction:P0}, boss {boss.Health.Fraction:P0}, potions used {potions}, {Time.realtimeSinceStartup - start:0}s real");
			}
			report.AppendLine($"won {wins} of {Runs}");
			Directory.CreateDirectory("Builds");
			File.WriteAllText("Builds/balance.txt", report.ToString());
			Time.timeScale = 1f;
			LegendsGame.SaveRootOverride = null;
			Assert.Pass(report.ToString());
		}

		static Vector2 InputFor(LegendsGame game, Vector3 worldDirection)
		{
			var local = Quaternion.Inverse(Quaternion.Euler(0f, game.Rig.Yaw, 0f)) * worldDirection;
			return new Vector2(local.x, local.z);
		}
	}
}
