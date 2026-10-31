using System;
using System.Collections.Generic;
using ITKamianets.Engine.Combat;
using ITKamianets.Engine.Net;
using ITKamianets.Engine.Shell;
using Newtonsoft.Json;
using UnityEngine;

namespace Temnorid.Legends
{
	/// <summary>The shared story as the host knows it: what is sent to a guest so the world looks the same there.</summary>
	[Serializable]
	public sealed class LegendsStatePacket
	{
		public LegendsSave save;
		public int[] puzzle;
	}

	/// <summary>
	/// Makes the village shared between two heroes. The host is the authority: it runs the foes, the story flags, the pack, the
	/// lock and the checkpoints, and publishes them; a guest asks (use, trigger, hit, potion) and shows what comes back.
	/// Each person's hero is simulated on their own machine and published as a pose; the other hero is a <see cref="RemoteHero"/>.
	/// Cameras are not shared: each machine follows its own hero.
	/// <para>
	/// Events from a guest to the host are named <c>host.*</c> (the session does not relay those on); events from the host to
	/// everybody are <c>leg.*</c>. A hero is hurt by the host's decision but resolved by its owner, who knows whether the shield
	/// was up or a roll was under way.
	/// </para>
	/// </summary>
	public sealed class LegendsNet : MonoBehaviour
	{
		public const string HereEvent = "host.here";
		public const string UseEvent = "host.use";
		public const string TriggerEvent = "host.trigger";
		public const string HitEvent = "host.hit";
		public const string PotionEvent = "host.potion";
		public const string StateEvent = "leg.state";
		public const string BeginEvent = "leg.begin";
		public const string ToastEvent = "leg.toast";
		public const string SceneEvent = "leg.scene";
		public const string DamageEvent = "leg.dmg";

		const float PoseEvery = 1f / 15f;
		const float EntityEvery = 0.1f;
		const float UseRange = 7f;

		LegendsGame _game;
		NetSession _net;
		Foe[] _foes;
		readonly Dictionary<ulong, RemoteHero> _heroes = new Dictionary<ulong, RemoteHero>();
		readonly Dictionary<ulong, float> _lastHit = new Dictionary<ulong, float>();
		readonly Dictionary<int, float> _deadSince = new Dictionary<int, float>();
		readonly HashSet<int> _removed = new HashSet<int>();
		float _nextPose;
		float _nextEntities;
		bool _dirty;
		bool _hello;
		bool _quitting;

		public bool IsClient => _net != null && _net.IsClient;
		public bool IsHost => _net != null && _net.IsHost;
		public IReadOnlyDictionary<ulong, RemoteHero> Heroes => _heroes;

		public void Init(LegendsGame game)
		{
			_game = game;
			_net = NetSession.Instance;
			var all = FindObjectsByType<Foe>(FindObjectsInactive.Include, FindObjectsSortMode.None);
			Array.Sort(all, (a, b) => string.CompareOrdinal(a.Id, b.Id));
			_foes = all;

			if (_net.IsClient)
			{
				foreach (var foe in _foes) foe.SetRemote(true);
				_game.SaveEnabled = false; // a guest's own save is never touched by someone else's game
			}

			_net.On(HereEvent, OnHere);
			_net.On(UseEvent, OnUse);
			_net.On(TriggerEvent, OnTriggerRequest);
			_net.On(HitEvent, OnHit);
			_net.On(PotionEvent, OnPotion);
			_net.On(StateEvent, OnState);
			_net.On(BeginEvent, OnBegin);
			_net.On(ToastEvent, OnToast);
			_net.On(SceneEvent, OnScene);
			_net.On(DamageEvent, OnDamage);
			_net.Disconnected += OnDisconnected;
		}

		void OnDestroy()
		{
			if (_net == null) return;
			_net.Off(HereEvent, OnHere);
			_net.Off(UseEvent, OnUse);
			_net.Off(TriggerEvent, OnTriggerRequest);
			_net.Off(HitEvent, OnHit);
			_net.Off(PotionEvent, OnPotion);
			_net.Off(StateEvent, OnState);
			_net.Off(BeginEvent, OnBegin);
			_net.Off(ToastEvent, OnToast);
			_net.Off(SceneEvent, OnScene);
			_net.Off(DamageEvent, OnDamage);
			_net.Disconnected -= OnDisconnected;
			foreach (var hero in _heroes.Values)
				if (hero != null) Destroy(hero.gameObject);
			_heroes.Clear();
		}

		void OnDisconnected(string reason)
		{
			// The host closed the game or the connection dropped: nothing left to play together.
			if (_quitting) return;
			_quitting = true;
			ShellFlow.QuitToMenu();
		}

		// ---- what the game calls ----

		/// <summary>Host: the shared story changed; tell the guest at the end of the frame.</summary>
		public void MarkDirty() => _dirty = true;

