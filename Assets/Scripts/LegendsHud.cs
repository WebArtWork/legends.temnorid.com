using System.Text;
using ITKamianets.Engine.Shell;
using ITKamianets.Engine.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Temnorid.Legends
{
	/// <summary>All of the game's screens, drawn with plain uGUI built in code. It only shows what <see cref="LegendsGame"/> says.</summary>
	public sealed class LegendsHud : MonoBehaviour
	{
		public LegendsGame Game;

		static readonly Color Panel = new Color(0.06f, 0.05f, 0.07f, 0.86f);
		static readonly Color Ember = new Color(0.92f, 0.62f, 0.25f);
		static readonly Color Pale = new Color(0.92f, 0.9f, 0.85f);

		UiBuilder _ui;
		GameObject _menu, _hud, _dialogue, _reading, _inventory, _pause, _dead, _end;
		Image _fade, _health, _bossFill;
		GameObject _bossBar, _continueButton, _newButton, _menuMainButton, _retryButton, _touch;
		Text _menuWait, _deadWait;
		ShellChrome _chrome;
		TouchStick _stick;
		TouchButton _attack, _block, _roll, _use, _heal;
		float _statusAt;
Text _potions, _objective, _prompt, _toast, _subtitle, _speaker, _line, _noteTitle, _noteText, _inventoryText, _bossName;
		Text _menuHint;
		string _signature = "";

		void Start()
		{
			_ui = new UiBuilder
			{
				ButtonHighlight = new Color(1f, 0.9f, 0.6f),
				ButtonSelected = new Color(1f, 0.85f, 0.45f),
				ButtonPressed = new Color(0.8f, 0.65f, 0.3f),
			};
			UiBuilder.EnsureEventSystem(transform);
			var canvasRoot = UiBuilder.CreateCanvas(transform).transform;
			_chrome = ShellChrome.Create(transform);
			_chrome.PlaceSelected += OnPlace;

			// Everything of the game sits between the top bar and (on phones) the bottom tabs, inside the safe area.
			var safe = new GameObject("Safe", typeof(RectTransform), typeof(SafeAreaFitter));
			safe.transform.SetParent(canvasRoot, false);
			var frame = new GameObject("Frame", typeof(RectTransform));
			frame.transform.SetParent(safe.transform, false);
			var frameRect = (RectTransform)frame.transform;
			frameRect.anchorMin = new Vector2(0f, _chrome.BottomFraction);
			frameRect.anchorMax = new Vector2(1f, 1f - _chrome.TopFraction);
			frameRect.offsetMin = frameRect.offsetMax = Vector2.zero;
			var root = frame.transform;
			BuildHud(root);
			BuildDialogue(root);
			BuildReading(root);
			BuildInventory(root);
			BuildMenu(root);
			BuildPause(root);
			BuildDead(root);
			BuildEnd(root);
				if (TouchButton.IsNeeded || LegendsGame.ForceTouch) BuildTouch(_hud.transform);
			var fade = UiBuilder.Box("Fade", canvasRoot, Vector2.zero, Vector2.one, Color.black);
			_fade = fade.GetComponent<Image>();
			_fade.raycastTarget = false;
		}

		void OnDestroy()
		{
			if (_chrome != null) _chrome.PlaceSelected -= OnPlace;
		}

		void OnPlace(string id)
		{
			if (Game == null) return;
			Game.SetOverlay(id == ShellChrome.ProfilePlace);
			Game.SetInventory(id == "journal");
		}

		bool Guest => Game.Net != null && Game.Net.IsClient;

		void Update()
		{
			if (Game == null || _menu == null) return;
			SyncChrome();
			var signature = $"{Game.Phase}|{Game.Paused}|{Game.InventoryOpen}|{Game.HasSave}|{Guest}";
			if (signature != _signature)
			{
				_signature = signature;
				var phase = Game.Phase;
				var playing = phase == LegendsPhase.Playing || phase == LegendsPhase.Dialogue || phase == LegendsPhase.Reading || phase == LegendsPhase.Cinematic;
				_menu.SetActive(phase == LegendsPhase.Menu);
				_hud.SetActive(playing || phase == LegendsPhase.Dead);
				_dialogue.SetActive(phase == LegendsPhase.Dialogue);
				_reading.SetActive(phase == LegendsPhase.Reading);
				_inventory.SetActive(Game.InventoryOpen && phase == LegendsPhase.Playing);
				_pause.SetActive(Game.Paused);
				_dead.SetActive(phase == LegendsPhase.Dead);
				_end.SetActive(phase == LegendsPhase.Ended);
				_continueButton.SetActive(Game.HasSave && !Guest);
				_newButton.SetActive(!Guest);
				_menuWait.gameObject.SetActive(Guest);
				_retryButton.SetActive(Game.Net == null);
				_deadWait.gameObject.SetActive(Game.Net != null);
				UiBuilder.Select(Game.Paused ? _pause : phase == LegendsPhase.Menu ? _menu : phase == LegendsPhase.Dead ? _dead : phase == LegendsPhase.Ended ? _end : null);
			}

			var c = _fade.color;
			c.a = Game.FadeAlpha;
			_fade.color = c;

			if (_hud.activeSelf) UpdateHud();
			if (_touch != null) UpdateTouch();
			if (_dialogue.activeSelf)
			{
				UiBuilder.SetText(_speaker, Game.DialogueSpeaker);
				UiBuilder.SetText(_line, Game.DialogueText);
			}
			if (_reading.activeSelf && Game.Reading != null)
			{
				UiBuilder.SetText(_noteTitle, Game.Reading.Title);
				UiBuilder.SetText(_noteText, Game.Reading.Text);
			}
			if (_inventory.activeSelf) UiBuilder.SetText(_inventoryText, InventoryText());
		}

		void SyncChrome()
		{
			if (_chrome == null) return;
			if (!_chrome.ProfileOpen)
			{
				var place = _chrome.CurrentPlace;
				if (Game.InventoryOpen && place != "journal") _chrome.Select("journal", false);
				else if (!Game.InventoryOpen && place == "journal") _chrome.Select("village", false);
			}
			if (Time.unscaledTime >= _statusAt)
			{
				_statusAt = Time.unscaledTime + 0.5f;
				_chrome.SetStatus(Game.Phase == LegendsPhase.Menu ? "" : "Checkpoint " + Game.Session.Checkpoint);
			}
		}

		void BuildTouch(Transform parent)
		{
			_touch = new GameObject("Touch", typeof(RectTransform));
			_touch.transform.SetParent(parent, false);
			var rect = (RectTransform)_touch.transform;
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.offsetMin = rect.offsetMax = Vector2.zero;
			_stick = TouchStick.Create(_touch.transform, new Vector2(0.02f, 0.16f), new Vector2(0.2f, 0.58f));
			_attack = TouchButton.Create(_ui, _touch.transform, "Attack", new Vector2(0.84f, 0.12f), new Vector2(0.98f, 0.38f), 34);
			_roll = TouchButton.Create(_ui, _touch.transform, "Roll", new Vector2(0.84f, 0.42f), new Vector2(0.98f, 0.62f), 34);
			_block = TouchButton.Create(_ui, _touch.transform, "Block", new Vector2(0.69f, 0.1f), new Vector2(0.82f, 0.3f), 34);
			_use = TouchButton.Create(_ui, _touch.transform, "Use", new Vector2(0.69f, 0.34f), new Vector2(0.82f, 0.54f), 34);
			_heal = TouchButton.Create(_ui, _touch.transform, "Potion", new Vector2(0.22f, 0.3f), new Vector2(0.32f, 0.46f), 28);
		}

		/// <summary>Feeds the hero what the thumbs say, in the same way the keys would.</summary>
		void UpdateTouch()
		{
			var hero = Game.Hero;
			var show = Game.CanControl;
			if (_touch.activeSelf != show) _touch.SetActive(show);
			if (!show)
			{
				hero.MoveOverride = Vector2.zero;
				hero.BlockOverride = false;
				hero.SprintOverride = false;
				_roll.ConsumePress(); _attack.ConsumePress(); _use.ConsumePress(); _heal.ConsumePress();
				return;
			}
			var move = _stick.Value;
			hero.MoveOverride = move;
			hero.SprintOverride = move.magnitude > 0.9f;
			hero.BlockOverride = _block.Held;
			if (_roll.ConsumePress()) hero.RequestRoll();
			if (_attack.ConsumePress()) hero.RequestSwing();
			if (_use.ConsumePress()) Game.TryInteract();
			if (_heal.ConsumePress()) Game.UseHeal();
		}

		void UpdateHud()
		{
			var health = Game.Hero.Health;
			UiBuilder.SetFill(_health, health.Fraction);
			UiBuilder.SetText(_potions, $"Potions {Game.Session.PotionCount}" + (LegendsGame.TouchUi ? "" : "  [H]"));
			UiBuilder.SetText(_objective, "Objective: " + Game.Objective);
			UiBuilder.SetText(_prompt, Game.Phase == LegendsPhase.Playing ? Game.Prompt : "");
			_toast.text = Time.unscaledTime < Game.ToastUntil ? Game.ToastText : "";
			_subtitle.text = Time.unscaledTime < Game.SubtitleUntil ? Game.SubtitleText : "";

			_bossBar.SetActive(Game.BossVisible);
			if (Game.BossVisible)
			{
				UiBuilder.SetFill(_bossFill, Game.Boss.HealthFraction);
				UiBuilder.SetText(_bossName, Game.Boss.CurrentAttack.Length > 0 ? "THE GUARDIAN  -  " + Game.Boss.CurrentAttack.ToUpperInvariant() : "THE GUARDIAN");
			}
		}

		string InventoryText()
		{
			var s = Game.Session;
			var text = new StringBuilder();
			text.AppendLine("Objective: " + Game.Objective);
				text.AppendLine("Checkpoint " + s.Checkpoint);
				if (Game.Net != null) text.AppendLine("Two heroes share the story, the pack and the lock.");
				text.AppendLine();
				text.AppendLine("Pack");
			var any = false;
			foreach (var entry in s.Items.Items())
			{
				text.AppendLine($"   {Items.Name(entry.itemId)}  x{entry.quantity}");
				any = true;
			}
			if (!any) text.AppendLine("   (empty)");
			text.AppendLine();
			text.AppendLine("Documents found");
			var notes = false;
			foreach (var note in Story.Notes)
				if (s.State.Has("note:" + note.Id))
				{
					text.AppendLine("   " + note.Title + ": " + note.Text.Replace("\n", " "));
					notes = true;
				}
			if (!notes) text.AppendLine("   (none yet)");
			return text.ToString();
		}

		// ---- screens ----

		void BuildHud(Transform root)
		{
			_hud = new GameObject("Hud", typeof(RectTransform));
			_hud.transform.SetParent(root, false);
			var rect = (RectTransform)_hud.transform;
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.offsetMin = rect.offsetMax = Vector2.zero;

			var left = UiBuilder.Box("Left", _hud.transform, new Vector2(0.02f, 0.03f), new Vector2(0.28f, 0.115f), Panel);
			_health = UiBuilder.Bar("Health", left.transform, new Vector2(0.05f, 0.5f), new Vector2(0.95f, 0.85f), new Color(0.8f, 0.15f, 0.15f));
			_potions = _ui.Label("Potions", left.transform, "", 26, TextAnchor.MiddleLeft, new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.48f), Pale);

			_objective = _ui.Label("Objective", _hud.transform, "", 28, TextAnchor.UpperLeft, new Vector2(0.02f, 0.9f), new Vector2(0.6f, 0.99f), Ember);
			_toast = _ui.Label("Toast", _hud.transform, "", 30, TextAnchor.MiddleCenter, new Vector2(0.2f, 0.8f), new Vector2(0.8f, 0.88f), Pale);
			_prompt = _ui.Label("Prompt", _hud.transform, "", 32, TextAnchor.MiddleCenter, new Vector2(0.3f, 0.17f), new Vector2(0.7f, 0.23f), Ember);
			_subtitle = _ui.Label("Subtitle", _hud.transform, "", 36, TextAnchor.MiddleCenter, new Vector2(0.1f, 0.06f), new Vector2(0.9f, 0.16f), Pale, wrap: true);

			_bossBar = new GameObject("BossBar", typeof(RectTransform));
			_bossBar.transform.SetParent(_hud.transform, false);
			var bossRect = (RectTransform)_bossBar.transform;
			bossRect.anchorMin = Vector2.zero;
			bossRect.anchorMax = Vector2.one;
			bossRect.offsetMin = bossRect.offsetMax = Vector2.zero;
			_bossName = _ui.Label("Name", _bossBar.transform, "THE GUARDIAN", 30, TextAnchor.MiddleCenter, new Vector2(0.25f, 0.84f), new Vector2(0.75f, 0.89f), Pale);
			_bossFill = UiBuilder.Bar("Boss", _bossBar.transform, new Vector2(0.25f, 0.81f), new Vector2(0.75f, 0.835f), new Color(0.7f, 0.1f, 0.1f));
		}

		void BuildDialogue(Transform root)
		{
			_dialogue = UiBuilder.Box("Dialogue", root, new Vector2(0.15f, 0.04f), new Vector2(0.85f, 0.26f), Panel);
			_speaker = _ui.Label("Speaker", _dialogue.transform, "", 30, TextAnchor.MiddleLeft, new Vector2(0.03f, 0.72f), new Vector2(0.97f, 0.96f), Ember);
			_line = _ui.Label("Line", _dialogue.transform, "", 32, TextAnchor.UpperLeft, new Vector2(0.03f, 0.2f), new Vector2(0.97f, 0.72f), Pale, wrap: true);
			var dialogueButton = _dialogue.AddComponent<Button>();
			dialogueButton.targetGraphic = _dialogue.GetComponent<Image>();
			dialogueButton.onClick.AddListener(() => Game.AdvanceDialogue());
			_ui.Label("Hint", _dialogue.transform, LegendsGame.TouchUi ? "Tap to continue" : "E to continue", 22, TextAnchor.MiddleRight, new Vector2(0.5f, 0.02f), new Vector2(0.97f, 0.2f), new Color(1f, 1f, 1f, 0.6f));
		}

		void BuildReading(Transform root)
		{
			_reading = UiBuilder.Box("Reading", root, new Vector2(0.25f, 0.2f), new Vector2(0.75f, 0.8f), Panel);
			_noteTitle = _ui.Label("Title", _reading.transform, "", 38, TextAnchor.MiddleCenter, new Vector2(0.03f, 0.82f), new Vector2(0.97f, 0.97f), Ember);
			_noteText = _ui.Label("Text", _reading.transform, "", 32, TextAnchor.UpperLeft, new Vector2(0.06f, 0.14f), new Vector2(0.94f, 0.8f), Pale, wrap: true);
			var readingButton = _reading.AddComponent<Button>();
			readingButton.targetGraphic = _reading.GetComponent<Image>();
			readingButton.onClick.AddListener(() => Game.CloseReading());
			_ui.Label("Hint", _reading.transform, LegendsGame.TouchUi ? "Tap to close" : "E to close", 22, TextAnchor.MiddleRight, new Vector2(0.5f, 0.02f), new Vector2(0.97f, 0.12f), new Color(1f, 1f, 1f, 0.6f));
		}

		void BuildInventory(Transform root)
		{
			_inventory = UiBuilder.Box("Inventory", root, new Vector2(0.15f, 0.12f), new Vector2(0.85f, 0.88f), Panel);
			_ui.Label("Title", _inventory.transform, "JOURNAL", 40, TextAnchor.MiddleCenter, new Vector2(0f, 0.9f), new Vector2(1f, 0.98f), Ember);
			_inventoryText = _ui.Label("Text", _inventory.transform, "", 26, TextAnchor.UpperLeft, new Vector2(0.04f, 0.1f), new Vector2(0.96f, 0.9f), Pale, wrap: true);
			_ui.Label("Hint", _inventory.transform, LegendsGame.TouchUi ? "Journal tab to close" : "I to close", 22, TextAnchor.MiddleRight, new Vector2(0.5f, 0.02f), new Vector2(0.97f, 0.09f), new Color(1f, 1f, 1f, 0.6f));
		}

		void BuildMenu(Transform root)
		{
			_menu = UiBuilder.Box("Menu", root, new Vector2(0.2f, 0.06f), new Vector2(0.8f, 0.94f), Panel);
			_ui.Label("Sub", _menu.transform, "The Abandoned Village", 54, TextAnchor.MiddleCenter, new Vector2(0f, 0.82f), new Vector2(1f, 0.96f), Ember);
			_ui.Label("Tag", _menu.transform, "Something is wrong in a village on the border. You were sent to find out what.", 28, TextAnchor.MiddleCenter, new Vector2(0.06f, 0.62f), new Vector2(0.94f, 0.8f), Pale, wrap: true);
			_continueButton = _ui.Button("Continue", _menu.transform, new Vector2(0.2f, 0.46f), new Vector2(0.8f, 0.59f), () => Game.Begin(false)).gameObject;
			_newButton = _ui.Button("New game", _menu.transform, new Vector2(0.2f, 0.31f), new Vector2(0.8f, 0.44f), () => Game.Begin(true)).gameObject;
			_menuMainButton = _ui.Button("Main menu", _menu.transform, new Vector2(0.2f, 0.16f), new Vector2(0.8f, 0.29f), () => Game.BackToMenu()).gameObject;
			_menuWait = _ui.Label("Wait", _menu.transform, "Waiting for the host to begin the story...", 34, TextAnchor.MiddleCenter, new Vector2(0.06f, 0.3f), new Vector2(0.94f, 0.55f), Ember, wrap: true);
			_menuWait.gameObject.SetActive(false);
			var hint = LegendsGame.TouchUi
				? "Left stick walk (push far to run)  -  Attack, Block, Roll, Use and Potion on the right  -  Journal tab for notes"
				: "WASD move  -  Shift run  -  E interact  -  LMB attack  -  RMB block  -  Space roll  -  H potion  -  I inventory  -  Esc pause";
			_menuHint = _ui.Label("Hint", _menu.transform, hint, 22, TextAnchor.MiddleCenter, new Vector2(0.03f, 0.02f), new Vector2(0.97f, 0.14f), new Color(1f, 1f, 1f, 0.7f), wrap: true);
		}

		void BuildPause(Transform root)
		{
			_pause = UiBuilder.Box("Pause", root, new Vector2(0.35f, 0.3f), new Vector2(0.65f, 0.7f), Panel);
			_ui.Label("Title", _pause.transform, "PAUSED", 52, TextAnchor.MiddleCenter, new Vector2(0f, 0.7f), new Vector2(1f, 0.96f), Ember);
			_ui.Button("Resume", _pause.transform, new Vector2(0.15f, 0.38f), new Vector2(0.85f, 0.62f), () => Game.SetPaused(false));
			_ui.Button("Main menu", _pause.transform, new Vector2(0.15f, 0.1f), new Vector2(0.85f, 0.34f), () => Game.BackToMenu());
		}

void BuildDead(Transform root)
		{
			_dead = UiBuilder.Box("Dead", root, new Vector2(0.3f, 0.3f), new Vector2(0.7f, 0.7f), Panel);
			_ui.Label("Title", _dead.transform, "YOU DIED", 60, TextAnchor.MiddleCenter, new Vector2(0f, 0.62f), new Vector2(1f, 0.95f), new Color(0.85f, 0.2f, 0.2f));
			_retryButton = _ui.Button("Retry from checkpoint", _dead.transform, new Vector2(0.1f, 0.36f), new Vector2(0.9f, 0.56f), () => Game.Retry()).gameObject;
			_deadWait = _ui.Label("Wait", _dead.transform, "You rise again at the last checkpoint...", 30, TextAnchor.MiddleCenter, new Vector2(0.05f, 0.4f), new Vector2(0.95f, 0.58f), Pale, wrap: true);
			_ui.Button("Main menu", _dead.transform, new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.3f), () => Game.BackToMenu());
		}

		void BuildEnd(Transform root)
		{
			_end = UiBuilder.Box("End", root, new Vector2(0.28f, 0.25f), new Vector2(0.72f, 0.75f), Panel);
			_ui.Label("Title", _end.transform, "DEMO COMPLETE", 52, TextAnchor.MiddleCenter, new Vector2(0f, 0.72f), new Vector2(1f, 0.95f), Ember);
			_ui.Label("Text", _end.transform, "The Abandoned Village\nThis was only the first of the legends of Temnorid.", 30, TextAnchor.MiddleCenter, new Vector2(0.05f, 0.4f), new Vector2(0.95f, 0.72f), Pale, wrap: true);
			_ui.Button("Main menu", _end.transform, new Vector2(0.15f, 0.12f), new Vector2(0.85f, 0.3f), () => Game.BackToMenu());
		}
	}
}
