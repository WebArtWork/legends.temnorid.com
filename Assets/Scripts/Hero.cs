using System;
using ITKamianets.Engine.Combat;
using ITKamianets.Engine.Feedback;
using ITKamianets.Engine.Input;
using UnityEngine;

namespace Temnorid.Legends
{
	/// <summary>
	/// The warrior: walks and runs, swings the sword, blocks with the shield and rolls out of the way. While
	/// exploring he walks relative to the camera he started moving with, so a change of shot never turns him
	/// around by surprise; in a fight he walks relative to the free camera.
	/// </summary>
	[RequireComponent(typeof(CharacterController), typeof(Health))]
	public sealed class Hero : MonoBehaviour
	{
		public LegendsGame Game;
		public CameraRig Rig;
		/// <summary>The body shapes; squashed while rolling so the collider is left alone.</summary>
		public Transform Visual;
		public Transform SwordPivot;
		public Transform ShieldPivot;
		public Light Torch;

		public Health Health { get; private set; }
		public bool Blocking { get; private set; }
		public bool Rolling => _rollLeft > 0f;
		public bool Invulnerable => Rolling && Fighting.RollSeconds - _rollLeft < Fighting.RollInvulnerableSeconds;
		public bool Swinging => _swingLeft > 0f;

		/// <summary>Raised when a blow lands, is blocked or is dodged.</summary>
		public event Action<HitResult, float> Struck;

		const float SwingSeconds = 0.32f;

		CharacterController _body;
		float _basisYaw;
		float _vy;
		float _rollLeft;
		Vector3 _rollDirection;
		float _nextRoll;
		float _nextSwing;
		float _swingLeft;
		float _torchBase;
		Vector3 _safe;
		float _nextSafe;

		/// <summary>Move input to use instead of the keys. Tests and bots set it; null means read the keys.</summary>
		public Vector2? MoveOverride;

		/// <summary>Block state to use instead of the key. Tests and bots set it; null means read the key.</summary>
		public bool? BlockOverride;

		bool _rollRequested;
		bool _swingRequested;

		/// <summary>Asks for a roll on the next frame, as if the key were pressed. Used by bots.</summary>
		public void RequestRoll() => _rollRequested = true;

		/// <summary>Asks for a swing on the next frame, as if the button were pressed. Used by bots.</summary>
		public void RequestSwing() => _swingRequested = true;

		void Awake()
		{
			Health = GetComponent<Health>();
			_body = GetComponent<CharacterController>();
			_safe = transform.position;
			if (Torch != null) _torchBase = Torch.intensity;
		}

		void Update()
		{
			var dt = Time.deltaTime;
			var actions = EngineInput.Default.Player;
			var canAct = Game != null && Game.CanControl && Health.IsAlive;
			var combat = Game != null && Game.InCombat;

			var move = Vector2.zero;
			if (canAct) move = MoveOverride ?? actions.Move.ReadValue<Vector2>();
			var moving = move.sqrMagnitude > 0.04f;

			// Walk relative to the camera the move started with; follow the camera freely when still or fighting.
			var cameraYaw = Rig != null ? Rig.Yaw : 0f;
			if (combat || !moving) _basisYaw = cameraYaw;
			var heading = Quaternion.Euler(0f, _basisYaw, 0f) * new Vector3(move.x, 0f, move.y);
			if (heading.sqrMagnitude > 1f) heading.Normalize();

			var wantsRoll = actions.Jump.WasPressedThisFrame() || _rollRequested;
			_rollRequested = false;
			if (canAct && !Rolling && !Swinging && wantsRoll && Time.time >= _nextRoll) StartRoll(moving ? heading.normalized : transform.forward);
			Blocking = canAct && !Rolling && !Swinging && (BlockOverride ?? actions.Secondary.IsPressed());
			if (canAct && !Rolling && !Blocking && Time.time >= _nextSwing && (actions.Primary.WasPressedThisFrame() || _swingRequested)) Swing();
			_swingRequested = false;

			var velocity = Vector3.zero;
			if (Rolling)
			{
				_rollLeft -= dt;
				velocity = _rollDirection * 8f;
			}
			else if (!Swinging)
			{
				var running = actions.Sprint.IsPressed();
				var speed = Blocking ? 1.6f : combat ? (running ? 4.6f : 3.4f) : (running ? 5.4f : 3.2f);
				velocity = heading * speed;
				if (Blocking && combat) FaceYaw(cameraYaw, 14f * dt);
				else if (moving) FaceDirection(heading, 12f * dt);
			}
			if (Swinging) _swingLeft -= dt;

			// If the hero ever slips out of the world, bring him back to the last place he stood safely.
			if (_body.isGrounded && transform.position.y > -1f && Time.time >= _nextSafe)
			{
				_safe = transform.position;
				_nextSafe = Time.time + 0.5f;
			}
			if (transform.position.y < -30f) Teleport(_safe, transform.eulerAngles.y);

			if (_body.isGrounded && _vy < 0f) _vy = -2f;
			_vy -= 20f * dt;
			velocity.y = _vy;
			if (_body.enabled) _body.Move(velocity * dt);

			Animate();
			if (Torch != null && Torch.enabled) Torch.intensity = _torchBase * (0.9f + 0.1f * Mathf.PerlinNoise(Time.time * 6f, 0.3f)) * (0.95f + 0.05f * Mathf.Sin(Time.time * 17f));
		}

