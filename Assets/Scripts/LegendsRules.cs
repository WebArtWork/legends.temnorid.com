using System;
using System.Collections.Generic;
using ITKamianets.Engine.Inventory;
using ITKamianets.Engine.Save;

namespace Temnorid.Legends
{
	/// <summary>The item ids kept in the inventory and the save.</summary>
	public static class Items
	{
		public const string HouseKey = "house-key";
		public const string Potion = "healing-potion";
		public const string Torch = "torch";
		public const string Artifact = "artifact";

		public static string Name(string id)
		{
			switch (id)
			{
				case HouseKey: return "Elder's house key";
				case Potion: return "Healing potion";
				case Torch: return "Torch";
				case Artifact: return "Strange artifact";
				default: return id;
			}
		}
	}

	/// <summary>
	/// What the story remembers, as plain text flags: <c>got:key</c>, <c>dead:boss</c>, <c>open:house-door</c>,
	/// <c>done:village-gate</c>, <c>note:cart</c>. The world reads these to look the way the player left it.
	/// </summary>
	public sealed class WorldState
	{
		readonly HashSet<string> _flags = new HashSet<string>();

		public int Count => _flags.Count;
		public bool Has(string flag) => _flags.Contains(flag);
		public bool Set(string flag) => _flags.Add(flag);
		public void Clear() => _flags.Clear();
		public string[] ToArray()
		{
			var all = new string[_flags.Count];
			_flags.CopyTo(all);
			Array.Sort(all, StringComparer.Ordinal);
			return all;
		}

		public void Load(string[] flags)
		{
			_flags.Clear();
			if (flags == null) return;
			foreach (var flag in flags) if (!string.IsNullOrEmpty(flag)) _flags.Add(flag);
		}
	}

	/// <summary>A document the hero can read.</summary>
	public sealed class Note
	{
		public string Id;
		public string Title;
		public string Text;
	}

	/// <summary>The words of the story: documents, the wounded villager, the objective of each moment.</summary>
	public static class Story
	{
		public static readonly Note[] Notes =
		{
			new Note { Id = "cart", Title = "A trader's note", Text = "To whoever finds this: we turned back at the village. The people there are not themselves. The trouble is under the elder's house. Do not trust the quiet.\n\n- Orsan, trader" },
			new Note { Id = "journal-1", Title = "Elder's journal, day 3", Text = "The dogs refuse to cross the cellar floor. They howl at the stones all night. Shalen says it is only the cold." },
			new Note { Id = "journal-2", Title = "Elder's journal, day 9", Text = "Half the village dreams of the same stone room. They wake with soil under their nails. Shalen and I sealed the cellar door with the old marks." },
			new Note { Id = "journal-3", Title = "Elder's journal, day 14", Text = "The seal answers only to the order carved in my rooms. I hid the three plates where only a careful eye will look. If I fall, someone will need them." },
			new Note { Id = "carving-moon", Title = "Carved plate", Text = "\"The watcher of the night opens the seal.\"" },
			new Note { Id = "carving-crown", Title = "Carved plate", Text = "\"The fallen king's token follows.\"" },
			new Note { Id = "carving-sun", Title = "Carved plate", Text = "\"And the day ends it: last, the sun.\"" },
		};

		public static Note Note(string id)
		{
			foreach (var note in Notes) if (note.Id == id) return note;
			return null;
		}

		public static readonly string[] VillagerLines =
		{
			"You... you are from the city? Thank the gods.",
			"They stopped being people three weeks ago. Something came up from beneath the elder's house.",
			"Do not go underground. That is where they come from.",
			"Take this. I will not be needing it.",
		};

		/// <summary>What the hero is trying to do right now, read from the flags.</summary>
		public static string Objective(WorldState s)
		{
			if (s.Has("got:artifact")) return "Leave the dungeon.";
			if (s.Has("dead:boss")) return "Take the artifact from the altar.";
			if (s.Has("done:altar")) return "Survive.";
			if (s.Has("done:ritual-room")) return "Examine the altar.";
			if (s.Has("done:descended")) return "Explore the dungeon.";
			if (s.Has("puzzle-solved")) return "Descend into the cellar.";
			if (s.Has("open:house-door")) return "Search the house and open the cellar door.";
			if (s.Has("dead:ambush")) return "Find the key to the elder's house.";
			if (s.Has("talked:villager")) return "Find the elder's house, north along the main street.";
			if (s.Has("done:village-gate")) return "Search the village. Someone is knocking in a house nearby.";
			return "Follow the forest road to the village.";
		}
	}

	/// <summary>The three-symbol lock of the cellar door.</summary>
	public sealed class SymbolPuzzle
	{
		public static readonly string[] Symbols = { "Sun", "Moon", "Star", "Crown" };

