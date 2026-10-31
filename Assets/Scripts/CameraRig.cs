using System.Collections.Generic;
using ITKamianets.Engine.Input;
using UnityEngine;

namespace Temnorid.Legends
{
	public enum CameraMode
	{
		/// <summary>Fixed shots chosen by <see cref="CameraZone"/>.</summary>
		Exploration,
		/// <summary>Over the shoulder, with free look.</summary>
		Combat,
		/// <summary>A shot placed by the story with <see cref="CameraRig.SetShot"/>.</summary>
		Cinematic,
	}

	/// <summary>
	/// The one camera and its three ways of looking: fixed shots for exploring, over the shoulder for fighting
	/// and scripted shots for the story. Moving between them glides, so a change never jumps.
	/// </summary>
	[RequireComponent(typeof(Camera))]
	public sealed class CameraRig : MonoBehaviour
	{
		public static CameraRig Instance { get; private set; }

		public Hero Target;
		public CameraMode Mode { get; private set; } = CameraMode.Exploration;

		[Header("Exploration")]
		public Vector3 FallbackOffset = new Vector3(0f, 7f, -8f);
		public float BlendSpeed = 3f;
		public float TrackSpeed = 7f;

		[Header("Combat")]
		public float Distance = 3.8f;
		public float Shoulder = 0.7f;
		public float Height = 1.6f;
		public float LookSensitivity = 1f;

		/// <summary>Without a mouse the fighting camera turns by itself toward the foe (or where the hero faces). True on phones.</summary>
		public bool AutoAim = Application.isMobilePlatform;

		readonly List<CameraZone> _zones = new List<CameraZone>();
		Camera _camera;
		float _yaw;
		float _pitch = 14f;
		Vector3 _shotPosition;
		Vector3 _shotLookAt;
		float _shotFov = 50f;
		bool _snap;

		public CameraZone ActiveZone { get; private set; }

		/// <summary>Which way the camera faces on the ground, in degrees. The hero walks relative to this.</summary>
		public float Yaw => transform.eulerAngles.y;

		void Awake()
		{
			Instance = this;
			_camera = GetComponent<Camera>();
		}

		void OnDestroy()
		{
			if (Instance == this) Instance = null;
		}

		public void ZoneEntered(CameraZone zone)
		{
			_zones.Remove(zone);
			_zones.Add(zone);
			Choose(zone);
		}

		public void ZoneExited(CameraZone zone)
		{
			if (!_zones.Remove(zone)) return;
			Choose(null);
		}

		void Choose(CameraZone preferred)
		{
			CameraZone best = null;
			foreach (var zone in _zones) if (best == null || zone.Priority >= best.Priority) best = zone;
			if (best != ActiveZone)
			{
				ActiveZone = best;
				if (best != null && best.Cut) _snap = true;
			}
		}

		public void SetMode(CameraMode mode)
		{
			if (mode == Mode) return;
			if (mode == CameraMode.Combat && Target != null)
			{
				_yaw = Target.transform.eulerAngles.y;
				_pitch = 14f;
			}
			Mode = mode;
		}

		/// <summary>The shot for <see cref="CameraMode.Cinematic"/>.</summary>
		public void SetShot(Vector3 position, Vector3 lookAt, float fov = 50f)
		{
			_shotPosition = position;
			_shotLookAt = lookAt;
			_shotFov = fov;
		}

		/// <summary>Turns the over-the-shoulder camera to look toward a point, as a player would with the mouse. Used by bots.</summary>
		public void AimAt(Vector3 point)
		{
			if (Target == null) return;
			var direction = point - Target.transform.position;
			_yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
		}

		/// <summary>Puts the camera on its target shot at once.</summary>
		public void Snap() => _snap = true;

		void LateUpdate()
		{
			if (Target == null) return;
			var dt = Time.unscaledDeltaTime;
			var focus = Target.transform.position + Vector3.up * 1.3f;

			Vector3 position;
			Quaternion rotation;
			float fov;
			var blend = BlendSpeed;

			switch (Mode)
			{
				case CameraMode.Combat:
					if (AutoAim)
					{
						var aim = Target.transform.eulerAngles.y;
						Foe nearest = null;
						var nearestDistance = 30f;
						foreach (var foe in Foe.All)
						{
							if (!foe.Engaged) continue;
							var d = Vector3.Distance(foe.transform.position, Target.transform.position);
							if (d < nearestDistance) { nearest = foe; nearestDistance = d; }
						}
						if (nearest != null)
						{
							var to = nearest.transform.position - Target.transform.position;
							aim = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
						}
						_yaw = Mathf.MoveTowardsAngle(_yaw, aim, 160f * dt);
						_pitch = Mathf.MoveTowards(_pitch, 14f, 60f * dt);
					}
					else
					{
						var look = EngineInput.Default.ReadLookDegrees();
						_yaw += look.x * LookSensitivity;
						_pitch = Mathf.Clamp(_pitch - look.y * LookSensitivity, -15f, 55f);
					}
					rotation = Quaternion.Euler(_pitch, _yaw, 0f);
					var pivot = Target.transform.position + Vector3.up * Height + rotation * Vector3.right * Shoulder;
					var back = -(rotation * Vector3.forward);
					var reach = Distance;
					if (Physics.Raycast(pivot, back, out var hit, Distance + 0.3f, ~0, QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<Hero>() == null)
						reach = Mathf.Max(0.8f, hit.distance - 0.3f);
					position = pivot + back * reach;
					fov = 58f;
					blend = 12f;
					break;
				case CameraMode.Cinematic:
					position = _shotPosition;
					rotation = Quaternion.LookRotation(_shotLookAt - _shotPosition);
					fov = _shotFov;
					break;
				default:
					if (ActiveZone != null && ActiveZone.Anchor != null)
					{
						position = ActiveZone.Anchor.position;
						fov = ActiveZone.Fov;
					}
					else
					{
						position = focus + FallbackOffset;
						fov = 50f;
					}
					rotation = Quaternion.LookRotation(focus - position);
					break;
			}

			if (_snap)
			{
				_snap = false;
				transform.SetPositionAndRotation(position, rotation);
				_camera.fieldOfView = fov;
				return;
			}

			var k = 1f - Mathf.Exp(-blend * dt);
			var turn = 1f - Mathf.Exp(-(Mode == CameraMode.Exploration ? TrackSpeed : blend) * dt);
			transform.position = Vector3.Lerp(transform.position, position, k);
			transform.rotation = Quaternion.Slerp(transform.rotation, rotation, turn);
			_camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, fov, k);
		}
	}
}