		/// <summary>Host: sends the shared story now (before something that depends on it).</summary>
		public void Flush()
		{
			if (!_net.IsHost) return;
			_dirty = false;
			_net.Send(StateEvent, JsonConvert.SerializeObject(Snapshot()));
		}

		public void AnnounceBegin() => _net.Send(BeginEvent, "");

		public void AnnounceScene(string scene) => _net.Send(SceneEvent, scene);

		/// <summary>Host: a line for everybody.</summary>
		public void ToastAll(string text) => _net.Send(ToastEvent, text);

		/// <summary>Host: a line for one player.</summary>
		public void ToastTo(ulong player, string text) => _net.SendTo(player, ToastEvent, text);

		public void SendUse(string payload) => _net.Send(UseEvent, payload);
		public void SendTrigger(string id) => _net.Send(TriggerEvent, id);
		public void SendPotion() => _net.Send(PotionEvent, "");

		/// <summary>Guest: a sword swing in a direction. The host checks it against where this hero stands.</summary>
		public void SendHit(Vector3 direction)
		{
			var yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
			_net.Send(HitEvent, yaw.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
		}

		/// <summary>Host: a foe's blow on another player's hero, sent to its owner to resolve.</summary>
		public void SendDamage(ulong owner, float amount, Vector3 from, float blockStrength)
		{
			if (!_net.IsHost) return;
			var c = System.Globalization.CultureInfo.InvariantCulture;
			_net.SendTo(owner, DamageEvent, string.Join("|", amount.ToString("R", c), from.x.ToString("R", c), from.y.ToString("R", c), from.z.ToString("R", c), blockStrength.ToString("R", c)));
		}

		public LegendsStatePacket Snapshot()
		{
			return new LegendsStatePacket { save = _game.Session.ToSave(), puzzle = (int[])_game.Puzzle.Current.Clone() };
		}

		// ---- events: guest to host ----

		void OnHere(ulong sender, string payload)
		{
			if (!_net.IsHost) return;
			SendStateTo(sender);
			if (_game.Begun) _net.SendTo(sender, BeginEvent, "");
		}

		void SendStateTo(ulong player) => _net.SendTo(player, StateEvent, JsonConvert.SerializeObject(Snapshot()));

		bool TryPose(ulong sender, out NetPose pose) => _net.TryGetPose(sender, out pose);

		void OnUse(ulong sender, string payload)
		{
			if (!_net.IsHost || string.IsNullOrEmpty(payload)) return;
			var parts = payload.Split('|');
			var done = parts.Length > 1 && parts[1] == "done";
			Interactable found = null;
			foreach (var item in Interactable.All)
				if (item.Id == parts[0]) { found = item; break; }
			if (found == null) return;
			if (!TryPose(sender, out var pose) || (pose.Position - found.transform.position).magnitude > UseRange) return;
			_game.HostUse(found, done, sender);
		}

		void OnTriggerRequest(ulong sender, string payload)
		{
			if (!_net.IsHost || string.IsNullOrEmpty(payload)) return;
			_game.HostTrigger(payload);
		}

		void OnPotion(ulong sender, string payload)
		{
			if (_net.IsHost) _game.HostPotion();
		}

		void OnHit(ulong sender, string payload)
		{
			if (!_net.IsHost) return;
			if (!float.TryParse(payload, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var yaw)) return;
			if (!TryPose(sender, out var pose) || (pose.State & RemoteHero.Down) != 0) return;
			var now = Time.realtimeSinceStartup;
			if (_lastHit.TryGetValue(sender, out var last) && now - last < Fighting.SwordCooldown * 0.8f) return;
			_lastHit[sender] = now;
			var direction = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
			var origin = pose.Position;
			CombatQueries.DamageInCone(origin, direction, Fighting.SwordRange + 0.4f, Fighting.SwordHalfAngle + 10f, _game.Hero.Health.Team, new DamageInfo(Fighting.SwordDamage, null, origin, "sword"));
		}

		// ---- events: host to guest ----

		void OnState(ulong sender, string payload)
		{
			if (!_net.IsClient) return;
			LegendsStatePacket packet;
			try { packet = JsonConvert.DeserializeObject<LegendsStatePacket>(payload); }
			catch (Exception) { return; }
			if (packet != null) _game.ApplyState(packet);
		}

		void OnBegin(ulong sender, string payload)
		{
			if (_net.IsClient) _game.BeginRemote();
		}

		void OnToast(ulong sender, string payload)
		{
			if (_net.IsClient) _game.ShowToast(payload);
		}

		void OnScene(ulong sender, string payload)
		{
			if (_net.IsClient) _game.PlayAnnounced(payload);
		}

		void OnDamage(ulong sender, string payload)
		{
			if (!_net.IsClient || _game.Hero == null) return;
			var p = payload.Split('|');
			if (p.Length < 5) return;
			var c = System.Globalization.CultureInfo.InvariantCulture;
			if (!float.TryParse(p[0], System.Globalization.NumberStyles.Float, c, out var amount)) return;
			float.TryParse(p[1], System.Globalization.NumberStyles.Float, c, out var x);
			float.TryParse(p[2], System.Globalization.NumberStyles.Float, c, out var y);
			float.TryParse(p[3], System.Globalization.NumberStyles.Float, c, out var z);
			float.TryParse(p[4], System.Globalization.NumberStyles.Float, c, out var strength);
			if (_game.Hero.Health.IsAlive) _game.Hero.TakeHit(amount, new Vector3(x, y, z), strength);
		}

		// ---- every frame ----

		void Update()
		{
			if (_net == null || !_net.IsOnline) return;

			if (!_hello && _net.IsClient)
			{
				_hello = true;
				_net.Send(HereEvent, "");
			}

			ShowOthers();
			if (_net.IsClient) ShowFoes();

			if (_dirty && _net.IsHost)
			{
				_dirty = false;
				_net.Send(StateEvent, JsonConvert.SerializeObject(Snapshot()));
			}

			if (Time.unscaledTime >= _nextPose)
			{
				_nextPose = Time.unscaledTime + PoseEvery;
				_net.PublishPose(MyPose());
			}
			if (_net.IsHost && Time.unscaledTime >= _nextEntities)
			{
				_nextEntities = Time.unscaledTime + EntityEvery;
				PublishFoes();
			}
		}

		NetPose MyPose()
		{
			var hero = _game.Hero;
			var state = 0;
			if (hero.Blocking) state |= RemoteHero.Blocking;
			if (hero.Swinging) state |= RemoteHero.Swinging;
			if (hero.Rolling) state |= RemoteHero.Rolling;
			if (!hero.Health.IsAlive) state |= RemoteHero.Down;
			return new NetPose
			{
				Position = hero.transform.position,
				Yaw = hero.transform.eulerAngles.y,
				Speed = hero.GroundSpeed,
				State = state,
				A = hero.Health.Fraction,
			};
		}

		void ShowOthers()
		{
			var seen = new HashSet<ulong>();
			foreach (var player in _net.Players)
			{
				if (player.Id == _net.LocalId) continue;
				seen.Add(player.Id);
				if (!_heroes.TryGetValue(player.Id, out var hero) || hero == null)
				{
					hero = RemoteHero.Create(player.Id, player.Name, ShellConfig.Current.ColourOf(player.Colour), this);
					_heroes[player.Id] = hero;
				}
				hero.SetName(player.Name);
				if (_net.TryGetPose(player.Id, out var pose)) hero.Apply(pose);
			}
			List<ulong> gone = null;
			foreach (var pair in _heroes)
				if (!seen.Contains(pair.Key)) (gone ?? (gone = new List<ulong>())).Add(pair.Key);
			if (gone == null) return;
			foreach (var id in gone)
			{
				if (_heroes[id] != null) Destroy(_heroes[id].gameObject);
				_heroes.Remove(id);
			}
		}

		/// <summary>The id a foe has on every machine: its place in the list sorted by name.</summary>
		public int IdOf(Foe foe) => Array.IndexOf(_foes, foe) + 1;

		void PublishFoes()
		{
			var list = new List<NetEntity>();
			for (var i = 0; i < _foes.Length; i++)
			{
				var foe = _foes[i];
				var id = i + 1;
				if (!foe.gameObject.activeInHierarchy || _removed.Contains(id)) continue;
				if (foe.State == FoeState.Dead)
				{
					if (!_deadSince.ContainsKey(id)) _deadSince[id] = Time.unscaledTime;
					else if (Time.unscaledTime - _deadSince[id] > 3f)
					{
						_removed.Add(id);
						_net.RemoveEntity(id);
						continue;
					}
				}
				var t = foe.transform;
				list.Add(new NetEntity { Id = id, Kind = (int)foe.Kind, Position = t.position, Yaw = t.eulerAngles.y, Value = foe.Health.Fraction, State = foe.PackedState });
			}
			_net.PublishEntities(list);
		}

		void ShowFoes()
		{
			for (var i = 0; i < _foes.Length; i++)
			{
				var foe = _foes[i];
				if (!_net.Entities.TryGetValue(i + 1, out var entity)) continue;
				if (!foe.gameObject.activeSelf) foe.gameObject.SetActive(true);
				var body = foe.GetComponent<RemoteBody>() ?? foe.gameObject.AddComponent<RemoteBody>();
				body.Apply(entity);
				foe.ShowRemote(entity.State, entity.Value);
			}
		}
	}
}
