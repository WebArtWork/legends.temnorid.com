using System;
using System.Collections;
using System.Collections.Generic;
using ITKamianets.Engine.Combat;
using ITKamianets.Engine.Feedback;
using UnityEngine;
using UnityEngine.AI;

namespace Temnorid.Legends
{
	public enum FoeKind
	{
		/// <summary>One of the villagers, twisted by the dark below.</summary>
		Cursed,
		/// <summary>The guardian of the altar.</summary>
		Boss,
	}

	public enum FoeState
	{
		Dormant,
		Chase,
		Windup,
		Strike,
		Recover,
		Dead,
	}

	/// <summary>Something a foe can hunt: the local hero, or another player's hero shown here.</summary>
	public interface IPrey
	{
		Vector3 Position { get; }
		bool Alive { get; }
		void Strike(float amount, Vector3 from, float blockStrength);
	}

	enum Shape
{
		/// <summary>A short swing in front.</summary>
		Swing,
		/// <summary>A heavy blow from above onto the ground ahead.</summary>
		Overhead,
		/// <summary>A wide sweep around the front.</summary>
		Sweep,
		/// <summary>A rush straight at the hero.</summary>
		Dash,
	}

	sealed class FoeAttack
	{
		public string Name;
		public Shape Shape;
		public float Damage;
		public float Windup;
		public float Range;
		public float HalfAngle;
		public float BlockStrength = Fighting.BlockStrength;
	}

	/// <summary>
	/// An enemy of the village. It waits until woken, chases over the NavMesh, and attacks with a warning
	/// first: a red mark on the floor and a wind-up the player can read, then the blow. The boss has three
	/// different attacks; the villager has one and can be staggered.
	/// </summary>
	[RequireComponent(typeof(NavMeshAgent), typeof(Health))]
	public sealed class Foe : MonoBehaviour
	{
		public static readonly List<Foe> All = new List<Foe>();

		/// <summary>Every hero a foe may go after. Offline it holds only the player's hero.</summary>
		public static readonly List<IPrey> Prey = new List<IPrey>();

		/// <summary>Raised when a foe dies.</summary>
		public static event Action<Foe> Killed;

		public string Id;
		public FoeKind Kind;
		/// <summary>The body shapes; leans back and lunges when attacking.</summary>
		public Transform Visual;

		public Health Health { get; private set; }
		public FoeState State { get; private set; } = FoeState.Dormant;

		/// <summary>True from the moment it is woken until it dies or the fight is over.</summary>
		public bool Engaged => State != FoeState.Dormant && State != FoeState.Dead;

		/// <summary>The attack being prepared, for the screen.</summary>
		public string CurrentAttack { get; private set; } = "";

		/// <summary>Index of the attack being prepared, or -1.</summary>
		public int AttackIndex { get; private set; } = -1;

		/// <summary>True on a guest's machine: the host simulates this foe and this copy only shows it.</summary>
		public bool Remote { get; private set; }

		float _remoteFraction = 1f;

		/// <summary>Health 0 to 1 for the screen: the real one, or what the host last said.</summary>
		public float HealthFraction => Remote ? _remoteFraction : Health.Fraction;

		NavMeshAgent _agent;
		Hero _hero;
		IPrey _prey;
FoeAttack[] _attacks;
		int _next;
		Coroutine _fight;
		GameObject _warning;

		static readonly FoeAttack[] CursedAttacks =
		{
			new FoeAttack { Name = "Swing", Shape = Shape.Swing, Damage = Fighting.VillagerDamage, Windup = 0.75f, Range = 2.3f, HalfAngle = 65f },
		};

		static readonly FoeAttack[] BossAttacks =
		{
			new FoeAttack { Name = "Overhead", Shape = Shape.Overhead, Damage = Fighting.OverheadDamage, Windup = 1.05f, Range = 2.7f, BlockStrength = Fighting.HeavyBlockStrength },
			new FoeAttack { Name = "Sweep", Shape = Shape.Sweep, Damage = Fighting.SweepDamage, Windup = 0.85f, Range = 4.4f, HalfAngle = 90f },
			new FoeAttack { Name = "Dash", Shape = Shape.Dash, Damage = Fighting.DashDamage, Windup = 0.85f, Range = 11f },
		};

