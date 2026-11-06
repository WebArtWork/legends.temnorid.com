using ITKamianets.Engine.Shell;
using UnityEngine;

namespace Temnorid.Legends
{
	/// <summary>What the shared shell (splash, landing, lobby, top and bottom bars) needs to know about this game.</summary>
	public static class LegendsShell
	{
		public const string Repo = "WebArtWork/legends.temnorid.com";

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		static void Configure()
		{
			ShellConfig.Current = new ShellConfig
			{
				Title = "Temnorid Legends",
				Tagline = "The Abandoned Village",
				GameId = "legends",
				Repo = Repo,
				InstallerSuffix = "-Setup.exe",
				MaxPlayers = 2,
				// One player: sign in, then straight into the village, where an elder starts the story. No landing screen, no lobby,
				// no Profile for now.
				Multiplayer = false,
				Profile = false,
				SkipLanding = true,
				GameScene = "Main",
				Accent = new Color(0.92f, 0.62f, 0.25f),
			};
		}
	}
}