		/// <summary>The right order, as indexes into <see cref="Symbols"/>: Moon, Crown, Sun.</summary>
		public static readonly int[] Solution = { 1, 3, 0 };

		public int[] Current { get; } = { 2, 2, 2 };

		public int Cycle(int slot)
		{
			Current[slot] = (Current[slot] + 1) % Symbols.Length;
			return Current[slot];
		}

		public string Name(int slot) => Symbols[Current[slot]];

		public bool IsSolved
		{
			get
			{
				for (var i = 0; i < Solution.Length; i++) if (Current[i] != Solution[i]) return false;
				return true;
			}
		}
	}

	/// <summary>How hard things hit and how well the hero defends. Tuned with the balance test.</summary>
	public static class Fighting
	{
		public const float HeroHealth = 120f;
		public const float SwordDamage = 24f;
		public const float SwordRange = 2.6f;
		public const float SwordHalfAngle = 65f;
		public const float SwordCooldown = 0.6f;

		public const float RollSeconds = 0.45f;
		public const float RollInvulnerableSeconds = 0.32f;
		public const float RollCooldown = 0.8f;

		public const float PotionHeal = 50f;

		/// <summary>The share of a blocked blow that is stopped. Heavy blows are stopped only partly.</summary>
		public const float BlockStrength = 0.85f;
		public const float HeavyBlockStrength = 0.5f;

		public const float VillagerHealth = 70f;
		public const float VillagerDamage = 14f;
		public const float BossHealth = 900f;
		public const float OverheadDamage = 38f;
		public const float SweepDamage = 26f;
		public const float DashDamage = 30f;
	}

	public enum HitResult
	{
		Hit,
		Blocked,
		Dodged,
	}

	public static class Blows
	{
		/// <summary>
		/// What reaches the hero: nothing when rolling, a share when blocking toward the attacker, all of it otherwise.
		/// </summary>
		public static float Resolve(float amount, bool blocking, bool attackerInFront, bool invulnerable, float blockStrength, out HitResult result)
		{
			if (invulnerable)
			{
				result = HitResult.Dodged;
				return 0f;
			}
			if (blocking && attackerInFront)
			{
				result = HitResult.Blocked;
				return amount * (1f - blockStrength);
			}
			result = HitResult.Hit;
			return amount;
		}
	}

	/// <summary>What is kept: the last checkpoint, the story flags and the inventory.</summary>
	[Serializable]
	public sealed class LegendsSave
	{
		public int checkpoint;
		public string[] flags;
		public InventoryData items;
	}

	/// <summary>
	/// Everything about the player's progress with no Unity objects in it: the checkpoint, the story flags
	/// and the inventory (key, torch, potions, artifact).
	/// </summary>
	public sealed class LegendsSession
	{
		public const int CheckpointCount = 6;

		public int Checkpoint { get; private set; }
		public WorldState State { get; } = new WorldState();
		public ItemContainer Items { get; } = new ItemContainer(8, 9);

		public int PotionCount => Items.Count(Temnorid.Legends.Items.Potion);
		public bool Has(string itemId) => Items.Count(itemId) > 0;

		/// <summary>Begins a new game: no checkpoint, no flags, an empty pack.</summary>
		public void StartNew()
		{
			Checkpoint = 0;
			State.Clear();
			Items.Clear();
		}

		public bool AddItem(string itemId, int quantity = 1) => Items.AddUpTo(itemId, quantity) == quantity;

		/// <summary>Drinks a potion. Returns false when there is none.</summary>
		public bool UsePotion() => Items.TryRemove(Temnorid.Legends.Items.Potion, 1) == InventoryStatus.Ok;

		public void SetCheckpoint(int index) => Checkpoint = Math.Max(Checkpoint, Math.Min(index, CheckpointCount - 1));

		public LegendsSave ToSave() => new LegendsSave { checkpoint = Checkpoint, flags = State.ToArray(), items = Items.ToData() };

		public void Load(LegendsSave save)
		{
			if (save == null) return;
			Checkpoint = Math.Max(0, Math.Min(save.checkpoint, CheckpointCount - 1));
			State.Load(save.flags);
			Items.Load(save.items);
		}
	}

	/// <summary>Reads and writes the <see cref="LegendsSave"/> in the game's save slot.</summary>
	public sealed class LegendsSaveStore
	{
		const string Slot = "default";
		const int PayloadVersion = 1;

		readonly SaveStore _store;

		public LegendsSaveStore(string rootDirectory = null)
		{
			_store = new SaveStore("legends", rootDirectory);
		}

		/// <summary>The saved game, or null when there is none yet or it cannot be read.</summary>
		public LegendsSave Load()
		{
			var result = _store.Load<LegendsSave>(Slot, PayloadVersion);
			return result.Ok ? result.Value : null;
		}

		public void Save(LegendsSave save) => _store.Save(Slot, save, PayloadVersion);
	}
}
