using UnityEngine;

namespace Temnorid.Legends
{
	/// <summary>
	/// A fixed camera for a place. While the hero is inside the volume, the camera sits at <see cref="Anchor"/>
	/// and turns to follow the hero, like the fixed shots of the classic survival horror games.
	/// </summary>
	[RequireComponent(typeof(BoxCollider))]
	public sealed class CameraZone : MonoBehaviour
	{
		public Transform Anchor;
		public float Fov = 50f;
		/// <summary>When zones overlap, the higher priority wins.</summary>
		public int Priority;
		/// <summary>Jump to this shot at once instead of gliding.</summary>
		public bool Cut;

		void OnTriggerEnter(Collider other)
		{
			if (other.GetComponentInParent<Hero>() == null) return;
			var rig = CameraRig.Instance;
			if (rig != null) rig.ZoneEntered(this);
		}

		void OnTriggerExit(Collider other)
		{
			if (other.GetComponentInParent<Hero>() == null) return;
			var rig = CameraRig.Instance;
			if (rig != null) rig.ZoneExited(this);
		}

		void OnDisable()
		{
			var rig = CameraRig.Instance;
			if (rig != null) rig.ZoneExited(this);
		}
	}
}
