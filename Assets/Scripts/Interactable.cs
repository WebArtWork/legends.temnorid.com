using System.Collections.Generic;
using UnityEngine;

namespace Temnorid.Legends
{
	public enum InteractKind
	{
		/// <summary>A document or carved plate to read.</summary>
		Note,
		/// <summary>An item to pick up.</summary>
		Pickup,
		/// <summary>A locked door that a key opens.</summary>
		Door,
		/// <summary>A person to talk to.</summary>
		Talk,
		/// <summary>One pedestal of the three-symbol lock.</summary>
		Panel,
		/// <summary>Stairs down into the dungeon.</summary>
		Stairs,
		/// <summary>The altar that wakes the guardian.</summary>
		Altar,
		/// <summary>The artifact the guardian protects.</summary>
		Artifact,
	}

	/// <summary>
	/// Something the hero can use with the Interact key. This only says what it is; what happens is decided
	/// by <see cref="LegendsGame"/>, so the whole story lives in one place.
	/// </summary>
	public sealed class Interactable : MonoBehaviour
	{
		public static readonly List<Interactable> All = new List<Interactable>();

		public string Id;
		public InteractKind Kind;
		public string Prompt = "Interact";
		/// <summary>Pickup and artifact: the item given.</summary>
		public string ItemId;
		public int Quantity = 1;
		/// <summary>Door: the item that unlocks it.</summary>
		public string RequiresItem;
		/// <summary>Panel: which of the three pedestals this is (0 to 2).</summary>
		public int Slot;
		/// <summary>Panel: the label that shows the chosen symbol.</summary>
		public TextMesh Label;

		public bool Available => isActiveAndEnabled;

		void OnEnable() => All.Add(this);
		void OnDisable() => All.Remove(this);

		/// <summary>The closest available thing within <paramref name="range"/> metres and in front of the hero.</summary>
		public static Interactable Nearest(Vector3 from, Vector3 forward, float range)
		{
			Interactable best = null;
			var bestDistance = range;
			forward.y = 0f;
			foreach (var item in All)
			{
				var to = item.transform.position - from;
				to.y = 0f;
				var distance = to.magnitude;
				if (distance > bestDistance) continue;
				if (distance > 0.9f && Vector3.Angle(forward, to) > 105f) continue;
				best = item;
				bestDistance = distance;
			}
			return best;
		}
	}
}
