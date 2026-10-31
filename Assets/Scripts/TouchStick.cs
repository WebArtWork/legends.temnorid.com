using ITKamianets.Engine.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Temnorid.Legends
{
	/// <summary>A thumb stick for phones: drag inside the pad to walk; pushed all the way it means run.</summary>
	public sealed class TouchStick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
	{
		/// <summary>What the stick says, each axis from -1 to 1 (x right, y forward).</summary>
		public Vector2 Value { get; private set; }

		RectTransform _pad;
		RectTransform _knob;

		public static TouchStick Create(Transform parent, Vector2 min, Vector2 max)
		{
			var pad = UiBuilder.Box("Touch Stick", parent, min, max, new Color(1f, 1f, 1f, 0.12f));
			var stick = pad.AddComponent<TouchStick>();
			stick._pad = (RectTransform)pad.transform;
			var knob = UiBuilder.Box("Knob", pad.transform, new Vector2(0.35f, 0.35f), new Vector2(0.65f, 0.65f), new Color(1f, 1f, 1f, 0.35f));
			knob.GetComponent<Image>().raycastTarget = false;
			stick._knob = (RectTransform)knob.transform;
			return stick;
		}

		public void OnPointerDown(PointerEventData eventData) => Move(eventData);

		public void OnDrag(PointerEventData eventData) => Move(eventData);

		public void OnPointerUp(PointerEventData eventData) => Release();

		void Move(PointerEventData eventData)
		{
			if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_pad, eventData.position, eventData.pressEventCamera, out var local)) return;
			var half = _pad.rect.size * 0.5f;
			var v = new Vector2(local.x / Mathf.Max(1f, half.x), local.y / Mathf.Max(1f, half.y));
			if (v.magnitude > 1f) v.Normalize();
			Value = v.magnitude < 0.12f ? Vector2.zero : v;
			_knob.anchoredPosition = new Vector2(v.x * half.x * 0.6f, v.y * half.y * 0.6f);
		}

		void Release()
		{
			Value = Vector2.zero;
			if (_knob != null) _knob.anchoredPosition = Vector2.zero;
		}

		void OnDisable() => Release();
	}
}