		void FaceDirection(Vector3 direction, float t)
		{
			if (direction.sqrMagnitude < 0.0001f) return;
			transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(new Vector3(direction.x, 0f, direction.z)), Mathf.Clamp01(t));
		}

		void FaceYaw(float yaw, float t) => transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, yaw, 0f), Mathf.Clamp01(t));

		// ---- actions ----

		void StartRoll(Vector3 direction)
		{
			direction.y = 0f;
			_rollDirection = direction.sqrMagnitude > 0.01f ? direction.normalized : transform.forward;
			transform.rotation = Quaternion.LookRotation(_rollDirection);
			_rollLeft = Fighting.RollSeconds;
			_nextRoll = Time.time + Fighting.RollCooldown;
		}

		/// <summary>Swings the sword if it is ready. Returns whether it swung.</summary>
		public bool Swing()
		{
			if (Time.time < _nextSwing || !Health.IsAlive) return false;
			_nextSwing = Time.time + Fighting.SwordCooldown;
			_swingLeft = SwingSeconds;

			var direction = AimDirection();
			transform.rotation = Quaternion.LookRotation(direction);
			var origin = transform.position;
			CombatQueries.DamageInCone(origin, direction, Fighting.SwordRange, Fighting.SwordHalfAngle, Health.Team, new DamageInfo(Fighting.SwordDamage, gameObject, origin, "sword"));
			Fx.Pulse(PrimitiveType.Cube, origin + Vector3.up * 1.1f + direction * 1.4f, Quaternion.LookRotation(direction) * Quaternion.Euler(0f, 0f, 25f),
				new Vector3(0.3f, 0.06f, 0.5f), new Vector3(2.4f, 0.06f, 0.5f), new Color(1f, 0.95f, 0.75f), 0.14f);
			return true;
		}

		/// <summary>Toward the nearest foe in front when there is one close, otherwise straight ahead.</summary>
		Vector3 AimDirection()
		{
			Foe best = null;
			var bestDistance = 4f;
			foreach (var foe in Foe.All)
			{
				var to = foe.transform.position - transform.position;
				to.y = 0f;
				if (to.magnitude > bestDistance || Vector3.Angle(transform.forward, to) > 100f) continue;
				best = foe;
				bestDistance = to.magnitude;
			}
			var forward = transform.forward;
			if (best != null)
			{
				forward = best.transform.position - transform.position;
				forward.y = 0f;
			}
			return forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
		}

		/// <summary>A foe's blow. Returns what became of it.</summary>
		public HitResult TakeHit(float amount, Vector3 from, float blockStrength)
		{
			var toAttacker = from - transform.position;
			toAttacker.y = 0f;
			var inFront = toAttacker.sqrMagnitude < 0.01f || Vector3.Angle(transform.forward, toAttacker) < 80f;
			var damage = Blows.Resolve(amount, Blocking, inFront, Invulnerable, blockStrength, out var result);
			if (damage > 0f) Health.ApplyDamage(new DamageInfo(damage, null, transform.position, "hit"));
			Fx.Pulse(PrimitiveType.Sphere, transform.position + Vector3.up * 1.2f + transform.forward * (result == HitResult.Blocked ? 0.5f : 0f), Quaternion.identity,
				Vector3.one * 0.2f, Vector3.one * (result == HitResult.Hit ? 1f : 0.7f),
				result == HitResult.Hit ? new Color(0.9f, 0.1f, 0.1f) : result == HitResult.Blocked ? new Color(0.8f, 0.85f, 1f) : new Color(1f, 1f, 1f, 0.5f), 0.18f);
			Struck?.Invoke(result, damage);
			return result;
		}

		public void Heal(float amount)
		{
			Health.Heal(amount);
			Fx.Pulse(PrimitiveType.Sphere, transform.position + Vector3.up * 1.0f, Quaternion.identity, Vector3.one * 0.4f, Vector3.one * 1.6f, new Color(0.3f, 1f, 0.5f), 0.4f);
		}

		public void SetTorch(bool on)
		{
			if (Torch != null) Torch.enabled = on;
		}

		/// <summary>Moves the hero to a place and faces a direction, without walking there.</summary>
		public void Teleport(Vector3 position, float yaw)
		{
			_body.enabled = false;
			transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
			_body.enabled = true;
			_vy = 0f;
			_rollLeft = 0f;
			_swingLeft = 0f;
		}

		// ---- the look of it, from primitives ----

		void Animate()
		{
			if (SwordPivot != null)
			{
				var t = Swinging ? 1f - _swingLeft / SwingSeconds : 1.2f;
				var angle = Swinging ? Mathf.Lerp(-80f, 55f, t) : 15f;
				SwordPivot.localRotation = Quaternion.Euler(angle, 0f, 0f);
			}
			if (ShieldPivot != null)
			{
				var target = Blocking ? new Vector3(0f, 1.1f, 0.45f) : new Vector3(-0.5f, 1.0f, 0.1f);
				ShieldPivot.localPosition = Vector3.Lerp(ShieldPivot.localPosition, target, 18f * Time.deltaTime);
				ShieldPivot.localRotation = Quaternion.Slerp(ShieldPivot.localRotation, Quaternion.Euler(0f, Blocking ? 0f : 80f, 0f), 18f * Time.deltaTime);
			}
			if (Visual != null) Visual.localScale = new Vector3(1f, Rolling ? 0.6f : 1f, 1f);
		}
	}
}
