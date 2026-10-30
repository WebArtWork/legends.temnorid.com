using UnityEngine;

namespace Temnorid.Legends
{
	/// <summary>A volume that tells the story the hero walked into it. Each runs once (see <see cref="LegendsGame.OnTrigger"/>).</summary>
	[RequireComponent(typeof(BoxCollider))]
	public sealed class StoryTrigger : MonoBehaviour
	{
		public string Id;

		void OnTriggerEnter(Collider other)
		{
			if (other.GetComponentInParent<Hero>() != null && LegendsGame.Instance != null) LegendsGame.Instance.OnTrigger(Id);
		}
	}
}
