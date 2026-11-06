using System;
using System.Collections;
using ITKamianets.Engine.Combat;
using ITKamianets.Engine.Core;
using ITKamianets.Engine.Input;
using ITKamianets.Engine.Net;
using ITKamianets.Engine.Shell;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Temnorid.Legends
{
	public enum LegendsPhase
	{
		Menu,
		Playing,
		Dialogue,
		/// <summary>Reading a document or a carved plate.</summary>
		Reading,
		Cinematic,
		Dead,
		/// <summary>The demo is over.</summary>
		Ended,
	}

	/// <summary>
	/// Runs "The Abandoned Village": it starts and ends the story, answers every interaction and trigger,
	/// switches the camera between exploring and fighting, plays the short scenes and keeps the checkpoints.
	/// All the story logic is here; the world objects only say what they are.
	/// </summary>
	[DefaultExecutionOrder(-100)]
	public sealed class LegendsGame : MonoBehaviour
	{
		/// <summary>Where saves go instead of the persistent data path. Tests set it before loading the scene.</summary>
		public static string SaveRootOverride;

		/// <summary>Set before reloading the scene to carry on from the last checkpoint instead of showing the menu.</summary>
		public static bool AutoContinue;

		public static LegendsGame Instance { get; private set; }

		/// <summary>Shows the touch controls and touch wording even where there is no touch screen. Tests set it.</summary>
		public static bool ForceTouch;

		/// <summary>True on phones and tablets: no keyboard or mouse to count on.</summary>
		public static bool TouchUi => Application.isMobilePlatform || ForceTouch;

		[Header("Scene")]
		public Hero Hero;
		public CameraRig Rig;
		public NavMeshSurface Surface;
		public Foe Ambusher;
		public Foe Boss;
		/// <summary>Where the hero stands at each checkpoint.</summary>
		public Transform[] Checkpoints;
		public Transform ShotRoot;
		public Transform WindowFigure;
		public Transform RitualBody;
		public Light Sun;

		public bool SaveEnabled = true;

		/// <summary>The link to the other player. Null offline, and a single player behaves exactly as before.</summary>
		public LegendsNet Net { get; private set; }

		/// <summary>True once the story has been started (on a guest: once the host has started it).</summary>
		public bool Begun => Phase != LegendsPhase.Menu;

		/// <summary>A shell screen (the profile) is open over the game: the hero stands still.</summary>
		public bool Overlay { get; private set; }

		const string Log = "Legends";
		const float DungeonX = 150f;

		LegendsSaveStore _store;
		InputAction _inventory;
		InputAction _heal;
		Coroutine _scene;
		string[] _lines;
		int _lineIndex;
		Action _afterDialogue;
		float _combatHold;
		float _promptUntilDark;
		float _respawnAt;
		ulong _actor;
		bool _mine = true;
		bool _worldApplied;
		bool _darkWarned;

		public LegendsSession Session { get; private set; }
		public SymbolPuzzle Puzzle { get; private set; }
		public LegendsPhase Phase { get; private set; } = LegendsPhase.Menu;
		public bool Paused { get; private set; }
		public bool InventoryOpen { get; private set; }
		public bool HasSave { get; private set; }

		public string DialogueSpeaker { get; private set; } = "";
		public string DialogueText { get; private set; } = "";
		public Note Reading { get; private set; }
		public string Prompt { get; private set; } = "";
		public string ToastText { get; private set; } = "";
		public float ToastUntil { get; private set; }
		public string SubtitleText { get; private set; } = "";
		public float SubtitleUntil { get; private set; }
		public float FadeAlpha { get; private set; }

		/// <summary>True while a fight is on (and for a moment after).</summary>
		public bool InCombat => Phase == LegendsPhase.Playing && _combatHold > 0f;

		/// <summary>True when the player may move the hero.</summary>
		public bool CanControl => Phase == LegendsPhase.Playing && !Paused && !InventoryOpen && !Overlay;

		public bool InDungeon => Hero != null && Hero.transform.position.x > DungeonX;

		public bool BossVisible => Boss != null && Boss.gameObject.activeSelf && Boss.Engaged;

		public string Objective => Story.Objective(Session.State);

		void Awake()
		{
			Instance = this;
			Time.timeScale = 1f;
			EngineInput.Default.PlayerEnabled = true;

			var map = EngineInput.Default.Asset.FindActionMap("Legends")
				?? EngineInput.Default.AddMap("Legends", m =>
				{
					m.AddAction("Inventory", InputActionType.Button, "<Keyboard>/i", groups: EngineInputActions.Groups.KeyboardMouse);
					m.AddAction("Heal", InputActionType.Button, "<Keyboard>/h", groups: EngineInputActions.Groups.KeyboardMouse);
				});
			_inventory = map.FindAction("Inventory");
			_heal = map.FindAction("Heal");

			Session = new LegendsSession();
			Puzzle = new SymbolPuzzle();
			_store = new LegendsSaveStore(SaveRootOverride);
			var saved = _store.Load();
			HasSave = saved != null && saved.checkpoint > 0;
			Session.Load(saved);

			// The foes wait, switched off, until the floor exists to walk on.
			Surface.BuildNavMesh();
			if (Ambusher != null) Ambusher.gameObject.SetActive(true);

			Hero.GetComponent<Health>().Died += OnHeroDied;

			if (NetSession.Instance.IsOnline)
			{
				Net = gameObject.AddComponent<LegendsNet>();
				Net.Init(this);
				if (Net.IsClient)
				{
					// The host's story is the story: nothing of this machine's own save is used or touched.
					Session.StartNew();
					HasSave = false;
				}
			}
		}

		void Start()
		{
			RefreshLabels();
			Place(Session.Checkpoint);
			ApplyWorld(true);
			if (AutoContinue && Net == null)
			{
				AutoContinue = false;
				Begin(false);
			}
		}

		void OnEnable()
		{
			Foe.Killed += OnFoeKilled;
			ShellFlow.BeforeQuit += OnBeforeQuit;
		}

		void OnDisable()
		{
			Foe.Killed -= OnFoeKilled;
			ShellFlow.BeforeQuit -= OnBeforeQuit;
		}

		void OnBeforeQuit()
		{
			if (Phase != LegendsPhase.Menu && (Net == null || Net.IsHost)) Save();
		}

		void OnDestroy()
		{
			if (Instance == this) Instance = null;
			Time.timeScale = 1f;
			Cursor.lockState = CursorLockMode.None;
			Cursor.visible = true;
		}

		// ---- starting, checkpoints, dying ----

		/// <summary>Leaves the menu. A new game starts from the road; otherwise the last checkpoint.</summary>
		public void Begin(bool newGame)
		{
			if (Phase != LegendsPhase.Menu) return;
			if (Net != null && Net.IsClient) return; // the host starts the story
			if (newGame)
			{
				Session.StartNew();
				Save();
				Place(0);
				ApplyWorld(true);
			}
			Phase = LegendsPhase.Playing;
			ShowToast(newGame ? "Temnorid Legends: The Abandoned Village" : "Back at the last checkpoint.");
			EngineLog.Info(Log, newGame ? "New game." : $"Continued from checkpoint {Session.Checkpoint}.");
			if (Net != null)
			{
				Net.Flush();
				Net.AnnounceBegin();
			}
		}

		/// <summary>A guest: the host has started the story. Stands at the shared checkpoint.</summary>
		public void BeginRemote()
		{
			if (Phase != LegendsPhase.Menu) return;
			Place(Session.Checkpoint);
			Phase = LegendsPhase.Playing;
			ShowToast("You join the other hero in the village.");
		}

		void Place(int checkpoint)
		{
			var spot = Checkpoints[Mathf.Clamp(checkpoint, 0, Checkpoints.Length - 1)];
			Hero.Teleport(spot.position, spot.eulerAngles.y);
			Rig.SetMode(CameraMode.Exploration);
			Rig.Snap();
		}

		public void SetCheckpoint(int index)
		{
			if (Net != null && Net.IsClient) return; // only the host keeps the checkpoints
			Session.SetCheckpoint(index);
			Save();
			Net?.MarkDirty();
			Announce("Checkpoint saved.");
			EngineLog.Info(Log, $"Checkpoint {index}.");
		}

		void OnHeroDied(Health health, DamageInfo info)
		{
			if (Phase == LegendsPhase.Dead || Phase == LegendsPhase.Ended) return;
			if (_scene != null) StopCoroutine(_scene);
			_scene = null;
			Phase = LegendsPhase.Dead;
			_respawnAt = Time.unscaledTime + 4f;
Rig.SetMode(CameraMode.Exploration);
			EngineLog.Info(Log, "The hero died.");
		}

		/// <summary>Reloads the scene and carries on from the last checkpoint.</summary>
		public void Retry()
		{
			AutoContinue = true;
			Time.timeScale = 1f;
			SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
		}

		/// <summary>Online, a fallen hero gets up at the shared checkpoint, so nobody waits forever.</summary>
		void Respawn()
		{
			var spot = Checkpoints[Mathf.Clamp(Session.Checkpoint, 0, Checkpoints.Length - 1)];
			Hero.Revive(spot.position, spot.eulerAngles.y);
			Phase = LegendsPhase.Playing;
			Rig.SetMode(CameraMode.Exploration);
			Rig.Snap();
			ShowToast("You rise again at the last checkpoint.");
		}

		/// <summary>Leaves the story for the village, where it began (the shell's menu is not used: the game opens in the village).</summary>
		public void BackToMenu()
		{
			AutoContinue = false;
			Time.timeScale = 1f;
			if (Net != null) ShellFlow.QuitToMenu();
			else SceneManager.LoadScene(LegendsHome.HomeScene);
		}

		public void Save()
		{
			if (SaveEnabled) _store.Save(Session.ToSave());
		}

		public void Quit()
		{
			Time.timeScale = 1f;
#if UNITY_EDITOR
			UnityEditor.EditorApplication.isPlaying = false;
#else
			Application.Quit();
#endif
		}

		public void SetPaused(bool paused)
		{
			if (Phase != LegendsPhase.Playing && !Paused) return;
			Paused = paused;
			// Online the world belongs to the host and keeps running: a pause is only a menu.
			Time.timeScale = Net == null && (paused || Overlay) ? 0f : 1f;
			}

			/// <summary>A shell screen (the profile) opens or closes over the game.</summary>
			public void SetOverlay(bool open)
			{
				Overlay = open;
				if (Net == null) Time.timeScale = open || Paused ? 0f : 1f;
			}

		/// <summary>Opens or closes the journal (the pack and the notes), as the Journal tab does.</summary>
		public void SetInventory(bool open)
		{
			if (open && (Phase != LegendsPhase.Playing || Paused)) return;
			InventoryOpen = open;
		}

		public void ToggleInventory()
		{
			if (Phase != LegendsPhase.Playing || Paused) return;
			InventoryOpen = !InventoryOpen;
		}

		// ---- every frame ----

		void Update()
		{
			if (Phase == LegendsPhase.Dead && Net != null && Time.unscaledTime >= _respawnAt) Respawn();
			if (Phase == LegendsPhase.Menu || Phase == LegendsPhase.Ended) { UpdateCursor(); return; }
			var input = EngineInput.Default;

			if (input.UI.Pause.WasPressedThisFrame() && (Phase == LegendsPhase.Playing || Paused)) SetPaused(!Paused);
			if (Paused) { UpdateCursor(); return; }

			UpdateLighting();

			switch (Phase)
			{
				case LegendsPhase.Playing:
					UpdatePlaying(input);
					break;
				case LegendsPhase.Dialogue:
					if (input.Player.Interact.WasPressedThisFrame() || input.UI.Submit.WasPressedThisFrame()) AdvanceDialogue();
					break;
				case LegendsPhase.Reading:
					if (input.Player.Interact.WasPressedThisFrame() || input.UI.Submit.WasPressedThisFrame() || input.UI.Cancel.WasPressedThisFrame()) CloseReading();
					break;
			}
			UpdateCursor();
		}

		void UpdatePlaying(EngineInput input)
		{
			var engaged = false;
			foreach (var foe in Foe.All)
				if (foe.Engaged && (Net == null || (foe.transform.position - Hero.transform.position).magnitude < 30f)) engaged = true;
			if (engaged) _combatHold = 1.4f;
			else if (_combatHold > 0f) _combatHold -= Time.deltaTime;
			Rig.SetMode(InCombat ? CameraMode.Combat : CameraMode.Exploration);

			if (_inventory.WasPressedThisFrame()) ToggleInventory();
			if (InventoryOpen) { Prompt = ""; return; }
			if (_heal.WasPressedThisFrame()) UseHeal();

			var near = InCombat ? null : Interactable.Nearest(Hero.transform.position, Hero.transform.forward, 2.4f);
			Prompt = near != null ? $"{(TouchUi ? "Use" : "E")}: {near.Prompt}" : "";
			if (near != null && input.Player.Interact.WasPressedThisFrame()) OnInteract(near);
		}

		/// <summary>Uses the nearest thing in reach, as the interact key does. The touch Use button calls this.</summary>
		public bool TryInteract()
		{
			if (!CanControl || InCombat) return false;
			var near = Interactable.Nearest(Hero.transform.position, Hero.transform.forward, 2.4f);
			if (near == null) return false;
			OnInteract(near);
			return true;
		}

		void UpdateCursor()
		{
			var lockIt = !Application.isMobilePlatform && InCombat && !Paused && !InventoryOpen;
			Cursor.lockState = lockIt ? CursorLockMode.Locked : CursorLockMode.None;
			Cursor.visible = !lockIt;
		}

		void UpdateLighting()
		{
			var dungeon = InDungeon;
			var dawn = Session.State.Has("done:ending");
			RenderSettings.ambientLight = dungeon ? new Color(0.03f, 0.03f, 0.045f) : dawn ? new Color(0.6f, 0.55f, 0.5f) : new Color(0.42f, 0.44f, 0.55f);
			if (Sun != null) Sun.enabled = !dungeon;
			var hasTorch = Session.Has(Items.Torch);
			Hero.SetTorch(dungeon && hasTorch);
			if (dungeon && !hasTorch && !_darkWarned)
			{
				_darkWarned = true;
				ShowToast("It is far too dark to go on. You need a light.");
			}
		}

		public bool UseHeal()
		{
			var health = Hero.Health;
			if (!health.IsAlive || health.Fraction >= 0.999f) return false;
			if (!Session.UsePotion())
			{
				ShowToast("You have no healing potion.");
				return false;
			}
			Hero.Heal(Fighting.PotionHeal);
			if (Net != null)
			{
				if (Net.IsClient) Net.SendPotion();
				else Net.MarkDirty();
			}
ShowToast("You drink a healing potion.");
			return true;
		}

		// ---- interactions ----

		/// <summary>The hero uses something. Public so tests can play the story.</summary>
		public void OnInteract(Interactable item)
		{
			if (Net != null && Net.IsClient)
			{
				InteractAsGuest(item);
				return;
			}
			Interact(item, false, 0UL, true);
		}

		/// <summary>A guest shows what it can at once (a document, a conversation) and asks the host to change the shared story.</summary>
		void InteractAsGuest(Interactable item)
		{
			switch (item.Kind)
			{
				case InteractKind.Note:
					Read(Story.Note(item.Id));
					Net.SendUse(item.Id);
					break;
				case InteractKind.Talk:
					if (Session.State.Has("talked:" + item.Id))
					{
						ShowToast("He has nothing more to say.");
						break;
					}
					StartDialogue("Wounded villager", Story.VillagerLines, () =>
					{
						Net.SendUse(item.Id + "|done");
						Play(WindowScene());
					});
					break;
				default:
					Net.SendUse(item.Id);
					break;
			}
		}

		/// <summary>Host: the other player's hero uses something (already checked for distance).</summary>
		public void HostUse(Interactable item, bool done, ulong who) => Interact(item, done, who, false);

		void Tell(string text)
		{
			if (_mine) ShowToast(text);
			else Net?.ToastTo(_actor, text);
		}

		/// <summary>A line everybody sees.</summary>
		void Announce(string text)
		{
			ShowToast(text);
			if (Net != null && Net.IsHost) Net.ToastAll(text);
		}

		void Interact(Interactable item, bool done, ulong who, bool mine)
		{
			_actor = who;
			_mine = mine;
			switch (item.Kind)
			{
				case InteractKind.Note:
					if (mine) Read(Story.Note(item.Id));
					else Session.State.Set("note:" + item.Id);
					break;
				case InteractKind.Pickup:
					Pick(item);
					break;
				case InteractKind.Door:
					if (!string.IsNullOrEmpty(item.RequiresItem) && !Session.Has(item.RequiresItem))
					{
						Tell("It is locked.");
						break;
					}
					Session.State.Set("open:" + item.Id);
					RefreshGates();
					Tell("The door is unlocked.");
					break;
				case InteractKind.Talk:
					if (mine) Talk(item);
					else if (done && !Session.State.Has("talked:" + item.Id)) FinishTalk(item);
					break;
				case InteractKind.Panel:
					Puzzle.Cycle(item.Slot);
					RefreshLabels();
					if (Puzzle.IsSolved && !Session.State.Has("puzzle-solved"))
					{
						Session.State.Set("puzzle-solved");
						Session.State.Set("open:basement-door");
						RefreshGates();
						Announce("The seal breaks. The cellar door swings open.");
						SetCheckpoint(3);
					}
					Net?.MarkDirty();
					break;
				case InteractKind.Stairs:
					if (_scene == null)
					{
						if (Net != null) Net.AnnounceScene("descend");
						PlayAnnounced("descend");
					}
					break;
				case InteractKind.Altar:
					if (_scene == null && Session.State.Has("done:ritual-room") && !Session.State.Has("done:altar"))
					{
						if (Net != null) Net.AnnounceScene("altar");
						Play(BossAwakens());
					}
					break;
				case InteractKind.Artifact:
					Pick(item);
					if (_scene == null && Net != null) Net.AnnounceScene("ending");
					Play(Ending());
					break;
			}
			Net?.MarkDirty();
		}

		/// <summary>
		/// A story scene every player sees, started by the host's announcement (and, on the host, by the same call). Each machine
		/// plays it for its own hero and camera.
		/// </summary>
		public void PlayAnnounced(string scene)
		{
			switch (scene)
			{
				case "ritual":
					if (Net == null || Phase == LegendsPhase.Playing) Play(RitualScene());
					break;
				case "descend":
					if (Phase == LegendsPhase.Dead) break;
					ForcePlaying();
					if (_scene == null) _scene = StartCoroutine(Descend());
					break;
				case "altar":
					if (Net != null && Net.IsClient && Phase != LegendsPhase.Dead && _scene == null)
					{
						ForcePlaying();
						Play(BossView());
					}
					break;
				case "ending":
					if (Net != null && Net.IsClient && Phase != LegendsPhase.Dead)
					{
						ForcePlaying();
						Play(Ending());
					}
					break;
			}
		}

		void ForcePlaying()
		{
			if (Phase != LegendsPhase.Dialogue && Phase != LegendsPhase.Reading) return;
			Reading = null;
			_afterDialogue = null;
			Phase = LegendsPhase.Playing;
		}

		/// <summary>Host: a guest used a healing potion from the shared pack.</summary>
		public void HostPotion()
		{
			Session.UsePotion();
			Net?.MarkDirty();
		}

		/// <summary>Guest: the host's story, the pack and the lock arrive here.</summary>
		public void ApplyState(LegendsStatePacket packet)
		{
			Session.Load(packet.save);
			if (packet.puzzle != null)
				for (var i = 0; i < packet.puzzle.Length && i < Puzzle.Current.Length; i++) Puzzle.Current[i] = packet.puzzle[i];
			RefreshLabels();
			if (!_worldApplied)
			{
				_worldApplied = true;
				ApplyWorld(true);
				return;
			}
			RefreshGates();
			foreach (var foe in Foe.All)
				if (foe.Remote && Session.State.Has("dead:" + foe.Id)) foe.RemoteDie();
		}

void Read(Note note)
		{
			if (note == null) return;
			Session.State.Set("note:" + note.Id);
			Reading = note;
			Phase = LegendsPhase.Reading;
		}

		public void CloseReading()
		{
			Reading = null;
			Phase = LegendsPhase.Playing;
		}

		void Pick(Interactable item)
		{
			Session.State.Set("got:" + item.Id);
			if (!string.IsNullOrEmpty(item.ItemId))
			{
				Session.AddItem(item.ItemId, item.Quantity);
				Tell($"Picked up: {Items.Name(item.ItemId)}");
			}
			RefreshGates();
		}

		void Talk(Interactable person)
		{
			if (Session.State.Has("talked:" + person.Id))
			{
				ShowToast("He has nothing more to say.");
				return;
			}
			StartDialogue("Wounded villager", Story.VillagerLines, () =>
			{
				_actor = 0UL;
				_mine = true;
				FinishTalk(person);
				Play(WindowScene());
			});
		}

		void FinishTalk(Interactable person)
		{
			Session.State.Set("talked:" + person.Id);
			Session.AddItem(Items.Potion);
			Session.State.Set("got:village-potion");
			Tell("Received: Healing potion");
			Net?.MarkDirty();
		}

// ---- dialogue ----

		public void StartDialogue(string speaker, string[] lines, Action done)
		{
			DialogueSpeaker = speaker;
			_lines = lines;
			_lineIndex = 0;
			_afterDialogue = done;
			DialogueText = lines[0];
			Phase = LegendsPhase.Dialogue;
		}

		public void AdvanceDialogue()
		{
			if (Phase != LegendsPhase.Dialogue) return;
			_lineIndex++;
			if (_lineIndex < _lines.Length)
			{
				DialogueText = _lines[_lineIndex];
				return;
			}
			Phase = LegendsPhase.Playing;
			var done = _afterDialogue;
			_afterDialogue = null;
			done?.Invoke();
		}

		// ---- triggers ----

		/// <summary>The hero walked into a story volume. Each runs once. Either hero counts for both.</summary>
		public void OnTrigger(string id)
		{
			if (Net != null && Net.IsClient)
			{
				if (!Session.State.Has("done:" + id)) Net.SendTrigger(id);
				return;
			}
			ApplyTrigger(id, true);
		}

		/// <summary>Host: the other player's hero walked into a story volume.</summary>
		public void HostTrigger(string id) => ApplyTrigger(id, false);

		void ApplyTrigger(string id, bool local)
		{
			var key = "done:" + id;
			if (Session.State.Has(key) || (local && Phase != LegendsPhase.Playing)) return;
			switch (id)
			{
				case "village-gate":
					Session.State.Set(key);
					SetCheckpoint(1);
					Announce("The village is silent. No smoke, no voices.");
					break;
				case "square-listen":
					Session.State.Set(key);
					Announce("You hear knocking from a house nearby.");
					break;
				case "street-ambush":
					if (Session.State.Has("dead:ambush") || Ambusher == null) return;
					Session.State.Set(key);
					StartCoroutine(Ambush());
					break;
				case "ritual-room":
					Session.State.Set(key);
					SetCheckpoint(5);
					Net?.AnnounceScene("ritual");
					PlayAnnounced("ritual");
					break;
			}
			RefreshGates();
		}

void OnFoeKilled(Foe foe)
{
	if (Net != null && Net.IsClient) return;
Session.State.Set("dead:" + foe.Id);
			RefreshGates();
			if (foe.Id == "ambush")
			{
				SetCheckpoint(2);
				Announce("The villager falls. Whatever he was, he is gone.");
			}
			else if (foe.Id == "boss")
			{
				Announce("The guardian crumbles to dust.");
			}
		}

		// ---- the scenes ----

		void Play(IEnumerator routine)
		{
			if (_scene != null) return;
			_scene = StartCoroutine(RunScene(routine));
		}

		IEnumerator RunScene(IEnumerator routine)
		{
			Phase = LegendsPhase.Cinematic;
			Rig.SetMode(CameraMode.Cinematic);
			yield return routine;
			SubtitleText = "";
			if (Phase == LegendsPhase.Cinematic)
			{
				Phase = LegendsPhase.Playing;
				Rig.SetMode(CameraMode.Exploration);
			}
			_scene = null;
		}

		Transform Shot(string name)
		{
			var found = ShotRoot != null ? ShotRoot.Find(name) : null;
			if (found == null) throw new InvalidOperationException("Missing shot: " + name);
			return found;
		}

		void Frame(string shot, float fov = 48f)
		{
			Rig.SetShot(Shot(shot).position, Shot(shot + "-look").position, fov);
		}

		void Subtitle(string text, float seconds)
		{
			SubtitleText = text;
			SubtitleUntil = Time.unscaledTime + seconds;
		}

		IEnumerator WindowScene()
		{
			yield return null;
			Rig.Snap();
			Frame("window");
			yield return new WaitForSeconds(1.2f);
			Subtitle("Something moves past the window.", 4f);
			var a = Shot("figure-a").position;
			var b = Shot("figure-b").position;
			WindowFigure.gameObject.SetActive(true);
			for (var t = 0f; t < 1f; t += Time.deltaTime / 3.5f)
			{
				WindowFigure.position = Vector3.Lerp(a, b, t);
				yield return null;
			}
			WindowFigure.gameObject.SetActive(false);
			yield return new WaitForSeconds(0.8f);
			Subtitle("You must find the elder's house.", 3f);
			yield return new WaitForSeconds(1.2f);
		}

		IEnumerator Ambush()
		{
			Announce("A villager stands in the street, his back to you.");
			yield return Ambusher.TurnToward(Hero.transform.position, 2.2f);
			Ambusher.Wake(Hero);
		}

		IEnumerator Descend()
		{
			Phase = LegendsPhase.Cinematic;
			yield return Fade(1f, 0.8f);
			var spot = Checkpoints[4];
			Hero.Teleport(spot.position, spot.eulerAngles.y);
			Session.State.Set("done:descended");
			SetCheckpoint(4);
			RefreshGates();
			Rig.SetMode(CameraMode.Exploration);
			Rig.Snap();
			yield return Fade(0f, 0.8f);
			Phase = LegendsPhase.Playing;
			_scene = null;
		}

		IEnumerator RitualScene()
		{
			yield return null;
			Frame("ritual");
			yield return new WaitForSeconds(1.5f);
			Subtitle("Bodies lie around an old stone altar.", 3.5f);
			var start = RitualBody.localRotation;
			for (var t = 0f; t < 3f; t += Time.deltaTime)
			{
				RitualBody.localRotation = start * Quaternion.Euler(0f, 0f, Mathf.Sin(t * 9f) * Mathf.Clamp01(3f - t) * 10f);
				yield return null;
			}
			RitualBody.localRotation = start;
			Subtitle("One of them moved. Then it was still.", 3f);
			yield return new WaitForSeconds(2.5f);
		}

		IEnumerator BossAwakens()
		{
			Session.State.Set("done:altar");
			RefreshGates();
			Hero.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(Shot("boss-look").position - Hero.transform.position, Vector3.up));
			Frame("boss", 60f);
			Boss.gameObject.SetActive(true);
			var agent = Boss.GetComponent<UnityEngine.AI.NavMeshAgent>();
			agent.enabled = false;
			var up = Boss.transform.position;
			var down = up + Vector3.down * 3.5f;
			Subtitle("The altar cracks. Something enormous rises.", 4f);
			for (var t = 0f; t < 1f; t += Time.deltaTime / 3f)
			{
				Boss.transform.position = Vector3.Lerp(down, up, Mathf.SmoothStep(0f, 1f, t));
				yield return null;
			}
			Boss.transform.position = up;
			agent.enabled = true;
			agent.Warp(up);
			yield return new WaitForSeconds(0.8f);
			Boss.Wake(Hero);
		}

		/// <summary>A guest's view of the guardian rising: the same shot, while the host's guardian climbs out.</summary>
		IEnumerator BossView()
		{
			Hero.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(Shot("boss-look").position - Hero.transform.position, Vector3.up));
			Frame("boss", 60f);
			Boss.gameObject.SetActive(true);
			Subtitle("The altar cracks. Something enormous rises.", 4f);
			yield return new WaitForSeconds(4.6f);
		}

		IEnumerator Ending()
		{
			yield return new WaitForSeconds(0.6f);
			yield return Fade(1f, 1.2f);
			var spot = Checkpoints[Checkpoints.Length - 1];
			Hero.Teleport(spot.position, spot.eulerAngles.y);
			Session.State.Set("done:ending");
			if (Sun != null)
			{
				Sun.enabled = true;
				Sun.color = new Color(1f, 0.75f, 0.45f);
				Sun.intensity = 1.3f;
				Sun.transform.rotation = Quaternion.Euler(12f, 70f, 0f);
			}
			UpdateLighting();
			Rig.Snap();
			Frame("ending-a", 42f);
			yield return Fade(0f, 1.5f);
			Subtitle("The sun rises over the village. The artifact bears a symbol you do not know.", 6f);
			var from = Shot("ending-a").position;
			var to = Shot("ending-b").position;
			var look = Shot("ending-a-look").position;
			for (var t = 0f; t < 1f; t += Time.deltaTime / 9f)
			{
				Rig.SetShot(Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t)), look, 42f);
				yield return null;
			}
			Subtitle("This was only the first of the legends of Temnorid.", 8f);
			yield return new WaitForSeconds(4.5f);
			yield return Fade(1f, 1.5f);
			Phase = LegendsPhase.Ended;
			Save();
			EngineLog.Info(Log, "The demo is over.");
			yield return Fade(0.55f, 0.8f);
			_scene = null;
		}

		IEnumerator Fade(float target, float seconds)
		{
			var start = FadeAlpha;
			for (var t = 0f; t < 1f; t += Time.unscaledDeltaTime / seconds)
			{
				FadeAlpha = Mathf.Lerp(start, target, t);
				yield return null;
			}
			FadeAlpha = target;
		}

		// ---- the world follows the flags ----

		void RefreshGates()
		{
			if (Net != null && Net.IsHost) Net.MarkDirty();
foreach (var gate in FindObjectsByType<FlagGate>(FindObjectsInactive.Include, FindObjectsSortMode.None)) gate.Refresh(Session.State);
		}

		void RefreshLabels()
		{
			foreach (var item in Interactable.All)
				if (item.Kind == InteractKind.Panel && item.Label != null) item.Label.text = Puzzle.Name(item.Slot);
		}

		/// <summary>Makes the world look the way the saved flags say. On loading, dead foes are gone too.</summary>
		void ApplyWorld(bool loading)
		{
			RefreshGates();
			if (!loading) return;
			foreach (var foe in FindObjectsByType<Foe>(FindObjectsInactive.Include, FindObjectsSortMode.None))
				if (Session.State.Has("dead:" + foe.Id)) foe.gameObject.SetActive(false);
		}

		public void ShowToast(string text)
		{
			ToastText = text;
			ToastUntil = Time.unscaledTime + 4f;
		}
	}
}
