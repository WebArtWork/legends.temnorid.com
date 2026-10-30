using UnityEngine;

namespace Temnorid.Legends
{
	/// <summary>
	/// Shows or hides an object by story flags: a picked-up key disappears, an opened door goes, the artifact
	/// appears when the guardian is dead. This is what makes the world look right after loading a checkpoint.
	/// </summary>
	public sealed class FlagGate : MonoBehaviour
	{
		/// <summary>Visible only once this flag is set (empty: no condition).</summary>
		public string ShowWhen;
		/// <summary>Hidden once this flag is set (empty: no condition).</summary>
		public string HideWhen;

		public void Refresh(WorldState state)
		{
			var visible = (string.IsNullOrEmpty(ShowWhen) || state.Has(ShowWhen)) && (string.IsNullOrEmpty(HideWhen) || !state.Has(HideWhen));
			if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
		}
	}
}
