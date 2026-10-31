using ITKamianets.Engine.Net;
using UnityEngine;

namespace Temnorid.Legends
{
	/// <summary>
	/// The other player's warrior as this machine sees it: a plain object built from primitives that glides to the pose its owner
	/// publishes. It has no collider and no health of its own; foes may hunt it (on the host), and a blow that lands on it is sent
	/// to its owner, who decides whether the shield or a roll stopped it.
	/// </summary>
	public sealed class RemoteHero : MonoBehaviour, IPrey
	{
		public const int Blocking = 1;
		public const int Swinging = 2;
		public const int Rolling = 4;
		public const int Down = 8;

		public ulong Owner;
		public LegendsNet Net;
		public int State { get; private set; }
		public float HealthFraction { get; private set; } = 1f;

		Transform _visual;
		Transform _sword;
		Transform _shield;
		TextMesh _name;
		RemoteBody _body;
		Vector3 _last;

		public Vector3 Position => this == null ? _last : transform.position;
		public bool Alive => this != null && (State & Down) == 0 && HealthFraction > 0f;

		public static RemoteHero Create(ulong owner, string playerName, Color colour, LegendsNet net)
		{
			var go = new GameObject("RemoteHero " + playerName);
			var hero = go.AddComponent<RemoteHero>();
			hero.Owner = owner;
			hero.Net = net;
			hero.Build(playerName, colour);
			return hero;
		}

		void Build(string playerName, Color colour)
		{
			_body = gameObject.AddComponent<RemoteBody>();
			_visual = new GameObject("Visual").transform;
			_visual.SetParent(transform, false);
			var mail = Tint(colour * 0.7f);
			var steel = Tint(new Color(0.72f, 0.74f, 0.8f));
			Prim(PrimitiveType.Capsule, "Torso", _visual, new Vector3(0f, 0.9f, 0f), new Vector3(0.7f, 0.9f, 0.7f), mail);
			Prim(PrimitiveType.Sphere, "Head", _visual, new Vector3(0f, 1.65f, 0.02f), new Vector3(0.38f, 0.38f, 0.38f), Tint(new Color(0.8f, 0.66f, 0.55f)));
			Prim(PrimitiveType.Sphere, "Helm", _visual, new Vector3(0f, 1.72f, 0.02f), new Vector3(0.42f, 0.3f, 0.42f), steel);

			_sword = new GameObject("SwordPivot").transform;
			_sword.SetParent(_visual, false);
			_sword.localPosition = new Vector3(0.45f, 1.0f, 0.1f);
			Prim(PrimitiveType.Cube, "Blade", _sword, new Vector3(0f, 0f, 0.55f), new Vector3(0.07f, 0.07f, 1.0f), steel);

			_shield = new GameObject("ShieldPivot").transform;
			_shield.SetParent(_visual, false);
			_shield.localPosition = new Vector3(-0.5f, 1.0f, 0.1f);
			Prim(PrimitiveType.Cube, "Shield", _shield, Vector3.zero, new Vector3(0.7f, 0.9f, 0.1f), colour);

			var label = new GameObject("Name");
			label.transform.SetParent(transform, false);
			label.transform.localPosition = new Vector3(0f, 2.3f, 0f);
			_name = label.AddComponent<TextMesh>();
			_name.text = playerName;
			_name.characterSize = 0.12f;
			_name.fontSize = 48;
			_name.anchor = TextAnchor.MiddleCenter;
			_name.color = Color.white;
		}

		static Color Tint(Color c)
		{
			c.a = 1f;
			return c;
		}

		static void Prim(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Color colour)
		{
			var go = GameObject.CreatePrimitive(type);
			go.name = name;
			Destroy(go.GetComponent<Collider>());
			go.transform.SetParent(parent, false);
			go.transform.localPosition = position;
			go.transform.localScale = scale;
			var renderer = go.GetComponent<Renderer>();
			var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
			renderer.material = new Material(shader) { color = colour };
		}

		void OnEnable()
		{
			if (!Foe.Prey.Contains(this)) Foe.Prey.Add(this);
		}

		void OnDisable() => Foe.Prey.Remove(this);

		public void SetName(string value)
		{
			if (_name != null && _name.text != value) _name.text = value;
		}

		/// <summary>Where the owner says the hero is and what it is doing.</summary>
		public void Apply(NetPose pose)
		{
			_body.Apply(pose);
			State = pose.State;
			HealthFraction = pose.A;
		}

		/// <summary>A foe's blow: the owner decides what became of it.</summary>
		public void Strike(float amount, Vector3 from, float blockStrength)
		{
			if (Net != null) Net.SendDamage(Owner, amount, from, blockStrength);
		}

		void Update()
		{
			_last = transform.position;
			var swinging = (State & Swinging) != 0;
			var blocking = (State & Blocking) != 0;
			_sword.localRotation = Quaternion.Euler(swinging ? 20f : 15f, 0f, 0f);
			var target = blocking ? new Vector3(0f, 1.1f, 0.45f) : new Vector3(-0.5f, 1.0f, 0.1f);
			_shield.localPosition = Vector3.Lerp(_shield.localPosition, target, 18f * Time.deltaTime);
			_shield.localRotation = Quaternion.Slerp(_shield.localRotation, Quaternion.Euler(0f, blocking ? 0f : 80f, 0f), 18f * Time.deltaTime);
			_visual.localScale = new Vector3(1f, (State & Rolling) != 0 ? 0.6f : 1f, 1f);
			_visual.localRotation = (State & Down) != 0 ? Quaternion.Euler(80f, 0f, 0f) : Quaternion.identity;

			var cam = Camera.main;
			if (cam != null) _name.transform.rotation = cam.transform.rotation;
		}
	}
}
