using System;
using System.IO;
using System.Linq;

using Xamarin.Android.Tools.VSWhere;

namespace XAPerfTestRunner
{
	static partial class Utilities
	{
		static VisualStudioInstance vsInstance;

		public const bool IsUnix = false;
		public const bool IsWindows = true;

		static void InitOS ()
		{
			vsInstance = MSBuildLocator.QueryLatest ();

			string[]? pathext = Environment.GetEnvironmentVariable ("PATHEXT")?.Split (new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
			if (pathext == null || pathext.Length == 0) {
				ExecutableExtensions.Add (".exe");
				ExecutableExtensions.Add (".cmd");
				ExecutableExtensions.Add (".bat");
			} else {
				foreach (string ext in pathext!) {
					ExecutableExtensions.Add (ext.ToLowerInvariant ());
				}
			}
		}

		public static string GetManagedProgramRunner (string programPath)
		{
			return String.Empty;
		}

		public static string Which (string programPath, bool required = true)
		{
			if (String.Compare ("msbuild", programPath, StringComparison.OrdinalIgnoreCase) == 0) {
				return vsInstance.MSBuildPath;
			}

			if (String.Compare ("sn", programPath, StringComparison.OrdinalIgnoreCase) == 0) {
				var netFXToolsPath = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.ProgramFilesX86), "Microsoft SDKs", "Windows", "v10.0A", "bin");
				var latestOrDefaultSn = Directory.EnumerateFiles (netFXToolsPath, "sn.exe", SearchOption.AllDirectories).LastOrDefault ();
				return latestOrDefaultSn != null && File.Exists (latestOrDefaultSn) ? latestOrDefaultSn : WhichCommon (programPath, required);
			}

			return WhichCommon (programPath, required);
		}

		public static bool FileExists (string filePath)
		{
			return File.Exists (filePath);
		}

		static string AssertIsExecutable (string fullPath)
		{
			return fullPath;
		}
	}
}
