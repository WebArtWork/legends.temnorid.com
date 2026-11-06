using ITKamianets.Engine.Character;
using ITKamianets.Engine.Core;
using ITKamianets.Engine.Input;
using ITKamianets.Engine.Shell;
using ITKamianets.Engine.UI;
using Temnorid.Village;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Temnorid.Legends
{
	/// <summary>
	/// Where the game opens: the player's village (the standard one for now: Legends is not linked to Lords yet) with one elder in it. Walk up and
	/// press E to talk; if you agree, the story of <i>The Abandoned Village</i> begins.
	/// </summary>
	public sealed class LegendsHome : MonoBehaviour
	{
		/// <summary>The village scene: where the game opens and where the story returns.</summary>
		public const string HomeScene = "Main";
		/// <summary>The story scene (the first legend).</summary>
		public const string StoryScene = "Story";

		public VillageView View;
		public CharacterMotor Hero;
		[Tooltip("The text material the elder's name uses.")]
		public Material TextMaterial;

		const float TalkReach = 4.5f;
		const string Log = "Legends";

		static readonly Color Panel = new Color(0.07f, 0.06f, 0.06f, 0.92f);
		static readonly Color Ember = new Color(0.95f, 0.65f, 0.28f);

		static readonly string[] Words =
		{
			"Peace, traveller. A village on the border of Temnorid has gone silent. It sent no goods to the town, and the traders I sent to look never came back.",
			"The lord needs a warrior to find out what happened. You carry a sword and you do not look afraid. Will you go?",
		};

		UiBuilder _ui;
		ShellChrome _chrome;
		Transform _elder;
		bool _started;
		int _line = -1;
		GameObject _box;
		Text _speaker, _text, _prompt;
		GameObject _next, _go, _later;

		void Awake()
		{
			Time.timeScale = 1f;
			EngineInput.Default.PlayerEnabled = true;
			var session = VillageSource.CreateDefault(null, "Standard village.", SharedStock.SystemClock());
			if (session.Data.WallLevel < 2) session.Data.WallLevel = 2;
			View.Build(session.Data);
			foreach (var plot in View.Plots) if (plot.Root != null) plot.Root.gameObject.SetActive(false); // only the village: the farm's fields are not shown
			_elder = BuildElder(View.SpawnPosition + new Vector3(-4f, 0f, 7f));
		}

		// The hero wakes after this component (its motor is not ready in Awake), so it is put on its spot from here.
		void Start()
		{
			_started = true;
			if (Hero != null) Hero.Teleport(View.SpawnPosition, 0f);

			_ui = ShellUi.Builder();
			UiBuilder.EnsureEventSystem(transform);
			_chrome = ShellChrome.Create(transform);
			_chrome.SetTitle("Village");

			var canvas = UiBuilder.CreateCanvas(transform, "HudCanvas");
			canvas.sortingOrder = 0;
			var safe = new GameObject("Safe", typeof(RectTransform), typeof(SafeAreaFitter));
			safe.transform.SetParent(canvas.transform, false);
			var hud = new GameObject("Hud", typeof(RectTransform));
			hud.transform.SetParent(safe.transform, false);
			var rect = (RectTransform)hud.transform;
			rect.anchorMin = new Vector2(0f, _chrome.BottomFraction);
			rect.anchorMax = new Vector2(1f, 1f - _chrome.TopFraction);
			rect.offsetMin = Vector2.zero;
			rect.offsetMax = Vector2.zero;

			_prompt = _ui.Label("Prompt", hud.transform, "", 42, TextAnchor.MiddleCenter, new Vector2(0.2f, 0.12f), new Vector2(0.8f, 0.22f), Color.white);

			_box = UiBuilder.Box("Dialogue", hud.transform, new Vector2(0.14f, 0.04f), new Vector2(0.86f, 0.4f), Panel);
			_speaker = _ui.Label("Speaker", _box.transform, "Elder Vasyl", 34, TextAnchor.UpperLeft, new Vector2(0.03f, 0.78f), new Vector2(0.6f, 0.97f), Ember);
			_text = _ui.Label("Text", _box.transform, "", 32, TextAnchor.UpperLeft, new Vector2(0.03f, 0.3f), new Vector2(0.97f, 0.8f), Color.white, wrap: true);
			_next = _ui.Button("Continue", _box.transform, new Vector2(0.3f, 0.04f), new Vector2(0.7f, 0.26f), Advance).gameObject;
			_go = _ui.Button("I will go", _box.transform, new Vector2(0.04f, 0.04f), new Vector2(0.48f, 0.26f), BeginStory).gameObject;
			_later = _ui.Button("Not now", _box.transform, new Vector2(0.52f, 0.04f), new Vector2(0.96f, 0.26f), CloseDialogue).gameObject;
			_box.SetActive(false);
		}

		void Update()
		{
			if (!_started || _prompt == null || Hero == null) return;
			if (_line >= 0)
			{
				_prompt.text = "";
				if (EngineInput.Default.UI.Pause.WasPressedThisFrame()) CloseDialogue();
				return;
			}

			var gap = _elder.position - Hero.transform.position;
			gap.y = 0f;
			var near = gap.magnitude < TalkReach;
			_prompt.text = near ? "E  -  talk to Elder Vasyl" : "";
			if (near && EngineInput.Default.Player.Interact.WasPressedThisFrame()) OpenDialogue();
		}

		void OpenDialogue()
		{
			_line = 0;
			EngineInput.Default.PlayerEnabled = false; // frees the cursor for the buttons
			_box.SetActive(true);
			Show();
		}

		void Show()
		{
			_text.text = Words[_line];
			var last = _line >= Words.Length - 1;
			_next.SetActive(!last);
			_go.SetActive(last);
			_later.SetActive(last);
			UiBuilder.Select(_box);
		}

		void Advance()
		{
			if (_line < Words.Length - 1)
			{
				_line++;
				Show();
			}
		}

		void CloseDialogue()
		{
			_line = -1;
			_box.SetActive(false);
			EngineInput.Default.PlayerEnabled = true;
		}

		/// <summary>The elder is told yes: the story of The Abandoned Village begins (or goes on from its last checkpoint).</summary>
		void BeginStory()
		{
			EngineLog.Info(Log, "The warrior accepts: the story begins.");
			LegendsGame.AutoContinue = true;
			Time.timeScale = 1f;
			EngineInput.Default.PlayerEnabled = true;
			SceneManager.LoadScene(StoryScene);
		}

		// ---- the elder ----

		Transform BuildElder(Vector3 position)
		{
			var robe = Colour(new Color(0.42f, 0.3f, 0.2f));
			var skin = Colour(new Color(0.85f, 0.68f, 0.52f));
			var beard = Colour(new Color(0.9f, 0.9f, 0.88f));
			var wood = Colour(new Color(0.35f, 0.24f, 0.14f));
			var gold = Colour(new Color(1f, 0.82f, 0.25f));

			var root = new GameObject("Elder Vasyl").transform;
			root.position = position;
			var capsule = root.gameObject.AddComponent<CapsuleCollider>();
			capsule.center = new Vector3(0f, 0.9f, 0f);
			capsule.radius = 0.5f;
			Piece(PrimitiveType.Cylinder, "Robe", root, new Vector3(0f, 0.85f, 0f), new Vector3(0.9f, 0.85f, 0.9f), robe);
			Piece(PrimitiveType.Sphere, "Head", root, new Vector3(0f, 1.8f, 0f), new Vector3(0.42f, 0.42f, 0.42f), skin);
			Piece(PrimitiveType.Cube, "Beard", root, new Vector3(0f, 1.52f, 0.17f), new Vector3(0.3f, 0.45f, 0.12f), beard);
			Piece(PrimitiveType.Sphere, "Hat", root, new Vector3(0f, 2.02f, 0f), new Vector3(0.5f, 0.28f, 0.5f), robe);
			Piece(PrimitiveType.Cylinder, "Staff", root, new Vector3(0.6f, 1.0f, 0.2f), new Vector3(0.07f, 1.0f, 0.07f), wood);
			Piece(PrimitiveType.Sphere, "Knob", root, new Vector3(0.6f, 2.05f, 0.2f), new Vector3(0.2f, 0.2f, 0.2f), gold);
			if (TextMaterial != null)
			{
				Label(root, "Name", "Elder Vasyl", new Vector3(0f, 2.9f, 0f), 0.09f, Color.white);
				Label(root, "Mark", "!", new Vector3(0f, 3.5f, 0f), 0.2f, new Color(1f, 0.82f, 0.25f));
			}
			// The elder faces the hero's spot.
			var toSpawn = View.SpawnPosition - position;
			toSpawn.y = 0f;
			if (toSpawn.sqrMagnitude > 0.01f) root.rotation = Quaternion.LookRotation(toSpawn);
			return root;
		}

		void Label(Transform parent, string name, string text, Vector3 localPosition, float size, Color colour)
		{
			var go = new GameObject(name);
			go.transform.SetParent(parent, false);
			go.transform.localPosition = localPosition;
			go.AddComponent<VillageLabel>();
			var mesh = go.AddComponent<TextMesh>();
			mesh.text = text;
			mesh.characterSize = size;
			mesh.fontSize = 64;
			mesh.anchor = TextAnchor.MiddleCenter;
			mesh.alignment = TextAlignment.Center;
			mesh.color = colour;
			mesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
			go.GetComponent<MeshRenderer>().sharedMaterial = TextMaterial;
		}

		static Material Colour(Color colour) => ITKamianets.Engine.Feedback.Fx.Material(colour);

		static void Piece(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 scale, Material material)
		{
			var piece = GameObject.CreatePrimitive(type);
			piece.name = name;
			Destroy(piece.GetComponent<Collider>());
			piece.transform.SetParent(parent, false);
			piece.transform.localPosition = localPosition;
			piece.transform.localScale = scale;
			piece.GetComponent<Renderer>().sharedMaterial = material;
		}
	}
}