		void Awake()
		{
			Health = GetComponent<Health>();
			_agent = GetComponent<NavMeshAgent>();
			_attacks = Kind == FoeKind.Boss ? BossAttacks : CursedAttacks;
			// Stats come from the rules at run time, so tuning them never needs a scene rebuild.
			Health.MaxHealth = Kind == FoeKind.Boss ? Fighting.BossHealth : Fighting.VillagerHealth;
			Health.Restore();
			Health.Died += OnDied;
			Health.Damaged += OnDamaged;
		}

		void OnEnable() => All.Add(this);
		void OnDisable() => All.Remove(this);

		/// <summary>Makes this copy only a picture of the host's foe: no walking, no fighting.</summary>
		public void SetRemote(bool remote)
		{
			Remote = remote;
			_attacks = Kind == FoeKind.Boss ? BossAttacks : CursedAttacks;
			var agent = GetComponent<NavMeshAgent>();
			if (remote && agent != null) agent.enabled = false;
		}

		/// <summary>Shows what the host says this foe is doing. <paramref name="packed"/> is the state with the attack index plus one above bit 4.</summary>
		public void ShowRemote(int packed, float fraction)
		{
			if (!Remote || State == FoeState.Dead) return;
			var state = (FoeState)(packed & 15);
			var attack = (packed >> 4) - 1;
			_remoteFraction = fraction;
			if (state == FoeState.Dead)
			{
				RemoteDie();
				return;
			}
			if (state == State) return;
			if (State == FoeState.Windup) ClearWarning();
			State = state;
			CurrentAttack = attack >= 0 && attack < _attacks.Length ? _attacks[attack].Name : "";
			if (state == FoeState.Windup && attack >= 0 && attack < _attacks.Length) ShowWarning(_attacks[attack]);
			Lean(state == FoeState.Windup ? -25f : state == FoeState.Strike ? 35f : 0f);
		}

		/// <summary>A guest's copy falls over when the host's foe dies.</summary>
		public void RemoteDie()
		{
			if (State == FoeState.Dead) return;
			State = FoeState.Dead;
			_remoteFraction = 0f;
			CurrentAttack = "";
			ClearWarning();
			foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
			StartCoroutine(Fall());
		}

		/// <summary>The state with the attack index plus one above bit 4, as the host publishes it.</summary>
		public int PackedState => (int)State | ((AttackIndex + 1) << 4);

		/// <summary>Applies <see cref="Kind"/> after the builder sets it (health and speed).</summary>
		public void Configure(FoeKind kind)
		{
			Kind = kind;
			_attacks = kind == FoeKind.Boss ? BossAttacks : CursedAttacks;
			Health = GetComponent<Health>(); // Awake has not run when the scene builder calls this
			Health.MaxHealth = kind == FoeKind.Boss ? Fighting.BossHealth : Fighting.VillagerHealth;
			Health.Team = 2;
			Health.Restore();
			if (_agent == null) _agent = GetComponent<NavMeshAgent>();
			_agent.speed = kind == FoeKind.Boss ? 3.0f : 2.6f;
			_agent.angularSpeed = 300f;
			_agent.acceleration = 14f;
			_agent.stoppingDistance = 0.2f;
		}

		/// <summary>Starts the fight.</summary>
		public void Wake(Hero hero)
		{
			if (State != FoeState.Dormant) return;
			_hero = hero;
			State = FoeState.Chase;
			_fight = StartCoroutine(Fight());
		}

		/// <summary>Turns slowly to face a point. Used when a foe notices the hero.</summary>
		public IEnumerator TurnToward(Vector3 point, float seconds)
		{
			var from = transform.rotation;
			var direction = point - transform.position;
			direction.y = 0f;
			var to = Quaternion.LookRotation(direction);
			for (var t = 0f; t < 1f; t += Time.deltaTime / seconds)
			{
				transform.rotation = Quaternion.Slerp(from, to, t);
				yield return null;
			}
			transform.rotation = to;
		}

