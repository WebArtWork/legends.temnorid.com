# Temnorid Legends

**Every legend has its own hero.**

A story-driven 3D action, adventure and survival-horror game in the dark medieval world of [Temnorid](https://temnorid.com), for **Windows**, built on the [IT Kamianets 3D Engine](https://3d.itkamianets.com) with Unity 6000.6.

**Status:** first demo, *The Abandoned Village* (0.2.0), built and tested; the installer is not published yet. Website: https://legends.temnorid.com · Download: [Temnorid-Legends-Setup.exe](https://github.com/WebArtWork/legends.temnorid.com/releases/latest/download/Temnorid-Legends-Setup.exe)

## What it is

Temnorid Legends is a series of separate stories. Each legend has its own hero, plot, mood and ending: some are about wars and heroic deeds, others about adventure, intrigue, vanishings or horrors from beyond. All of them share the same controls, cameras, combat and ways of touching the world, so a new legend does not start from nothing.

## The first demo: The Abandoned Village

About 15 to 20 minutes. Mystery, survival horror and action.

A small settlement on the border of Temnorid has stopped sending goods to the neighbouring town. The traders who went to look did not come back. The local lord sends a warrior to find out what happened. You are that warrior.

### The story, scene by scene

1. **The road to the village.** A narrow forest road under a high camera. At an abandoned cart the camera drops to a low side shot: scattered goods, signs of a struggle, and a trader's note to read.
2. **The square.** Empty houses, deserted stalls, a cold fire; the camera watches from a rooftop. Knocking comes from a house. A wounded villager warns you: *do not go underground, that is where they come from.* Something passes the window.
3. **The first fight.** In a narrow street a villager stands with his back to you. He turns slowly, his face twisted, an axe in his hand. The camera slides from its fixed shot to over your shoulder and the fight begins. When it is over, the fixed cameras return.
4. **The elder's house.** Find the key in the storeroom, open the door, read the documents about the strange weeks before, and open the sealed cellar door. Three symbols must be set in the right order; the clues are carved on plates in the rooms of the house.
5. **The dungeon.** Down the stairs it is almost black. Take a torch to see. Narrow corridors, strange sounds behind the walls, a room of bodies around a stone altar, and one of them moves.
6. **The final fight.** Touch the altar and a great guardian rises. Dodge its three attacks, block, and strike when it is open. Heal when you must. Take the artifact.
7. **The ending.** The hero climbs out as the sun rises over the village, looking at an artifact with a symbol he does not know. *This was only the first of the legends of Temnorid.*

### Cameras

| Mode | When | How it behaves |
| --- | --- | --- |
| Exploration | Roads, streets, rooms | Fixed cinematic shots, like the classic survival-horror games. The shot changes by itself as you move. You walk relative to the camera you started moving with, so a change of shot never turns you around by surprise. |
| Combat | Whenever a fight is on | Over the shoulder with free look. It starts and ends by itself, and the fixed shots come back when the danger is over. |
| Cinematic | Story moments | Scripted shots for the window figure, the ritual room, the guardian's rising and the ending, with smooth transitions. |

### Controls

| Key | Action |
| --- | --- |
| W A S D | Move (relative to the camera) |
| Shift | Run |
| E | Interact: talk, read, take, open, turn a symbol |
| Left mouse | Sword attack |
| Right mouse (hold) | Block with the shield (stops most of a blow from the front) |
| Space | Roll (a short moment of safety) |
| H | Drink a healing potion |
| I | Inventory and documents found |
| Mouse | Look around (in a fight) |
| Esc | Pause menu |

### Playing with others

Two heroes can explore the village together. On the first screen choose **Play with others**, then **Host a game here** (a friend on the same network types the address it shows) or **Host online** (the friend types a short code; needs the game linked to Unity Cloud). Press **Ready**; the host starts and chooses Continue or New game. The story is shared: the pack, the documents, the cellar lock, the checkpoints and the story flags are the host's, so whatever either hero finds, opens or reaches counts for both. The host's machine runs the villager and the guardian; both heroes fight them, each with their own sword, shield and roll, and each hero's blows are checked by the host. Each player has their own camera. Story scenes (the ritual room, the guardian rising, the descent, the ending) play on both screens. If a hero falls, they get up again at the last checkpoint after a few seconds, so nobody waits for the other. If the host leaves, the game ends for both. A guest's own single-player save is never touched.

### Updates

When a newer release is published on GitHub, the Windows game downloads and installs it by itself the next time it starts, and starts again. Turn it off in **Profile** if you prefer. On Android and iOS the update comes from the store or the release page.

### Phones

Android and iOS builds are made with Unity Build Profiles (Android 8 or newer, iOS 15 or newer, landscape). On a phone the left thumb stick walks (push it all the way to run), and the buttons on the right are **Attack**, **Block** (hold), **Roll**, **Use** (talk, read, take, open) and **Potion**; the **Journal** tab shows the pack and the documents; tap a dialogue or a document to go on. In a fight the camera turns toward the foe by itself, so no mouse is needed. The tabs are along the bottom of the screen.

### Fighting

- The sword reaches about 2.6 m in front and takes aim at the nearest foe.
- A block stops most of a blow from the front but only half of the guardian's overhead blow. A roll gives a moment in which nothing can hurt you.
- Every enemy attack is announced by a red mark on the floor and a wind-up. The villager can be staggered by a hit; the guardian cannot.
- The guardian has three attacks: a heavy blow from above, a wide sweep, and a rush straight at you.
- Health is 120. A healing potion gives 50. There are two potions on the way.

### Saving

The game saves at checkpoints: the village gate, after the first fight, when the cellar opens, at the foot of the stairs, and in the altar room. If you fall, you carry on from the last checkpoint with the story and your pack as they were then.

## What is in the demo

| System | In the demo |
| --- | --- |
| Hero | One warrior with sword and shield |
| Cameras | Exploration, Combat, Cinematic |
| Controls | Move, run, interact |
| Fighting | Attack, block, roll |
| Enemies | One kind of cursed villager, one boss |
| Characters | One wounded villager to talk to |
| Inventory | Key, torch, potions, artifact, documents |
| Puzzle | The three-symbol cellar lock |
| Places | Road, village, elder's house, dungeon |
| Saves | Checkpoints |
| Screens | Health, objective, inventory, dialogue, documents, menus |

## What comes next

More independent legends that use the same systems: *The Last Defender* (a fortress under siege), *The Lost Temple* (ruins, puzzles, an ancient artifact), *The Black Forest* (a hunter searching for missing people) and *The Fall of the King* (betrayal and the fight for the throne).

## How it connects to Lords

This is for a later phase: [Lords](https://lords.temnorid.com) is the central strategy game and every Temnorid game will be connected to it. **This version does not talk to Lords**; progress is saved locally on the player's PC. Keep identifiers and save data simple and stable so the connection is cheap to add later.

Everything is drawn with Unity primitives (blocks, capsules, spheres), simple colours and text. No models, no paid assets. Every visible object has a stable asset id (for example `monsters/boss-awakened`) so real models can replace the placeholders without touching gameplay. The list of models to replace is on https://temnorid.com/models.

## For developers and agents

- **Unity:** 6000.6, Universal 3D (URP). Open the folder in Unity Hub. `Library/` is rebuilt locally and not committed.
- **Platform:** Windows (keyboard and mouse), Android and iOS (touch). Local saves; co-op for two through `3d-unity-net`.
- **Starts from:** the IT Kamianets 3D Engine [Character View starter](AGENTS.md). Read [AGENTS.md](AGENTS.md) first for the engine packages and workflow.
- **Engine pieces this game uses:** `3d-unity-core`, `3d-unity-input`, `3d-unity-save`, `3d-unity-inventory`, `3d-unity-combat` (health, damage), `3d-unity-feedback` (warnings, health bars), `3d-unity-graybox` (scene building, asset ids), `3d-unity-ui`. The cameras and the character control are written in this game because no engine package does fixed shots yet.
- **Game code (`Assets/Scripts`):**
  - `LegendsRules` holds the story flags, documents, objectives, the symbol lock, the fighting numbers, the save and the session, with no Unity objects, so it is tested without the Editor.
  - `LegendsGame` runs the story: every interaction, trigger, scene, checkpoint and the camera mode.
  - `Hero`, `Foe`, `CameraRig` and `CameraZone` (a fixed shot per place), `Interactable`, `StoryTrigger`, `FlagGate` (shows or hides an object by story flags, which is how a loaded checkpoint looks right), `LegendsHud`.
  - The map is built by `Assets/Scripts/Editor/LegendsSceneBuilder.cs` (menu Temnorid > Legends > Build Scene); the Windows player by `LegendsReleaseBuilder` and `installer/Legends.iss`.
- **Where code lives:**
  - `3d-*` packages: generic, engine-level behaviour with no Temnorid content.
  - `temnorid-*` packages: Temnorid content shared by **several games**. Never `temnorid-<game>-*`.
  - `Assets/Scripts`: this game's own rules, scene builder, HUD and menus.
- **Packages while developing:** the packages sit in the shared workspace folder next to the games and are referenced from `Packages/manifest.json` by local path. They move to their own repositories later, and only the paths change.
- **Tests:** 23 edit-mode tests (rules, lock, fighting maths, saves) and 10 play-mode tests (three of them online and touch, see `OnlineLegendsTests`): the whole demo played from the road to the ending, the fight maths on a real hero, the cameras, retry from a checkpoint, the guardian using all three attacks, and a check that every fixed camera is in open air and sees its place.
- **Balance:** `BalanceTests.Bot_fights_the_guardian` (explicit play-mode test) plays the boss fight with a bot that blocks the sweep and rolls out of the overhead blow and the dash; it wins 6 of 6 with 49 to 77% health left, in about a minute.
- **Shell and multiplayer (0.2.0):** scenes `Boot` (splash, update check), `Menu` (landing, profile, lobby) and `Main` (the village) come from `3d-unity-shell` (`LegendsShell.cs` configures it: game id `legends`, repo `WebArtWork/legends.temnorid.com`, two players, places Village and Journal). Online code is in `LegendsNet.cs` (events, poses, entities), `RemoteHero.cs` (the other hero, a plain object with `RemoteBody`), and the guest/host branches of `LegendsGame`, `Hero` (a guest's swing is a `host.hit` request) and `Foe` (hunts the nearest `IPrey`; on a guest it only shows the host's state). **Authority rules:** the host (`NetSession.IsAuthority`) alone runs the foes, the story flags, the pack, the lock and the checkpoints and publishes them (`leg.state`, foes as entities 1 = villager, 2 = guardian); a guest sends requests named `host.use`, `host.trigger`, `host.hit`, `host.potion` which the host validates (distance from the guest's published pose, sword cooldown); blows on a hero are decided by the host and resolved by that hero's owner (`leg.dmg`). Test port 7805. Single-player numbers in `LegendsRules` are unchanged.
- **Do not add:** Firebase, accounts, or any link to Lords in this version.

## Website and downloads

The `docs/` folder is this game's public website (`docs/index.html`, served at https://legends.temnorid.com through GitHub Pages; `docs/CNAME` holds the domain). It repeats the information in this README: what the game is, the demo, controls and the download button.

The download button points to the latest GitHub Release asset:

`https://github.com/WebArtWork/legends.temnorid.com/releases/latest/download/Temnorid-Legends-Setup.exe`

Release steps (when the demo is ready):

1. Build the Windows player from Unity (menu Temnorid > Legends > Build Windows Player).
2. Wrap the build in the Windows installer `Temnorid-Legends-Setup.exe` (`installer/Legends.iss`, Inno Setup).
3. Create a GitHub Release (tag `v0.2.0`) and attach that file.
4. Open the website, click the button, install, play, save, restart, uninstall.

When you change the game's description, controls or rules, update this README and `docs/index.html` together.

## License

See [LICENSE](LICENSE).
