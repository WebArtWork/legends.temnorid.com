using System;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Temnorid.Legends.EditorTools
{
	/// <summary>
	/// Makes the Windows player (<c>Builds/Windows/TemnoridLegends</c>) and a portable zip next to it.
	/// The installer is made from that folder by <c>installer/Legends.iss</c> (Inno Setup).
	/// Command line: <c>-executeMethod Temnorid.Legends.EditorTools.LegendsReleaseBuilder.BuildWindows</c>.
	/// </summary>
	public static class LegendsReleaseBuilder
	{
		const string ScenePath = "Assets/Scenes/Main.unity";
		const string PlayerFolder = "Builds/Windows/TemnoridLegends";
		const string ExeName = "TemnoridLegends.exe";

		[MenuItem("Temnorid/Legends/Build Windows Player")]
		public static void BuildWindows()
		{
			if (!File.Exists(ScenePath)) throw new InvalidOperationException("Build the scene first: Temnorid > Legends > Build Scene.");

			if (Directory.Exists(PlayerFolder)) Directory.Delete(PlayerFolder, true);
			Directory.CreateDirectory(PlayerFolder);

			var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
			{
				// Boot, Menu and the game: the scenes the scene builder put in the build settings.
				scenes = Array.ConvertAll(Array.FindAll(EditorBuildSettings.scenes, s => s.enabled), s => s.path),
				locationPathName = PlayerFolder + "/" + ExeName,
				target = BuildTarget.StandaloneWindows64,
				options = BuildOptions.None,
			});
			if (report.summary.result != BuildResult.Succeeded)
				throw new Exception("Windows build failed: " + report.summary.result + ", " + report.summary.totalErrors + " errors.");

			foreach (var folder in Directory.GetDirectories(PlayerFolder))
			{
				var name = Path.GetFileName(folder);
				if (name.Contains("DoNotShip") || name.Contains("BackUpThisFolder")) Directory.Delete(folder, true);
			}

			var zip = "Builds/Windows/Temnorid-Legends-" + PlayerSettings.bundleVersion + "-windows.zip";
			if (File.Exists(zip)) File.Delete(zip);
			ZipFile.CreateFromDirectory(PlayerFolder, zip);

			Debug.Log("[Legends] Windows build ready: " + PlayerFolder + "/" + ExeName + " (" + (report.summary.totalSize / (1024 * 1024)) + " MB), zip: " + zip);
		}
	}
}