		IEnumerator Fight()
		{
			while (State != FoeState.Dead)
			{
				State = FoeState.Chase;
				var reach = Kind == FoeKind.Boss ? 3.4f : 1.9f;
				_agent.isStopped = false;
				var stuck = 0f;
				while (Distance() > reach)
				{
					if (_agent.isOnNavMesh) _agent.SetDestination(PreyPosition());
					// If it cannot get closer (a corner, the altar), it steps through to the hero's side rather than waiting forever.
					stuck = _agent.velocity.sqrMagnitude < 0.04f ? stuck + Time.deltaTime : 0f;
					if (stuck > 2.5f)
					{
						stuck = 0f;
						var at = PreyPosition();
						var behind = at + (transform.position - at).normalized * (reach + 0.5f);
						if (NavMesh.SamplePosition(behind, out var spot, 5f, NavMesh.AllAreas)) _agent.Warp(spot.position);
					}
					yield return null;
				}
				var attack = _attacks[_next % _attacks.Length];
				_next = Kind == FoeKind.Boss ? UnityEngine.Random.Range(0, _attacks.Length) : 0;
				yield return Perform(attack);
				State = FoeState.Recover;
				yield return new WaitForSeconds(Kind == FoeKind.Boss ? 0.8f : 0.8f);
			}
		}

		/// <summary>The nearest living hero; the local hero when nobody is alive.</summary>
		IPrey Pick()
		{
			IPrey best = null;
			var bestDistance = float.MaxValue;
			for (var i = 0; i < Prey.Count; i++)
			{
				var prey = Prey[i];
				if (!prey.Alive) continue;
				var d = (prey.Position - transform.position).sqrMagnitude;
				if (d < bestDistance)
				{
					bestDistance = d;
					best = prey;
				}
			}
			return best ?? (IPrey)_hero;
		}

		Vector3 PreyPosition() => _prey.Position;

		float Distance()
		{
			_prey = Pick();
			var to = PreyPosition() - transform.position;
			to.y = 0f;
			return to.magnitude;
		}

		void Face(Vector3 point, float t)
		{
			var direction = point - transform.position;
			direction.y = 0f;
			if (direction.sqrMagnitude < 0.001f) return;
			transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Mathf.Clamp01(t));
		}

		IEnumerator Perform(FoeAttack attack)
		{
			State = FoeState.Windup;
			CurrentAttack = attack.Name;
				AttackIndex = Array.IndexOf(_attacks, attack);
			_agent.isStopped = true;
			_agent.ResetPath();

			_prey = Pick();
			Face(PreyPosition(), 1f);
			var dashDirection = transform.forward;
			ShowWarning(attack);

			for (var t = 0f; t < attack.Windup; t += Time.deltaTime)
			{
				// Keeps aiming for most of the wind-up, then commits.
				if (t < attack.Windup * 0.6f)
				{
					Face(PreyPosition(), 10f * Time.deltaTime);
					dashDirection = transform.forward;
				}
				Lean(-Mathf.Sin(Mathf.Clamp01(t / attack.Windup) * 1.5f) * 25f);
				yield return null;
			}
			ClearWarning();

			State = FoeState.Strike;
			var hit = false;
			switch (attack.Shape)
			{
				case Shape.Dash:
					for (var t = 0f; t < 0.5f; t += Time.deltaTime)
					{
						if (_agent.isOnNavMesh) _agent.Move(dashDirection * (attack.Range / 0.5f * Time.deltaTime));
						if (!hit && Distance() < 1.8f)
						{
							hit = true;
							_prey.Strike(attack.Damage, transform.position, attack.BlockStrength);
						}
						Lean(35f);
						yield return null;
					}
					break;
				default:
					if (Lands(attack)) _prey.Strike(attack.Damage, transform.position, attack.BlockStrength);
					Fx.Pulse(PrimitiveType.Cylinder, transform.position + Vector3.up * 0.08f + transform.forward * (attack.Shape == Shape.Overhead ? 2.4f : 0f), Quaternion.identity,
						Vector3.one * 0.5f, new Vector3(attack.Range * 2f, 0.02f, attack.Range * 2f), new Color(1f, 0.85f, 0.4f), 0.22f);
					Lean(40f);
					yield return new WaitForSeconds(0.25f);
					break;
			}
			Lean(0f);
			CurrentAttack = "";
			AttackIndex = -1;
			}

