using System.Collections.Generic;
using ITKamianets.Engine.Combat;
using ITKamianets.Engine.Feedback;
using ITKamianets.Engine.Graybox;
using ITKamianets.Engine.Graybox.EditorTools;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Temnorid.Legends.EditorTools
{
	/// <summary>
	/// Builds the whole demo from Unity primitives: the forest road, the village with its square, the wounded
	/// villager's hut and the street, the elder's house with its three-symbol lock, and the dungeon with its
	/// altar room; plus the hero, the foes, the camera zones, the story triggers and the screens. Run it from
	/// the menu (Temnorid > Legends > Build Scene) or the command line
	/// (<c>-executeMethod Temnorid.Legends.EditorTools.LegendsSceneBuilder.Build</c>); it rebuilds
	/// <c>Assets/Scenes/Main.unity</c> from scratch each time, so do not edit that scene by hand.
	/// The NavMesh is baked when the game starts, from the floor and wall colliders.
	/// </summary>
	public static class LegendsSceneBuilder
	{
		const string ScenePath = "Assets/Scenes/Main.unity";
		const string MaterialDir = "Assets/Materials";
		const float Wall = 3.2f;

		static Material _grass, _dirt, _wood, _woodDark, _stone, _stoneDark, _plaster, _roof, _cloth, _bone, _dark, _gold, _ember, _red, _paper, _skin, _steel;

		[MenuItem("Temnorid/Legends/Build Scene")]
		public static void Build()
		{
			PlayerSettings.productName = "Temnorid Legends";
			PlayerSettings.companyName = "Temnorid";
			PlayerSettings.bundleVersion = "0.1.0";
			PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.itkamianets.legends");
			PlayerSettings.runInBackground = true;
			PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;

			var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
			RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
			RenderSettings.ambientLight = new Color(0.42f, 0.44f, 0.55f);
			MakeMaterials();

			var world = new GameObject("World");
			var surface = world.AddComponent<NavMeshSurface>();
			surface.collectObjects = CollectObjects.Children;
			surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
			var doors = new GameObject("Doors").transform;
			var props = new GameObject("Props").transform;
			var zones = new GameObject("CameraZones").transform;
			var triggers = new GameObject("StoryTriggers").transform;

			BuildGround(world.transform, props);
			BuildRoad(world.transform, props, zones, triggers);
			BuildVillage(world.transform, props, zones, triggers, out var villagerHut);
			BuildStreet(world.transform, props, zones, triggers, out var ambusher);
			BuildElderHouse(world.transform, doors, props, zones);
			BuildDungeon(world.transform, props, zones, triggers, out var ritualBody, out var boss);
			BuildBoundaries();

			var checkpoints = BuildCheckpoints();
			var shotRoot = BuildShots(out var figure);
			var hero = BuildHero();
			var rig = BuildCameraAndLight(hero, out var sun);

			var gameObject = new GameObject("Game");
			var game = gameObject.AddComponent<LegendsGame>();
			game.Hero = hero;
			game.Rig = rig;
			game.Surface = surface;
			game.Ambusher = ambusher;
			game.Boss = boss;
			game.Checkpoints = checkpoints;
			game.ShotRoot = shotRoot;
			game.WindowFigure = figure;
			game.RitualBody = ritualBody;
			game.Sun = sun;
			hero.Game = game;
			hero.Rig = rig;

			var hud = gameObject.AddComponent<LegendsHud>();
			hud.Game = game;

			ambusher.gameObject.SetActive(false); // woken by the game once the NavMesh exists
			boss.gameObject.SetActive(false); // rises from the altar

			EditorSceneManager.SaveScene(scene, ScenePath);
			EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
			AssetDatabase.SaveAssets();
			Debug.Log("[Legends] Scene built: " + ScenePath + " (" + villagerHut + ")");
		}

		static void MakeMaterials()
		{
			_grass = Mat("Grass", new Color(0.2f, 0.3f, 0.18f));
			_dirt = Mat("Dirt", new Color(0.32f, 0.26f, 0.19f));
			_wood = Mat("Wood", new Color(0.4f, 0.27f, 0.15f));
			_woodDark = Mat("WoodDark", new Color(0.22f, 0.15f, 0.1f));
			_stone = Mat("Stone", new Color(0.42f, 0.41f, 0.44f));
			_stoneDark = Mat("StoneDark", new Color(0.25f, 0.24f, 0.27f));
			_plaster = Mat("Plaster", new Color(0.55f, 0.5f, 0.42f));
			_roof = Mat("Roof", new Color(0.3f, 0.18f, 0.15f));
			_cloth = Mat("Cloth", new Color(0.35f, 0.42f, 0.55f));
			_bone = Mat("Bone", new Color(0.78f, 0.75f, 0.65f));
			_dark = Mat("Dark", new Color(0.08f, 0.07f, 0.09f));
			_gold = Mat("Gold", new Color(0.95f, 0.75f, 0.25f));
			_ember = Mat("Ember", new Color(1f, 0.6f, 0.2f));
			_red = Mat("Red", new Color(0.6f, 0.1f, 0.1f));
			_paper = Mat("Paper", new Color(0.88f, 0.84f, 0.7f));
			_skin = Mat("Skin", new Color(0.8f, 0.66f, 0.55f));
			_steel = Mat("Steel", new Color(0.72f, 0.74f, 0.8f));
		}

		// ---- ground, road, gate ----

		static void BuildGround(Transform world, Transform props)
		{
			Box(world, "Ground", new Vector3(0f, -0.25f, 10f), new Vector3(140f, 0.5f, 220f), _grass);
			Prim(PrimitiveType.Cube, "RoadStrip", props, new Vector3(0f, 0.03f, -40f), new Vector3(4.5f, 0.06f, 70f), _dirt);
			Prim(PrimitiveType.Cube, "Paving", props, new Vector3(0f, 0.03f, 18f), new Vector3(26f, 0.06f, 24f), _dirt);
			Prim(PrimitiveType.Cube, "StreetStrip", props, new Vector3(0f, 0.03f, 52f), new Vector3(5f, 0.06f, 42f), _dirt);
		}

		static void BuildRoad(Transform world, Transform props, Transform zones, Transform triggers)
		{
			var random = new System.Random(11);
			for (var z = -76f; z < -8f; z += 4.2f)
				foreach (var side in new[] { -1f, 1f })
					Tree(props, new Vector3(side * (6.5f + (float)random.NextDouble() * 4f), 0f, z + (float)random.NextDouble() * 2f), 3.8f + (float)random.NextDouble() * 2f);

			// The village gate.
			var gate = new GameObject("VillageGate").transform;
			gate.SetParent(props, false);
			Prim(PrimitiveType.Cube, "PostL", gate, new Vector3(-3.6f, 1.8f, -6f), new Vector3(0.5f, 3.6f, 0.5f), _woodDark);
			Prim(PrimitiveType.Cube, "PostR", gate, new Vector3(3.6f, 1.8f, -6f), new Vector3(0.5f, 3.6f, 0.5f), _woodDark);
			Prim(PrimitiveType.Cube, "Beam", gate, new Vector3(0f, 3.5f, -6f), new Vector3(8f, 0.45f, 0.5f), _woodDark);

			// The abandoned cart with the trader's note.
			var cart = new GameObject("AbandonedCart").transform;
			cart.SetParent(props, false);
			cart.position = new Vector3(4.2f, 0f, -38f);
			cart.rotation = Quaternion.Euler(0f, 35f, 0f);
			cart.gameObject.AddComponent<AssetIdTag>().Id = "props/abandoned-cart";
			Prim(PrimitiveType.Cube, "Bed", cart, new Vector3(0f, 0.9f, 0f), new Vector3(2.2f, 0.5f, 3.6f), _wood);
			Prim(PrimitiveType.Cube, "SideL", cart, new Vector3(-1.05f, 1.3f, 0f), new Vector3(0.1f, 0.7f, 3.6f), _wood);
			Prim(PrimitiveType.Cube, "SideR", cart, new Vector3(1.05f, 1.3f, 0f), new Vector3(0.1f, 0.7f, 3.6f), _wood);
			var wheel = Prim(PrimitiveType.Cylinder, "WheelL", cart, new Vector3(-1.2f, 0.6f, 0.6f), new Vector3(1.2f, 0.1f, 1.2f), _woodDark);
			wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
			wheel = Prim(PrimitiveType.Cylinder, "WheelR", cart, new Vector3(1.2f, 0.3f, -0.4f), new Vector3(1.2f, 0.1f, 1.2f), _woodDark);
			wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 70f); // one wheel broken off and leaning
			Prim(PrimitiveType.Cube, "Crate", props, new Vector3(1.6f, 0.3f, -35f), new Vector3(0.7f, 0.6f, 0.7f), _wood).transform.rotation = Quaternion.Euler(0f, 20f, 0f);
			Prim(PrimitiveType.Cube, "Sack", props, new Vector3(2.4f, 0.2f, -36.2f), new Vector3(0.6f, 0.4f, 0.5f), _plaster);
			Prim(PrimitiveType.Cube, "Crate2", props, new Vector3(3.6f, 0.25f, -34.4f), new Vector3(0.6f, 0.5f, 0.6f), _wood).transform.rotation = Quaternion.Euler(0f, 70f, 12f);
			var note = Interact(props, "cart", InteractKind.Note, "Read the trader's note", new Vector3(3.0f, 0f, -37.5f));
			Prim(PrimitiveType.Cube, "Paper", note.transform, new Vector3(0f, 0.15f, 0f), new Vector3(0.3f, 0.02f, 0.4f), _paper);

			// Fixed camera shots along the road.
			Zone(zones, "RoadHigh", new Vector3(0f, 2f, -58f), new Vector3(18f, 6f, 36f), new Vector3(-5f, 11f, -76f), 50f, 0);
			Zone(zones, "CartLow", new Vector3(0f, 2f, -37f), new Vector3(18f, 6f, 14f), new Vector3(-1.5f, 1.3f, -46f), 52f, 1);
			Zone(zones, "RoadEnd", new Vector3(0f, 2f, -18f), new Vector3(18f, 6f, 28f), new Vector3(-8f, 9f, -34f), 50f, 0);
			Trigger(triggers, "village-gate", new Vector3(0f, 1.5f, -4f), new Vector3(10f, 3f, 2f));
		}

		// ---- the square, the wounded villager's hut ----

		static void BuildVillage(Transform world, Transform props, Transform zones, Transform triggers, out string summary)
		{
			// Houses around the square.
			House(world, "HouseE1", new Vector3(20f, 0f, 12f), new Vector3(8f, 3.4f, 8f));
			House(world, "HouseE2", new Vector3(20f, 0f, 26f), new Vector3(8f, 3.4f, 8f));
			House(world, "HouseW2", new Vector3(-20f, 0f, 31f), new Vector3(8f, 3.4f, 8f));
			House(world, "HouseS1", new Vector3(-13f, 0f, 4f), new Vector3(7f, 3.4f, 6f));
			House(world, "HouseS2", new Vector3(13f, 0f, 3f), new Vector3(7f, 3.4f, 6f));

			// The hut of the wounded villager: walls, a doorway on the east, a window on the north, a roof.
			var hut = new GameObject("Hut").transform;
			hut.SetParent(world, false);
			Gapped(hut, "South", true, 14f, -24f, -16f, Wall, _plaster);
			Gapped(hut, "West", false, -24f, 14f, 22f, Wall, _plaster);
			Gapped(hut, "East", false, -16f, 14f, 22f, Wall, _plaster, new Vector2(16.5f, 19.5f));
			Gapped(hut, "North", true, 22f, -24f, -16f, Wall, _plaster, new Vector2(-21f, -19f));
			Box(hut, "Sill", new Vector3(-20f, 0.5f, 22f), new Vector3(2f, 1f, 0.5f), _plaster);
			Box(hut, "Lintel", new Vector3(-20f, 2.7f, 22f), new Vector3(2f, 1f, 0.5f), _plaster);
			Roof(hut, -24f, -16f, 14f, 22f, Wall);
			hut.gameObject.AddComponent<AssetIdTag>().Id = "buildings/house";
			Prim(PrimitiveType.Cube, "Bed", props, new Vector3(-22.4f, 0.3f, 19.6f), new Vector3(1.2f, 0.5f, 2.4f), _wood);
			Prim(PrimitiveType.Cube, "Table", props, new Vector3(-18.6f, 0.5f, 15.4f), new Vector3(1.4f, 0.1f, 0.8f), _woodDark);
			Zone(zones, "HutInside", new Vector3(-20f, 2f, 18f), new Vector3(7f, 5f, 7f), new Vector3(-23.3f, 2.7f, 14.7f), 62f, 3);

			var villager = Interact(props, "villager", InteractKind.Talk, "Talk to the wounded villager", new Vector3(-21.4f, 0f, 17.6f));
			villager.gameObject.AddComponent<AssetIdTag>().Id = "characters/villager-wounded";
			Prim(PrimitiveType.Capsule, "Body", villager.transform, new Vector3(0f, 0.7f, 0f), new Vector3(0.7f, 0.7f, 0.7f), _cloth).transform.localRotation = Quaternion.Euler(0f, 0f, 70f);
			Prim(PrimitiveType.Sphere, "Head", villager.transform, new Vector3(-0.75f, 0.85f, 0f), new Vector3(0.38f, 0.38f, 0.38f), _skin);
			Prim(PrimitiveType.Sphere, "Wound", villager.transform, new Vector3(0.15f, 1.0f, 0.3f), new Vector3(0.2f, 0.2f, 0.12f), _red);

			// The square: the cold fire, stalls, crates; camera from a roof.
			var fire = new GameObject("Campfire").transform;
			fire.SetParent(props, false);
			fire.position = new Vector3(0f, 0f, 18f);
			fire.gameObject.AddComponent<AssetIdTag>().Id = "props/campfire";
			for (var i = 0; i < 8; i++)
			{
				var angle = i * Mathf.PI * 2f / 8f;
				Prim(PrimitiveType.Sphere, "Stone" + i, fire, new Vector3(Mathf.Cos(angle) * 1f, 0.2f, Mathf.Sin(angle) * 1f), new Vector3(0.4f, 0.35f, 0.4f), _stoneDark);
			}
			Prim(PrimitiveType.Cylinder, "Ash", fire, new Vector3(0f, 0.05f, 0f), new Vector3(1.4f, 0.04f, 1.4f), _dark);
			Prim(PrimitiveType.Cylinder, "Log", fire, new Vector3(0.1f, 0.25f, 0f), new Vector3(0.18f, 0.6f, 0.18f), _woodDark).transform.localRotation = Quaternion.Euler(0f, 0f, 80f);
			Stall(props, new Vector3(7f, 0f, 12f), 20f);
			Stall(props, new Vector3(-7f, 0f, 11f), -15f);
			Stall(props, new Vector3(9f, 0f, 22f), 100f);
			Prim(PrimitiveType.Cube, "Crate", props, new Vector3(-9f, 0.4f, 24f), new Vector3(0.9f, 0.8f, 0.9f), _wood);
			Prim(PrimitiveType.Cube, "Crate2", props, new Vector3(-8.2f, 0.3f, 24.9f), new Vector3(0.7f, 0.6f, 0.7f), _wood).transform.rotation = Quaternion.Euler(0f, 30f, 0f);
			Zone(zones, "SquareRoof", new Vector3(0f, 2f, 18f), new Vector3(30f, 6f, 22f), new Vector3(22.5f, 5.6f, 10f), 55f, 0);
			Trigger(triggers, "square-listen", new Vector3(0f, 1.5f, 10f), new Vector3(16f, 3f, 3f));

			var random = new System.Random(5);
			for (var i = 0; i < 16; i++)
				Tree(props, new Vector3(-36f + (float)random.NextDouble() * 6f, 0f, -4f + i * 7f), 4f + (float)random.NextDouble() * 2f);
			for (var i = 0; i < 16; i++)
				Tree(props, new Vector3(35f + (float)random.NextDouble() * 6f, 0f, -4f + i * 7f), 4f + (float)random.NextDouble() * 2f);
			summary = "village";
		}

		// ---- the street, the ambush, the storeroom ----

		static void BuildStreet(Transform world, Transform props, Transform zones, Transform triggers, out Foe ambusher)
		{
			// Houses along both sides of the street (z 32 to 58), solid blocks.
			House(world, "StreetW1", new Vector3(-9f, 0f, 38f), new Vector3(8f, 3.4f, 11f));
			House(world, "StreetW2", new Vector3(-9f, 0f, 51f), new Vector3(8f, 3.4f, 11f));
			House(world, "StreetE1", new Vector3(9f, 0f, 38f), new Vector3(8f, 3.4f, 11f));
			House(world, "StreetE2", new Vector3(9f, 0f, 51f), new Vector3(8f, 3.4f, 11f));
			House(world, "YardE", new Vector3(10f, 0f, 65f), new Vector3(7f, 3.4f, 6f));

			// The storeroom: a shed with its door open to the street.
			var shed = new GameObject("Storeroom").transform;
			shed.SetParent(world, false);
			shed.gameObject.AddComponent<AssetIdTag>().Id = "buildings/storeroom";
			Gapped(shed, "South", true, 62f, -13f, -6f, Wall, _wood);
			Gapped(shed, "North", true, 68f, -13f, -6f, Wall, _wood);
			Gapped(shed, "West", false, -13f, 62f, 68f, Wall, _wood);
			Gapped(shed, "East", false, -6f, 62f, 68f, Wall, _wood, new Vector2(64f, 66f));
			Roof(shed, -13f, -6f, 62f, 68f, Wall);
			Prim(PrimitiveType.Cube, "Crate", props, new Vector3(-11.8f, 0.4f, 63.2f), new Vector3(0.9f, 0.8f, 0.9f), _wood);
			Prim(PrimitiveType.Cube, "Barrel", props, new Vector3(-11.6f, 0.5f, 66.8f), new Vector3(0.7f, 1f, 0.7f), _woodDark);
			Prim(PrimitiveType.Cube, "Shelf", props, new Vector3(-12.6f, 1f, 65f), new Vector3(0.4f, 0.1f, 3f), _woodDark);
			Zone(zones, "StorerRoom", new Vector3(-9.5f, 2f, 65f), new Vector3(7f, 5f, 6f), new Vector3(-12.4f, 2.8f, 62.6f), 62f, 3);

			var key = Interact(props, "key", InteractKind.Pickup, "Take the key", new Vector3(-9.4f, 0f, 66.4f));
			key.ItemId = Items.HouseKey;
			key.gameObject.AddComponent<AssetIdTag>().Id = "loot/key";
			Prim(PrimitiveType.Cube, "Shaft", key.transform, new Vector3(0f, 0.5f, 0f), new Vector3(0.08f, 0.08f, 0.35f), _gold);
			Prim(PrimitiveType.Cylinder, "Bow", key.transform, new Vector3(0f, 0.5f, -0.22f), new Vector3(0.2f, 0.03f, 0.2f), _gold).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
			Gate(key.gameObject, null, "got:key");
			Potion(props, "potion-shed", new Vector3(-8.2f, 0f, 63.2f));

			// The ambush: a villager stands in the street with his back to the square.
			ambusher = BuildCursed("Ambusher", "ambush", props, new Vector3(0f, 0f, 44f), 0f);
			Trigger(triggers, "street-ambush", new Vector3(0f, 1.5f, 38f), new Vector3(6f, 3f, 3f));

			Zone(zones, "StreetSouth", new Vector3(0f, 2f, 40f), new Vector3(10f, 6f, 24f), new Vector3(4.5f, 7f, 26f), 52f, 1);
			Zone(zones, "StreetNorth", new Vector3(0f, 2f, 54f), new Vector3(10f, 6f, 8f), new Vector3(-5.5f, 6f, 36f), 50f, 0);
			Zone(zones, "Yard", new Vector3(0f, 2f, 64f), new Vector3(16f, 6f, 12f), new Vector3(6f, 5.5f, 56.5f), 52f, 1);
		}

		static Foe BuildCursed(string name, string id, Transform parent, Vector3 position, float yaw)
		{
			var root = new GameObject(name);
			root.transform.SetParent(parent, false);
			root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
			root.AddComponent<AssetIdTag>().Id = "monsters/cursed-villager";
			var capsule = root.AddComponent<CapsuleCollider>();
			capsule.radius = 0.45f;
			capsule.height = 1.8f;
			capsule.center = new Vector3(0f, 0.9f, 0f);
			root.AddComponent<Health>();
			var agent = root.AddComponent<NavMeshAgent>();
			agent.radius = 0.4f;
			agent.height = 1.8f;
			agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
			var foe = root.AddComponent<Foe>();
			foe.Id = id;
			foe.Configure(FoeKind.Cursed);

			var visual = new GameObject("Visual").transform;
			visual.SetParent(root.transform, false);
			foe.Visual = visual;
			var rags = Mat("Cursed", new Color(0.42f, 0.5f, 0.42f));
			Prim(PrimitiveType.Capsule, "Body", visual, new Vector3(0f, 0.9f, 0f), new Vector3(0.75f, 0.9f, 0.75f), rags);
			Prim(PrimitiveType.Sphere, "Head", visual, new Vector3(0f, 1.7f, 0.1f), new Vector3(0.42f, 0.42f, 0.42f), _bone);
			Prim(PrimitiveType.Sphere, "EyeL", visual, new Vector3(-0.1f, 1.74f, 0.3f), new Vector3(0.08f, 0.08f, 0.08f), _red);
			Prim(PrimitiveType.Sphere, "EyeR", visual, new Vector3(0.1f, 1.74f, 0.3f), new Vector3(0.08f, 0.08f, 0.08f), _red);
			Prim(PrimitiveType.Cube, "Handle", visual, new Vector3(0.5f, 1.15f, 0.45f), new Vector3(0.08f, 0.08f, 1.0f), _woodDark);
			Prim(PrimitiveType.Cube, "Blade", visual, new Vector3(0.5f, 1.15f, 0.95f), new Vector3(0.06f, 0.4f, 0.4f), _dark);
			root.AddComponent<HealthBar3D>().Height = 2.4f;
			return foe;
		}

		// ---- the elder's house ----

		static void BuildElderHouse(Transform world, Transform doors, Transform props, Transform zones)
		{
			const float h = 3.6f;
			var house = new GameObject("ElderHouse").transform;
			house.SetParent(world, false);
			house.gameObject.AddComponent<AssetIdTag>().Id = "buildings/elder-house";

			// Outer walls.
			Gapped(house, "South", true, 70f, -10f, 10f, h, _plaster, new Vector2(-1.5f, 1.5f));
			Gapped(house, "North", true, 88f, -10f, 10f, h, _plaster);
			Gapped(house, "West", false, -10f, 70f, 88f, h, _plaster);
			Gapped(house, "East", false, 10f, 70f, 88f, h, _plaster);
			// The partition between the hall and the back: doorways to the west and east rooms, and the cellar door.
			Gapped(house, "Partition", true, 78f, -10f, 10f, h, _plaster, new Vector2(-8f, -5f), new Vector2(-1.5f, 1.5f), new Vector2(5f, 8f));
			// The cellar corridor and the two back rooms.
			Seg(house, "CorridorW", false, -2f, 78f, 88f, h, _plaster);
			Seg(house, "CorridorE", false, 2f, 78f, 88f, h, _plaster);
			Roof(house, -10f, 10f, 70f, 88f, h);

			// The front door (locked) and the cellar door (sealed).
			var door = Box(doors, "HouseDoor", new Vector3(0f, 1.7f, 70f), new Vector3(3f, 3.4f, 0.35f), _woodDark);
			door.AddComponent<AssetIdTag>().Id = "props/door";
			var doorUse = door.AddComponent<Interactable>();
			doorUse.Id = "house-door";
			doorUse.Kind = InteractKind.Door;
			doorUse.Prompt = "Open the door";
			doorUse.RequiresItem = Items.HouseKey;
			Gate(door, null, "open:house-door");
			var cellar = Box(doors, "CellarDoor", new Vector3(0f, 1.7f, 78f), new Vector3(3f, 3.4f, 0.35f), _stoneDark);
			cellar.AddComponent<AssetIdTag>().Id = "props/door";
			Prim(PrimitiveType.Cube, "Seal", cellar.transform, new Vector3(0f, 0.15f, -0.6f), new Vector3(0.4f, 0.4f, 0.1f), _gold);
			Gate(cellar, null, "open:basement-door");

			// The three pedestals of the lock.
			for (var i = 0; i < 3; i++)
			{
				var pedestal = Interact(props, "symbol-" + i, InteractKind.Panel, "Turn the symbol", new Vector3(-1.2f + i * 1.2f, 0f, 76.6f));
				pedestal.Slot = i;
				pedestal.gameObject.AddComponent<AssetIdTag>().Id = "props/symbol-pedestal";
				Prim(PrimitiveType.Cube, "Pedestal", pedestal.transform, new Vector3(0f, 0.5f, 0f), new Vector3(0.7f, 1f, 0.7f), _stone);
				Prim(PrimitiveType.Cube, "Top", pedestal.transform, new Vector3(0f, 1.05f, 0f), new Vector3(0.8f, 0.1f, 0.8f), _gold);
				pedestal.Label = Text3D(pedestal.transform, "Star", new Vector3(0f, 1.6f, 0f), 0.09f);
			}

			// Documents and the three carved plates (the clues).
			Document(props, "journal-1", "Read the journal page", new Vector3(6f, 0f, 73f));
			Document(props, "journal-2", "Read the journal page", new Vector3(6f, 0f, 84f));
			Document(props, "journal-3", "Read the journal page", new Vector3(-6f, 0f, 84f));
			Plate(props, "carving-moon", new Vector3(-9.6f, 1.6f, 83f));
			Plate(props, "carving-crown", new Vector3(9.6f, 1.6f, 83f));
			Plate(props, "carving-sun", new Vector3(-9.6f, 1.6f, 74f));
			Prim(PrimitiveType.Cube, "TableHall", props, new Vector3(6f, 0.45f, 73f), new Vector3(1.6f, 0.9f, 0.9f), _woodDark);
			Prim(PrimitiveType.Cube, "TableEast", props, new Vector3(6f, 0.45f, 84f), new Vector3(1.6f, 0.9f, 0.9f), _woodDark);
			Prim(PrimitiveType.Cube, "TableWest", props, new Vector3(-6f, 0.45f, 84f), new Vector3(1.6f, 0.9f, 0.9f), _woodDark);
			Prim(PrimitiveType.Cube, "Bed", props, new Vector3(7.5f, 0.3f, 86.4f), new Vector3(1.4f, 0.6f, 2.2f), _wood);

			// In the cellar corridor: a torch, the stairs down.
			var torch = Interact(props, "torch", InteractKind.Pickup, "Take the torch", new Vector3(0f, 0f, 81.5f));
			torch.ItemId = Items.Torch;
			torch.gameObject.AddComponent<AssetIdTag>().Id = "props/torch";
			Prim(PrimitiveType.Cylinder, "Handle", torch.transform, new Vector3(0f, 0.4f, 0f), new Vector3(0.1f, 0.4f, 0.1f), _woodDark).transform.localRotation = Quaternion.Euler(0f, 0f, 80f);
			Prim(PrimitiveType.Sphere, "Flame", torch.transform, new Vector3(0.35f, 0.45f, 0f), new Vector3(0.2f, 0.28f, 0.2f), _ember);
			Gate(torch.gameObject, null, "got:torch");
			var stairs = Interact(props, "dungeon-stairs", InteractKind.Stairs, "Descend into the cellar", new Vector3(0f, 0f, 86.2f));
			stairs.gameObject.AddComponent<AssetIdTag>().Id = "props/stairs";
			Prim(PrimitiveType.Cube, "Pit", stairs.transform, new Vector3(0f, 0.03f, 0f), new Vector3(2.6f, 0.06f, 2f), _dark);
			for (var i = 0; i < 4; i++) Prim(PrimitiveType.Cube, "Step" + i, stairs.transform, new Vector3(0f, 0.08f, -0.7f + i * 0.45f), new Vector3(2.6f, 0.04f, 0.3f), _stoneDark);
			Gate(stairs.gameObject, "open:basement-door", null);

			Zone(zones, "HouseHall", new Vector3(0f, 2f, 74f), new Vector3(20f, 5f, 8f), new Vector3(-9.3f, 3.2f, 70.7f), 62f, 3);
			Zone(zones, "HouseWest", new Vector3(-6f, 2f, 83f), new Vector3(8f, 5f, 10f), new Vector3(-9.3f, 3.2f, 87.3f), 62f, 3);
			Zone(zones, "HouseEast", new Vector3(6f, 2f, 83f), new Vector3(8f, 5f, 10f), new Vector3(9.3f, 3.2f, 87.3f), 62f, 3);
			Zone(zones, "HouseCellar", new Vector3(0f, 2f, 83f), new Vector3(3.6f, 5f, 10f), new Vector3(0f, 3.1f, 86.9f), 60f, 4);
		}

		// ---- the dungeon (x from 296; the story reaches it down the cellar stairs) ----

		static void BuildDungeon(Transform world, Transform props, Transform zones, Transform triggers, out Transform ritualBody, out Foe boss)
		{
			const float h = 3.8f;
			var d = new GameObject("Dungeon").transform;
			d.SetParent(world, false);

			// Floors.
			Box(d, "FloorLanding", new Vector3(300f, -0.25f, -1f), new Vector3(8f, 0.5f, 10f), _stoneDark);
			Box(d, "FloorA", new Vector3(300f, -0.25f, 16.5f), new Vector3(3f, 0.5f, 25f), _stoneDark);
			Box(d, "FloorB", new Vector3(311f, -0.25f, 28f), new Vector3(25f, 0.5f, 3f), _stoneDark);
			Box(d, "FloorC", new Vector3(322f, -0.25f, 38f), new Vector3(3f, 0.5f, 23f), _stoneDark);
			Box(d, "FloorRitual", new Vector3(323f, -0.25f, 60f), new Vector3(22f, 0.5f, 22f), _stone);

			// Walls: landing, corridor A, corridor B, corridor C, the altar room.
			Seg(d, "LandingW", false, 296f, -6f, 4f, h, _stoneDark);
			Seg(d, "LandingE", false, 304f, -6f, 4f, h, _stoneDark);
			Seg(d, "LandingS", true, -6f, 296f, 304f, h, _stoneDark);
			Seg(d, "LandingNW", true, 4f, 296f, 298.5f, h, _stoneDark);
			Seg(d, "LandingNE", true, 4f, 301.5f, 304f, h, _stoneDark);
			Seg(d, "AW", false, 298.5f, 4f, 29.5f, h, _stoneDark);
			Seg(d, "AE", false, 301.5f, 4f, 26.5f, h, _stoneDark);
			Seg(d, "BS", true, 26.5f, 301.5f, 323.5f, h, _stoneDark);
			Seg(d, "BN", true, 29.5f, 298.5f, 320.5f, h, _stoneDark);
			Seg(d, "CW", false, 320.5f, 29.5f, 49f, h, _stoneDark);
			Seg(d, "CE", false, 323.5f, 26.5f, 49f, h, _stoneDark);
			Seg(d, "RitualS1", true, 49f, 312f, 320.5f, h, _stone);
			Seg(d, "RitualS2", true, 49f, 323.5f, 334f, h, _stone);
			Seg(d, "RitualN", true, 71f, 312f, 334f, h, _stone);
			Seg(d, "RitualW", false, 312f, 49f, 71f, h, _stone);
			Seg(d, "RitualE", false, 334f, 49f, 71f, h, _stone);
			foreach (var p in new[] { new Vector2(316f, 54f), new Vector2(330f, 54f), new Vector2(316f, 66f), new Vector2(330f, 66f) })
				Box(d, "Pillar", new Vector3(p.x, 2f, p.y), new Vector3(1.2f, 4f, 1.2f), _stone);

			// The altar, the braziers, the bodies of the ritual.
			var altar = Box(props, "Altar", new Vector3(323f, 0.6f, 63f), new Vector3(3f, 1.2f, 1.6f), _stone);
			altar.AddComponent<AssetIdTag>().Id = "props/altar";
			Prim(PrimitiveType.Cube, "Slab", altar.transform, new Vector3(0f, 0.55f, 0f), new Vector3(1.1f, 0.1f, 1.1f), _red);
			var use = Interact(props, "altar", InteractKind.Altar, "Examine the altar", new Vector3(323f, 0f, 61.2f));
			Gate(use.gameObject, "done:ritual-room", "done:altar");
			var artifact = Interact(props, "artifact", InteractKind.Artifact, "Take the artifact", new Vector3(323f, 1.2f, 63f));
			artifact.ItemId = Items.Artifact;
			artifact.gameObject.AddComponent<AssetIdTag>().Id = "loot/artifact";
			Prim(PrimitiveType.Cube, "Gem", artifact.transform, new Vector3(0f, 0.5f, 0f), new Vector3(0.4f, 0.4f, 0.4f), _gold).transform.localRotation = Quaternion.Euler(45f, 45f, 0f);
			var glow = artifact.gameObject.AddComponent<Light>();
			glow.type = LightType.Point;
			glow.range = 6f;
			glow.intensity = 1.5f;
			glow.color = new Color(1f, 0.8f, 0.4f);
			Gate(artifact.gameObject, "dead:boss", "got:artifact");

			foreach (var p in new[] { new Vector2(316f, 52f), new Vector2(330f, 52f), new Vector2(316f, 68f), new Vector2(330f, 68f) })
			{
				var brazier = new GameObject("Brazier").transform;
				brazier.SetParent(props, false);
				brazier.position = new Vector3(p.x, 0f, p.y);
				Prim(PrimitiveType.Cylinder, "Stand", brazier, new Vector3(0f, 0.6f, 0f), new Vector3(0.18f, 0.6f, 0.18f), _dark);
				Prim(PrimitiveType.Cylinder, "Bowl", brazier, new Vector3(0f, 1.25f, 0f), new Vector3(0.7f, 0.15f, 0.7f), _dark);
				Prim(PrimitiveType.Sphere, "Fire", brazier, new Vector3(0f, 1.5f, 0f), new Vector3(0.4f, 0.5f, 0.4f), _ember);
				var light = brazier.gameObject.AddComponent<Light>();
				light.type = LightType.Point;
				light.range = 11f;
				light.intensity = 1.6f;
				light.color = new Color(1f, 0.45f, 0.2f);
				light.transform.localPosition = new Vector3(0f, 1.8f, 0f);
			}

			var cloth = Mat("BodyCloth", new Color(0.3f, 0.28f, 0.25f));
			var positions = new[] { new Vector3(320f, 0.3f, 57f), new Vector3(326f, 0.3f, 56f), new Vector3(318f, 0.3f, 62f), new Vector3(328f, 0.3f, 61f), new Vector3(322f, 0.3f, 54f) };
			ritualBody = null;
			for (var i = 0; i < positions.Length; i++)
			{
				var body = new GameObject(i == 0 ? "RitualBody" : "Body" + i).transform;
				body.SetParent(props, false);
				body.position = positions[i];
				body.rotation = Quaternion.Euler(0f, 40f * i, 0f);
				Prim(PrimitiveType.Capsule, "Torso", body, Vector3.zero, new Vector3(0.6f, 0.8f, 0.6f), cloth).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
				Prim(PrimitiveType.Sphere, "Head", body, new Vector3(0f, 0f, 0.95f), new Vector3(0.36f, 0.36f, 0.36f), _bone);
				if (i == 0) ritualBody = body;
			}
			Prim(PrimitiveType.Cylinder, "RitualCircle", props, new Vector3(323f, 0.03f, 60f), new Vector3(9f, 0.02f, 9f), _red);

			// The guardian waits under the altar, off until it rises.
			boss = BuildBoss(props, new Vector3(323f, 0f, 67f));

			Trigger(triggers, "ritual-room", new Vector3(322f, 1.5f, 51f), new Vector3(7f, 3f, 2f));
			Zone(zones, "DungeonLanding", new Vector3(300f, 2f, -1f), new Vector3(8f, 5f, 10f), new Vector3(296.8f, 3.4f, -5.2f), 62f, 2);
			Zone(zones, "DungeonA", new Vector3(300f, 2f, 16.5f), new Vector3(3f, 5f, 25f), new Vector3(300f, 3.4f, 3f), 62f, 2);
			Zone(zones, "DungeonB", new Vector3(311f, 2f, 28f), new Vector3(25f, 5f, 3f), new Vector3(299.2f, 3.4f, 28f), 62f, 2);
			Zone(zones, "DungeonC", new Vector3(322f, 2f, 38f), new Vector3(3f, 5f, 23f), new Vector3(322f, 3.4f, 27.3f), 62f, 2);
			Zone(zones, "RitualRoom", new Vector3(323f, 2f, 60f), new Vector3(22f, 6f, 22f), new Vector3(314.6f, 4.3f, 50.6f), 58f, 3);
		}

		static Foe BuildBoss(Transform parent, Vector3 position)
		{
			var root = new GameObject("Guardian");
			root.transform.SetParent(parent, false);
			root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 180f, 0f));
			root.AddComponent<AssetIdTag>().Id = "monsters/boss-awakened";
			var capsule = root.AddComponent<CapsuleCollider>();
			capsule.radius = 1.1f;
			capsule.height = 3.6f;
			capsule.center = new Vector3(0f, 1.8f, 0f);
			root.AddComponent<Health>();
			var agent = root.AddComponent<NavMeshAgent>();
			agent.radius = 1.0f;
			agent.height = 3.6f;
			agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
			var foe = root.AddComponent<Foe>();
			foe.Id = "boss";
			foe.Configure(FoeKind.Boss);

			var visual = new GameObject("Visual").transform;
			visual.SetParent(root.transform, false);
			foe.Visual = visual;
			var rock = Mat("Guardian", new Color(0.3f, 0.28f, 0.3f));
			var glow = Mat("GuardianEye", new Color(1f, 0.25f, 0.1f));
			Prim(PrimitiveType.Cylinder, "Body", visual, new Vector3(0f, 1.6f, 0f), new Vector3(2.0f, 1.6f, 1.6f), rock);
			Prim(PrimitiveType.Sphere, "Head", visual, new Vector3(0f, 3.4f, 0.1f), new Vector3(1.1f, 1.0f, 1.1f), rock);
			Prim(PrimitiveType.Sphere, "EyeL", visual, new Vector3(-0.25f, 3.45f, 0.5f), new Vector3(0.2f, 0.2f, 0.2f), glow);
			Prim(PrimitiveType.Sphere, "EyeR", visual, new Vector3(0.25f, 3.45f, 0.5f), new Vector3(0.2f, 0.2f, 0.2f), glow);
			Prim(PrimitiveType.Cube, "ArmL", visual, new Vector3(-1.4f, 2.1f, 0.2f), new Vector3(0.7f, 2.2f, 0.7f), rock);
			Prim(PrimitiveType.Cube, "ArmR", visual, new Vector3(1.4f, 2.1f, 0.2f), new Vector3(0.7f, 2.2f, 0.7f), rock);
			Prim(PrimitiveType.Cube, "FistL", visual, new Vector3(-1.4f, 0.8f, 0.4f), new Vector3(1.0f, 0.9f, 1.0f), rock);
			Prim(PrimitiveType.Cube, "Maul", visual, new Vector3(1.5f, 0.9f, 0.7f), new Vector3(1.0f, 1.0f, 1.8f), _dark);
			root.AddComponent<HealthBar3D>().Height = 4.6f;
			root.GetComponent<HealthBar3D>().Width = 2.6f;
			return foe;
		}

		// ---- hero, shots, checkpoints, camera ----

		static Hero BuildHero()
		{
			var root = new GameObject("Hero");
			root.transform.position = new Vector3(0f, 0.1f, -70f);
			var controller = root.AddComponent<CharacterController>();
			controller.height = 1.8f;
			controller.radius = 0.35f;
			controller.center = new Vector3(0f, 0.9f, 0f);
			controller.stepOffset = 0.3f;
			var health = root.AddComponent<Health>();
			health.MaxHealth = Fighting.HeroHealth;
			health.Team = 1;
			health.Restore();
			var hero = root.AddComponent<Hero>();
			root.AddComponent<AssetIdTag>().Id = "characters/hero-warrior";

			var visual = new GameObject("Visual").transform;
			visual.SetParent(root.transform, false);
			hero.Visual = visual;
			var mail = Mat("HeroMail", new Color(0.36f, 0.4f, 0.5f));
			var tabard = Mat("HeroTabard", new Color(0.6f, 0.15f, 0.12f));
			Prim(PrimitiveType.Capsule, "Torso", visual, new Vector3(0f, 0.9f, 0f), new Vector3(0.7f, 0.9f, 0.7f), mail);
			Prim(PrimitiveType.Cube, "Tabard", visual, new Vector3(0f, 0.85f, 0.3f), new Vector3(0.4f, 0.8f, 0.06f), tabard);
			Prim(PrimitiveType.Sphere, "Head", visual, new Vector3(0f, 1.65f, 0.02f), new Vector3(0.38f, 0.38f, 0.38f), _skin);
			Prim(PrimitiveType.Sphere, "Helm", visual, new Vector3(0f, 1.72f, 0.02f), new Vector3(0.42f, 0.3f, 0.42f), _steel);

			var sword = new GameObject("SwordPivot").transform;
			sword.SetParent(visual, false);
			sword.localPosition = new Vector3(0.45f, 1.0f, 0.1f);
			Prim(PrimitiveType.Cube, "Blade", sword, new Vector3(0f, 0f, 0.55f), new Vector3(0.07f, 0.07f, 1.0f), _steel);
			Prim(PrimitiveType.Cube, "Guard", sword, new Vector3(0f, 0f, 0.05f), new Vector3(0.3f, 0.07f, 0.08f), _gold);
			hero.SwordPivot = sword;

			var shield = new GameObject("ShieldPivot").transform;
			shield.SetParent(visual, false);
			shield.localPosition = new Vector3(-0.5f, 1.0f, 0.1f);
			Prim(PrimitiveType.Cube, "Shield", shield, Vector3.zero, new Vector3(0.7f, 0.9f, 0.1f), tabard);
			Prim(PrimitiveType.Cube, "Boss", shield, new Vector3(0f, 0f, -0.07f), new Vector3(0.2f, 0.2f, 0.06f), _gold);
			hero.ShieldPivot = shield;

			var torch = new GameObject("Torch");
			torch.transform.SetParent(root.transform, false);
			torch.transform.localPosition = new Vector3(0.4f, 1.9f, 0.4f);
			var light = torch.AddComponent<Light>();
			light.type = LightType.Point;
			light.range = 12f;
			light.intensity = 2.6f;
			light.color = new Color(1f, 0.72f, 0.4f);
			light.shadows = LightShadows.None;
			light.enabled = false;
			hero.Torch = light;
			return hero;
		}

		static Transform[] BuildCheckpoints()
		{
			var root = new GameObject("Checkpoints").transform;
			var spots = new[]
			{
				(new Vector3(0f, 0.1f, -70f), 0f), (new Vector3(0f, 0.1f, -2f), 0f), (new Vector3(0f, 0.1f, 50f), 0f),
				(new Vector3(0f, 0.1f, 75f), 0f), (new Vector3(300f, 0.1f, -2f), 0f), (new Vector3(322f, 0.1f, 45f), 0f),
				(new Vector3(0f, 0.1f, 14f), 180f),
			};
			var result = new Transform[spots.Length];
			for (var i = 0; i < spots.Length; i++)
			{
				var spot = new GameObject(i < 6 ? "Checkpoint" + i : "EndingSpot").transform;
				spot.SetParent(root, false);
				spot.SetPositionAndRotation(spots[i].Item1, Quaternion.Euler(0f, spots[i].Item2, 0f));
				result[i] = spot;
			}
			return result;
		}

		static Transform BuildShots(out Transform figure)
		{
			var root = new GameObject("Shots").transform;
			void Add(string name, Vector3 position)
			{
				var shot = new GameObject(name).transform;
				shot.SetParent(root, false);
				shot.position = position;
			}
			Add("window", new Vector3(-22.7f, 1.7f, 14.9f));
			Add("window-look", new Vector3(-20f, 1.5f, 22f));
			Add("figure-a", new Vector3(-26f, 1f, 24.2f));
			Add("figure-b", new Vector3(-14f, 1f, 24.2f));
			Add("ritual", new Vector3(318.5f, 2.6f, 50.8f));
			Add("ritual-look", new Vector3(320f, 0.4f, 57f));
			Add("boss", new Vector3(323f, 0.7f, 56.5f));
			Add("boss-look", new Vector3(323f, 2.4f, 66.5f));
			Add("ending-a", new Vector3(0f, 1.7f, 9.5f));
			Add("ending-a-look", new Vector3(0f, 1.5f, 14f));
			Add("ending-b", new Vector3(0f, 8f, -8f));

			var shade = new GameObject("WindowFigure");
			shade.transform.SetParent(root, false);
			Prim(PrimitiveType.Capsule, "Shade", shade.transform, Vector3.zero, new Vector3(0.8f, 1f, 0.8f), _dark);
			shade.SetActive(false);
			figure = shade.transform;
			return root;
		}

		static CameraRig BuildCameraAndLight(Hero hero, out Light sun)
		{
			var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
			cameraObject.transform.position = new Vector3(-5f, 11f, -76f);
			var camera = cameraObject.AddComponent<Camera>();
			camera.clearFlags = CameraClearFlags.SolidColor;
			camera.backgroundColor = new Color(0.05f, 0.06f, 0.09f);
			camera.fieldOfView = 50f;
			camera.nearClipPlane = 0.1f;
			camera.farClipPlane = 300f;
			cameraObject.AddComponent<AudioListener>();
			var rig = cameraObject.AddComponent<CameraRig>();
			rig.Target = hero;

			var moon = new GameObject("Moon");
			moon.transform.rotation = Quaternion.Euler(48f, -30f, 0f);
			sun = moon.AddComponent<Light>();
			sun.type = LightType.Directional;
			sun.intensity = 0.9f;
			sun.color = new Color(0.65f, 0.72f, 1f);
			sun.shadows = LightShadows.Soft;
			return rig;
		}

		static void BuildBoundaries()
		{
			var root = new GameObject("Boundary").transform;
			void Fence(string name, Vector3 center, Vector3 size)
			{
				var go = new GameObject(name);
				go.transform.SetParent(root, false);
				go.transform.position = center;
				go.AddComponent<BoxCollider>().size = size;
			}
			// The road is a corridor of forest; the village is fenced by its outskirts.
			Fence("RoadW", new Vector3(-12f, 3f, -42f), new Vector3(1f, 6f, 72f));
			Fence("RoadE", new Vector3(12f, 3f, -42f), new Vector3(1f, 6f, 72f));
			Fence("RoadS", new Vector3(0f, 3f, -78f), new Vector3(26f, 6f, 1f));
			Fence("VillageW", new Vector3(-34f, 3f, 46f), new Vector3(1f, 6f, 110f));
			Fence("VillageE", new Vector3(34f, 3f, 46f), new Vector3(1f, 6f, 110f));
			Fence("VillageN", new Vector3(0f, 3f, 98f), new Vector3(70f, 6f, 1f));
			Fence("VillageSW", new Vector3(-23f, 3f, -7f), new Vector3(22f, 6f, 1f));
			Fence("VillageSE", new Vector3(23f, 3f, -7f), new Vector3(22f, 6f, 1f));
		}

		// ---- helpers ----

		static void House(Transform parent, string name, Vector3 center, Vector3 size)
		{
			var house = new GameObject(name).transform;
			house.SetParent(parent, false);
			house.gameObject.AddComponent<AssetIdTag>().Id = "buildings/house";
			Box(house, "Walls", center + Vector3.up * (size.y * 0.5f), size, _plaster);
			Box(house, "Roof", center + Vector3.up * (size.y + 0.4f), new Vector3(size.x + 0.8f, 0.8f, size.z + 0.8f), _roof);
			Prim(PrimitiveType.Cube, "Door", house, center + new Vector3(0f, 1.1f, -size.z * 0.5f - 0.03f), new Vector3(1.2f, 2.2f, 0.08f), _woodDark);
			Prim(PrimitiveType.Cube, "Window", house, center + new Vector3(size.x * 0.28f, 1.8f, -size.z * 0.5f - 0.03f), new Vector3(0.8f, 0.8f, 0.08f), _dark);
		}

		static void Stall(Transform parent, Vector3 position, float yaw)
		{
			var stall = new GameObject("Stall").transform;
			stall.SetParent(parent, false);
			stall.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
			stall.gameObject.AddComponent<AssetIdTag>().Id = "props/market-stall";
			Prim(PrimitiveType.Cube, "Table", stall, new Vector3(0f, 0.5f, 0f), new Vector3(2.4f, 0.1f, 1.1f), _wood);
			foreach (var x in new[] { -1.1f, 1.1f })
			{
				Prim(PrimitiveType.Cube, "Leg", stall, new Vector3(x, 0.25f, 0f), new Vector3(0.1f, 0.5f, 0.9f), _woodDark);
				Prim(PrimitiveType.Cube, "Pole", stall, new Vector3(x, 1.2f, 0.45f), new Vector3(0.1f, 2.4f, 0.1f), _woodDark);
			}
			Prim(PrimitiveType.Cube, "Awning", stall, new Vector3(0f, 2.4f, 0.1f), new Vector3(2.8f, 0.08f, 1.6f), _cloth).transform.localRotation = Quaternion.Euler(10f, 0f, 0f);
		}

		static void Tree(Transform parent, Vector3 position, float height)
		{
			var tree = new GameObject("Tree").transform;
			tree.SetParent(parent, false);
			tree.position = position;
			tree.gameObject.AddComponent<AssetIdTag>().Id = "resources/wood-node";
			Prim(PrimitiveType.Cylinder, "Trunk", tree, new Vector3(0f, height * 0.35f, 0f), new Vector3(0.6f, height * 0.35f, 0.6f), _woodDark);
			var leaves = Mat("Pine", new Color(0.1f, 0.22f, 0.14f));
			Prim(PrimitiveType.Sphere, "Crown", tree, new Vector3(0f, height * 0.85f, 0f), new Vector3(2.6f, height * 0.7f, 2.6f), leaves);
		}

		static void Potion(Transform parent, string id, Vector3 position)
		{
			var potion = Interact(parent, id, InteractKind.Pickup, "Take the healing potion", position);
			potion.ItemId = Items.Potion;
			potion.gameObject.AddComponent<AssetIdTag>().Id = "loot/healing-potion";
			Prim(PrimitiveType.Sphere, "Flask", potion.transform, new Vector3(0f, 0.55f, 0f), new Vector3(0.25f, 0.25f, 0.25f), _red);
			Prim(PrimitiveType.Cylinder, "Neck", potion.transform, new Vector3(0f, 0.72f, 0f), new Vector3(0.08f, 0.08f, 0.08f), _bone);
			Gate(potion.gameObject, null, "got:" + id);
		}

		static void Document(Transform parent, string id, string prompt, Vector3 position)
		{
			var note = Interact(parent, id, InteractKind.Note, prompt, position);
			Prim(PrimitiveType.Cube, "Paper", note.transform, new Vector3(0f, 0.95f, 0f), new Vector3(0.3f, 0.02f, 0.4f), _paper);
		}

		static void Plate(Transform parent, string id, Vector3 position)
		{
			var plate = Interact(parent, id, InteractKind.Note, "Examine the carved plate", new Vector3(position.x, 0f, position.z));
			Prim(PrimitiveType.Cube, "Plate", plate.transform, new Vector3(0f, position.y, 0f), new Vector3(0.12f, 0.9f, 0.9f), _gold);
		}

		static Interactable Interact(Transform parent, string id, InteractKind kind, string prompt, Vector3 position)
		{
			var go = new GameObject(id);
			go.transform.SetParent(parent, false);
			go.transform.position = position;
			var use = go.AddComponent<Interactable>();
			use.Id = id;
			use.Kind = kind;
			use.Prompt = prompt;
			return use;
		}

		static void Gate(GameObject target, string showWhen, string hideWhen)
		{
			var gate = target.AddComponent<FlagGate>();
			gate.ShowWhen = showWhen;
			gate.HideWhen = hideWhen;
		}

		static void Zone(Transform parent, string name, Vector3 center, Vector3 size, Vector3 anchor, float fov, int priority)
		{
			var go = new GameObject(name);
			go.transform.SetParent(parent, false);
			go.transform.position = center;
			var box = go.AddComponent<BoxCollider>();
			box.isTrigger = true;
			box.size = size;
			var zone = go.AddComponent<CameraZone>();
			var anchorObject = new GameObject("Anchor").transform;
			anchorObject.SetParent(go.transform, false);
			anchorObject.position = anchor;
			zone.Anchor = anchorObject;
			zone.Fov = fov;
			zone.Priority = priority;
		}

		static void Trigger(Transform parent, string id, Vector3 center, Vector3 size)
		{
			var go = new GameObject(id);
			go.transform.SetParent(parent, false);
			go.transform.position = center;
			var box = go.AddComponent<BoxCollider>();
			box.isTrigger = true;
			box.size = size;
			go.AddComponent<StoryTrigger>().Id = id;
		}

		/// <summary>A wall along X (at <paramref name="fixedZ"/>) or along Z (at <paramref name="fixedX"/>) from one end to the other.</summary>
		static GameObject Seg(Transform parent, string name, bool alongX, float fixedCoord, float from, float to, float height, Material material, float thickness = 0.5f)
		{
			var length = Mathf.Abs(to - from);
			var middle = (from + to) * 0.5f;
			return alongX
				? Box(parent, name, new Vector3(middle, height * 0.5f, fixedCoord), new Vector3(length + thickness, height, thickness), material)
				: Box(parent, name, new Vector3(fixedCoord, height * 0.5f, middle), new Vector3(thickness, height, length + thickness), material);
		}

		/// <summary>A wall with doorways: the gaps are (start, end) along the wall.</summary>
		static void Gapped(Transform parent, string name, bool alongX, float fixedCoord, float from, float to, float height, Material material, params Vector2[] gaps)
		{
			var list = new List<Vector2>(gaps);
			list.Sort((a, b) => a.x.CompareTo(b.x));
			var cursor = from;
			var index = 0;
			foreach (var gap in list)
			{
				if (gap.x > cursor) Seg(parent, name + index++, alongX, fixedCoord, cursor, gap.x, height, material);
				cursor = gap.y;
			}
			if (cursor < to) Seg(parent, name + index, alongX, fixedCoord, cursor, to, height, material);
		}

		static void Roof(Transform parent, float x0, float x1, float z0, float z1, float y)
		{
			Box(parent, "Roof", new Vector3((x0 + x1) * 0.5f, y + 0.2f, (z0 + z1) * 0.5f), new Vector3(x1 - x0 + 1.2f, 0.4f, z1 - z0 + 1.2f), _roof);
		}

		static TextMesh Text3D(Transform parent, string text, Vector3 localPosition, float size)
		{
			var go = new GameObject("Label");
			go.transform.SetParent(parent, false);
			go.transform.localPosition = localPosition;
			var mesh = go.AddComponent<TextMesh>();
			mesh.text = text;
			mesh.anchor = TextAnchor.MiddleCenter;
			mesh.alignment = TextAlignment.Center;
			mesh.characterSize = size;
			mesh.fontSize = 48;
			mesh.color = new Color(1f, 0.85f, 0.4f);
			var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
			mesh.font = font;
			go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
			return mesh;
		}

		// Shared with the other games: 3d-unity-graybox.
		static GameObject Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material) => GrayboxBuilder.Box(parent, name, position, scale, material);

		static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Material material, bool keepCollider = false) => GrayboxBuilder.Prim(type, name, parent, position, scale, material, keepCollider);

		static Material Mat(string name, Color colour) => GrayboxBuilder.Mat(name, colour, MaterialDir);
	}
}
