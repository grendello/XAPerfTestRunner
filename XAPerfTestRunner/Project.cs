using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace XAPerfTestRunner
{
	class Project
	{
		static readonly char[] TimingSplitChars = new char[]{ ':' };
		static readonly Regex DisplayedRegex = new Regex ("^\\+(?<val>\\d+s)?(?<ms>\\d+)ms", RegexOptions.Compiled);

		readonly Context context;
		readonly string runId;
		readonly bool projectUsesGit;

		List<RunDefinition> runs;
		XAVersionInfo xaVersion = new XAVersionInfo ();
		bool xaVersionNotDetectedYet = true;
		string? projectGitCommit;
		string? projectGitBranch;
		uint repetitionCount;
		AndroidDeviceInfo adi = new AndroidDeviceInfo ();
		string configuration;

		public string FullProjectFilePath { get; }
		public string FullProjectDirPath { get; }
		public string FullBinDirPath { get; }
		public string FullObjDirPath { get; }
		public string FullAndroidManifestPath { get; }
		public string FullDataDirectoryPath { get; }
		public string? GitBranch => projectGitBranch;
		public string? GitCommit => projectGitCommit;
		public ProjectConfig? ProjectConfig { get; }
		public List<RunDefinition> Runs => runs;
		public uint RepetitionCount => repetitionCount;
		public AndroidDeviceInfo AndroidDevice => adi;
		public XAVersionInfo XAVersion => xaVersion;
		public string Description { get; }
		public string Configuration => configuration;
		public DateTime WhenUTC { get; }

		public Project (Context context, string projectPath, ProjectConfig? projectConfig = null)
		{
			if (projectPath.Length == 0)
				throw new ArgumentException ("must not be empty", nameof (projectPath));
			this.context = context;
			ProjectConfig = projectConfig;

			uint? projectConfigRepetitions = null;
			if (!String.IsNullOrEmpty (projectConfig?.Repetitions)) {
				if (!UInt32.TryParse (projectConfig.Repetitions, out uint val))
					throw new InvalidOperationException ($"Project configuration contains invalid number of repetitions ('{projectConfig.Repetitions}')");
				projectConfigRepetitions = val;
			}

			if (!String.IsNullOrEmpty (projectConfig?.Description))
				Description = projectConfig.Description;
			else
				Description = "Android Project";

			configuration = Utilities.FirstOf (projectConfig?.Configuration, context.Configuration, Constants.DefaultConfiguration);
			repetitionCount = Utilities.FirstOf (context.RepetitionCount, projectConfigRepetitions, Constants.DefaultRepetitionCount);
			FullProjectFilePath = Path.GetFullPath (projectPath);
			FullProjectDirPath = Path.GetDirectoryName (FullProjectFilePath)!;

			FullBinDirPath = Path.Combine (FullProjectDirPath, "bin");
			FullObjDirPath = Path.Combine (FullProjectDirPath, "obj");

			WhenUTC = DateTime.UtcNow;
			runId = WhenUTC.ToString ("yyyy-MM-dd-HH:mm:ss");
			FullAndroidManifestPath = Path.Combine (FullObjDirPath, configuration, Constants.AndroidManifestRelativePath);

			if (!String.IsNullOrEmpty (projectConfig?.OutputDirectory)) {
				FullDataDirectoryPath = projectConfig.OutputDirectory;
				if (!Path.IsPathRooted (FullDataDirectoryPath)) {
					FullDataDirectoryPath = Path.Combine (Path.GetDirectoryName (projectConfig.ConfigFilePath)!, FullDataDirectoryPath);
				}
				FullDataDirectoryPath = Path.Combine (FullDataDirectoryPath, runId);
			} else if (!String.IsNullOrEmpty (context.OutputDirectory)) {
				FullDataDirectoryPath = Path.Combine (context.OutputDirectory, Path.GetFileNameWithoutExtension (FullProjectFilePath), runId);
			} else {
				FullDataDirectoryPath = Path.Combine (FullProjectDirPath, Constants.DataRelativePath, runId);
			}

			string gitRoot = String.Empty;
			projectUsesGit = UsesGit (FullProjectDirPath, ref gitRoot);
			runs = CreateRunDefinitions ();
		}

		List<RunDefinition> CreateRunDefinitions ()
		{
			var ret = new List<RunDefinition> ();

			if (ProjectConfig != null)
				CreateRunDefinitions (ret, ProjectConfig);
			else
				CreateRunDefinitions (ret);

			return ret;
		}

		void CreateRunDefinitions (List<RunDefinition> rundefs, ProjectConfig projectConfig)
		{
			if (projectConfig.RunDefinitions.Count == 0) {
				CreateRunDefinitions (rundefs);
				return;
			}

			foreach (ProjectConfigSingleRunDefinition runConfig in projectConfig.RunDefinitions) {
				rundefs.Add (new RunDefinition (context, runConfig, projectConfig));
			}
		}

		void CreateRunDefinitions (List<RunDefinition> rundefs)
		{
			rundefs.Add (new RunDefinition (context));
		}

		bool UsesGit (string dir, ref string gitRoot)
		{
			if (String.IsNullOrEmpty (dir))
				return false;

			dir = Path.GetFullPath (dir);
			string dotGit = Path.Combine (dir, ".git");

			// git worktree directories have a ".git" file instead of directory
			if (Directory.Exists (dotGit) || File.Exists (dotGit)) {
				gitRoot = dir;
				return true;
			}

			if (String.Compare (dir, Path.GetPathRoot (dir), StringComparison.OrdinalIgnoreCase) == 0)
				return false;

			return UsesGit (Path.GetFullPath (Path.Combine (dir, "..")), ref gitRoot);
		}

		async Task<(string hash, string branch)> GetCommitHashAndBranch (string dir)
		{
			var git = new GitRunner (context);
			string hash = await git.GetTopCommitHash (dir);
			string branch = await git.GetCurrentBranch (dir);

			return (hash, branch);
		}

		string GetLogBasePath (string subdir, string opName, string logTag, string? hash, string? branch)
		{
			var sb = new StringBuilder (Path.Combine (FullDataDirectoryPath, subdir, opName));
			if (!String.IsNullOrEmpty (hash)) {
				sb.Append ('-');
				sb.Append (hash);
			}

			if (!String.IsNullOrEmpty (branch)) {
				sb.Append ('-');
				sb.Append (branch);
			}

			if (!String.IsNullOrEmpty (logTag)) {
				sb.Append ('-');
				sb.Append (logTag);
			}

			return sb.ToString ();
		}

		async Task<bool> BuildAndInstall (RunDefinition run)
		{
			if (projectUsesGit && projectGitCommit == null) {
				(projectGitCommit, projectGitBranch) = await GetCommitHashAndBranch (FullProjectDirPath);
			}

			var args = new List<string> {
				$"/v:quiet"
			};
			args.AddRange (run.Args);

			var msbuild = new MSBuildRunner (context) {
				WorkingDirectory = FullProjectDirPath,
				EchoStandardOutput = true,
				EchoStandardError = true,
			};

			string projectPath = Path.GetRelativePath (FullProjectDirPath, FullProjectFilePath);
			string binlogBasePath = String.Empty;

			binlogBasePath = GetLogBasePath (Constants.MSBuildLogDir, "restore", run.LogTag, projectGitCommit, projectGitBranch);
			if (!await msbuild.Run (projectPath, binlogBasePath, "Restore", configuration, args))
				return false;

			binlogBasePath = GetLogBasePath (Constants.MSBuildLogDir, "build", run.LogTag, projectGitCommit, projectGitBranch);
			run.BinlogPath = Path.GetRelativePath (FullDataDirectoryPath, $"{binlogBasePath}.binlog");
			if (!await msbuild.Run (projectPath, binlogBasePath, "Install", configuration, args))
				return false;

			if (xaVersionNotDetectedYet) {
				const string NotGit = "not a git build";

				Log.InfoLine ("Retrieving Xamarin.Android version information");
				xaVersion = await GetXAVersion (msbuild, binlogBasePath);
				Log.InfoLine ($"    Location: {xaVersion.RootDir}");
				Log.InfoLine ($"     Version: {xaVersion.Version}");

				string hash = String.IsNullOrEmpty (xaVersion.Commit) ? NotGit : xaVersion.Commit;
				string branch = String.IsNullOrEmpty (xaVersion.Commit) ? NotGit : xaVersion.Branch;
				Log.InfoLine ($"  Git branch: {branch}");
				Log.InfoLine ($"  Git commit: {hash}");
				xaVersionNotDetectedYet = false;
			}

			return true;
		}

		async Task<XAVersionInfo> GetXAVersion (MSBuildRunner msbuild, string binlogBasePath)
		{
			var neededProperties = new HashSet<string> (StringComparer.Ordinal) {
				"TargetFrameworkRootPath",
				"XamarinAndroidVersion",
			};

			Dictionary<string, string> properties = await msbuild.GetPropertiesFromBinlog (binlogBasePath, neededProperties);
			string? propertyValue;
			string rootDir = String.Empty;
			string branch = String.Empty;
			string commit = String.Empty;
			if (properties.TryGetValue ("TargetFrameworkRootPath", out propertyValue) && !String.IsNullOrEmpty (propertyValue)) {
				string gitRoot = String.Empty;
				if (UsesGit (propertyValue, ref gitRoot)) {
					(commit, branch) = await GetCommitHashAndBranch (propertyValue);
					rootDir = gitRoot;
				}
			} else
				rootDir = "system";

			string version = String.Empty;
			if (!properties.TryGetValue("XamarinAndroidVersion", out propertyValue)) {
				version = "unknown";
			} else
				version = propertyValue;

			return new XAVersionInfo (
				version,
				branch,
				commit,
				rootDir
			);
		}

		public async Task<bool> Run ()
		{
			var adb = new AdbRunner (context);
			AndroidDeviceInfo? info = await adb.GetDeviceInfo ();
			if (info == null) {
				Log.FatalLine ("Failed to obtain Android device info");
				return false;
			}
			adi = info;
			Log.InfoLine ($"Device: {adi.Model}");
			Log.InfoLine ($"Device architecture: {adi.Architecture}");
			Log.InfoLine ($"Device SDK: {adi.SdkVersion}");

			if (!await adb.SetPropertyValue ("debug.mono.log", "default,timing=bare")) {
				Log.FatalLine ("Failed to set Mono debugging properties");
				return false;
			}

			if (!await adb.SetLogcatBufferSize ("16M")) {
				Log.WarningLine ("Failed to set logcat buffer size");
			}

			Utilities.CreateDirectory (FullDataDirectoryPath);

			foreach (RunDefinition run in runs) {
				if (run.RunPerformanceTest) {
					if (!await RunPerformanceTest (run, adb)) {
						return false;
					}
				}
			}

			foreach (RunDefinition run in runs) {
				if (run.RunManagedProfiler) {
					if (!await RunManagedProfiler (run)) {
						return false;
					}
				}
			}

			foreach (RunDefinition run in runs) {
				if (run.RunNativeProfiler) {
					if (!await RunNativeProfiler (run)) {
						return false;
					}
				}
			}

			RawResults.Save (adi, this);

			return true;
		}

		async Task<bool> RunPerformanceTest (RunDefinition run, AdbRunner adb)
		{
			Log.MessageLine (run.Description);
			Utilities.DeleteDirectorySilent (FullBinDirPath);
			Utilities.DeleteDirectorySilent (FullObjDirPath);
			if (!await BuildAndInstall (run))
				return false;

			(string packageName, string activityName) = Utilities.GetPackageAndActivityName (
				FullAndroidManifestPath,
				Utilities.FirstOf (context.PackageName, run.PackageName, ProjectConfig?.PackageName)
			);

			for (uint i = 0; i < repetitionCount; i++) {
				uint runNum = i + 1;
				Log.InfoLine ($"[{run.Summary}] run {runNum} of {repetitionCount}");
				if (!await adb.ClearLogcat ()) {
					Log.WarningLine ("Failed to clear logcat buffer");
				}

				Log.InfoLine ($"[{run.Summary}] running application");
				if (!await adb.RunApp (packageName, activityName)) {
					Log.FatalLine ($"[{run.Summary}] application failed");
					return false;
				}

				Log.InfoLine ($"[{run.Summary}] recording statistics");
				string logcatPath = GetLogBasePath (Constants.DeviceLogDir, "logcat", $"{run.LogTag}-{runNum:000}.txt", projectGitCommit, projectGitBranch);
				await adb.DumpLogcatToFile (logcatPath);
				run.Results.Add (GetPerfDataFromLogcat (run, logcatPath, packageName, activityName));
				Log.InfoLine ($"[{run.Summary}] pausing for {Constants.PauseBetweenRunsMS}ms");
				Thread.Sleep (Constants.PauseBetweenRunsMS);
			}
			Log.MessageLine ();

			return true;
		}

		string FormatMilliseconds (decimal nanoseconds)
		{
			const decimal ms_in_nsec = 1000000UL;
			decimal res = (decimal)nanoseconds / ms_in_nsec;

			return res.ToString ("0.000");
		}

		RunResults GetPerfDataFromLogcat (RunDefinition run, string logcatPath, string packageName, string activityName)
		{
			const string NativeToManagedMarker = "Runtime.init: end native-to-managed transition; elapsed:";
			const string TotalInitMarker = "Runtime.init: end, total time; elapsed:";
			string DisplayedMarker = $"ActivityTaskManager: Displayed {packageName}/{activityName}:";

			string? nativeToManaged = null;
			string? totalInit = null;
			string? displayed = null;

			using (var sr = new StreamReader (logcatPath, Encoding.UTF8)) {
				string? line = null;

				while ((line = sr.ReadLine ()) != null) {
					int idx;

					if (nativeToManaged == null && (idx = line.IndexOf (NativeToManagedMarker, StringComparison.Ordinal)) >= 0) {
						nativeToManaged = line.Substring (idx + NativeToManagedMarker.Length).Trim ();
						continue;
					}

					if (totalInit == null && (idx = line.IndexOf (TotalInitMarker, StringComparison.Ordinal)) >= 0) {
						totalInit = line.Substring (idx + TotalInitMarker.Length).Trim ();
						continue;
					}

					if (displayed == null && (idx = line.IndexOf (DisplayedMarker, StringComparison.Ordinal)) >= 0) {
						displayed = line.Substring (idx + DisplayedMarker.Length).Trim ();
						continue;
					}

					if (nativeToManaged != null && totalInit != null && displayed != null)
						break;
				}
			}

			return new RunResults (run) {
				NativeToManaged = ParseXATiming (nativeToManaged),
				TotalInit = ParseXATiming (totalInit),
				Displayed = ParseActivityDisplayed (displayed),
				LogcatPath = Path.GetRelativePath (FullDataDirectoryPath, logcatPath),
			};

			ulong ParseXATiming (string? data)
			{
				data = data?.Trim ();
				if (String.IsNullOrEmpty (data))
				    return 0;

				// Format: 0s:49::833495
				string[] parts = data.Split (TimingSplitChars, StringSplitOptions.RemoveEmptyEntries);
				if (parts.Length < 3) {
					Log.WarningLine ($"Unable to parse '{data}' as Xamarin.Android timing information");
					return 0;
				}

				ulong sec = ParseTimeUnit (parts[0].Trim ('s'), "seconds", 60UL);
				ulong ms = ParseTimeUnit (parts[1], "milliseconds", 1000UL);
				ulong ns = ParseTimeUnit (parts[2], "nanoseconds", 1000000000UL);

				return (sec * 1000000000UL) + (ms * 1000000UL) + ns;
			}

			ulong ParseActivityDisplayed (string? data)
			{
				data = data?.Trim ();
				if (String.IsNullOrEmpty (data)) {
					Log.WarningLine ("Activity Displayed message not found in the log");
					return 0;
				}

				// Format: +1s60ms
				Match match = DisplayedRegex.Match (data);
				if (!match.Success) {
					Log.WarningLine ($"Failed to parse Activity Displayed time from '{data}'");
					return 0;
				}

				ulong sec = ParseTimeUnit (match.Groups[1].Value.TrimEnd ('s'), "seconds", 60UL);
				ulong ms = ParseTimeUnit (match.Groups[2].Value, "milliseconds", 1000UL);

				return (sec * 1000000000UL) + (ms * 1000000UL);
			}

			ulong ParseTimeUnit (string data, string unitName, ulong maxValue)
			{
				if (String.IsNullOrEmpty (data))
					return 0;

				if (!UInt64.TryParse (data, out ulong val)) {
					Log.WarningLine ($"Unable to parse '{data}' as the number of {unitName}");
					return 0;
				}

				if (val > maxValue) {
					Log.WarningLine ($"Value {val} exceeds the maximum value of {maxValue} for {unitName}");
					return 0;
				}

				return val;
			}
		}

		async Task<bool> RunManagedProfiler (RunDefinition run)
		{
			throw new NotImplementedException ();
		}

		async Task<bool> RunNativeProfiler (RunDefinition run)
		{
			throw new NotImplementedException ();
		}
	}
}