			/// <summary>Is the hero where this attack reaches at the moment of the blow?</summary>
		bool Lands(FoeAttack attack)
		{
			var heroAt = PreyPosition();
			var to = heroAt - transform.position;
			to.y = 0f;
			switch (attack.Shape)
			{
				case Shape.Overhead:
					var centre = transform.position + transform.forward * 2.4f;
					return Vector3.Distance(new Vector3(centre.x, 0f, centre.z), new Vector3(heroAt.x, 0f, heroAt.z)) <= attack.Range;
				default:
					return to.magnitude <= attack.Range && Vector3.Angle(transform.forward, to) <= attack.HalfAngle;
			}
		}

		void ShowWarning(FoeAttack attack)
		{
			var red = new Color(0.95f, 0.1f, 0.05f);
			switch (attack.Shape)
			{
				case Shape.Overhead:
					_warning = Fx.Pulse(PrimitiveType.Cylinder, transform.position + Vector3.up * 0.06f + transform.forward * 2.4f, Quaternion.identity,
						Vector3.one * 0.4f, new Vector3(attack.Range * 2f, 0.02f, attack.Range * 2f), red, attack.Windup);
					break;
				case Shape.Dash:
					_warning = Fx.Pulse(PrimitiveType.Cube, transform.position + Vector3.up * 0.06f + transform.forward * (attack.Range * 0.5f), Quaternion.LookRotation(transform.forward),
						new Vector3(0.4f, 0.02f, attack.Range), new Vector3(2.6f, 0.02f, attack.Range), red, attack.Windup);
					break;
				default:
					_warning = Fx.Pulse(PrimitiveType.Cylinder, transform.position + Vector3.up * 0.06f, Quaternion.identity,
						Vector3.one * 0.4f, new Vector3(attack.Range * 2f, 0.02f, attack.Range * 2f), red, attack.Windup);
					break;
			}
		}

		void ClearWarning()
		{
			if (_warning != null) Destroy(_warning);
			_warning = null;
		}

		void Lean(float degrees)
		{
			if (Visual != null) Visual.localRotation = Quaternion.Euler(degrees, 0f, 0f);
		}

		// ---- being hurt ----

		void OnDamaged(Health health, DamageInfo info)
		{
			// A villager flinches and loses his attack; the guardian does not.
			if (Remote || Kind != FoeKind.Cursed || State == FoeState.Dormant || State == FoeState.Dead || State == FoeState.Strike) return;
			if (_fight != null) StopCoroutine(_fight);
			ClearWarning();
			_fight = StartCoroutine(Stagger());
		}

		IEnumerator Stagger()
		{
			State = FoeState.Recover;
			CurrentAttack = "";
			_agent.isStopped = true;
			Lean(-20f);
			yield return new WaitForSeconds(0.45f);
			Lean(0f);
			_fight = StartCoroutine(Fight());
		}

		void OnDied(Health health, DamageInfo info)
		{
			State = FoeState.Dead;
			if (_fight != null) StopCoroutine(_fight);
			StopAllCoroutines();
			ClearWarning();
			if (_agent.isOnNavMesh) _agent.isStopped = true;
			_agent.enabled = false;
			foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
			Killed?.Invoke(this);
			StartCoroutine(Fall());
		}

		IEnumerator Fall()
		{
			var start = transform.rotation;
			var end = start * Quaternion.Euler(90f, 0f, 0f);
			for (var t = 0f; t < 1f; t += Time.deltaTime / 0.5f)
			{
				transform.rotation = Quaternion.Slerp(start, end, t);
				yield return null;
			}
		}
	}
}
