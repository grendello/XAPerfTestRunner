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
		static readonly Regex DisplayedRegex = new Regex ("^[\\s\\d\\w]*?:\\s\\+(?<val>\\d+s)?(?<ms>\\d+)ms", RegexOptions.Compiled);

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
		public string FullDataDirectoryPath { get; }
		public string FullBinDirPath { get; }
		public string FullObjDirPath { get; }
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

			configuration = Utilities.FirstOf (context.Configuration, projectConfig?.Configuration, Constants.DefaultConfiguration);
			repetitionCount = Utilities.FirstOf (context.RepetitionCount, projectConfigRepetitions, Constants.DefaultRepetitionCount);
			FullProjectFilePath = Path.GetFullPath (projectPath);
			FullProjectDirPath = Path.GetDirectoryName (FullProjectFilePath)!;

			FullBinDirPath = Path.Combine (FullProjectDirPath, "bin");
			FullObjDirPath = Path.Combine (FullProjectDirPath, "obj");

			WhenUTC = DateTime.UtcNow;
			runId = WhenUTC.ToString ("yyyy-MM-dd-HH:mm:ss");

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

		BuildInfo FindFirstAndroidBuildInfo (Dictionary<string, BuildInfo> buildInfos)
		{
			if (buildInfos == null || buildInfos.Count == 0) {
				throw new InvalidOperationException ($"Missing build info for project {FullProjectFilePath}. Does the project have the correct targets?");
			}

			BuildInfo? androidInfo = null;
			foreach (var kvp in buildInfos) {
				string tfm = kvp.Key;
				BuildInfo info = kvp.Value;

				if (String.Compare ("legacy", tfm, StringComparison.Ordinal) == 0) {
					androidInfo = info;
					break;
				}

				// Format: netX.Y-android
				Match match = Utilities.AndroidTFM.Match (tfm);
				if (!match.Success) {
					continue;
				}

				androidInfo = info;
				break;
			}

			if (androidInfo == null) {
				throw new InvalidOperationException ($"Unable to find Android build info for project {FullProjectFilePath}. Does the project have the correct targets?");
			}

			return androidInfo;
		}

		async Task<(bool, BuildInfo?)> BuildAndInstall (RunDefinition run)
		{
			if (projectUsesGit && projectGitCommit == null) {
				(projectGitCommit, projectGitBranch) = await GetCommitHashAndBranch (FullProjectDirPath);
			}

			string buildCommand = run.BuildCommand;
			bool usesDotnet = Path.GetFileName (buildCommand).StartsWith ("dotnet", StringComparison.OrdinalIgnoreCase);

			var args = new List<string> {
				"-v:quiet"
			};
			if (context.UseFastTiming) {
				args.Add ("-p:_AndroidFastTiming=true");
			}

			args.AddRange (run.Args);

			string projectPath = Path.GetRelativePath (FullProjectDirPath, FullProjectFilePath);
			string binlogBasePath = String.Empty;
			MSBuildCommon builder;
			BuildInfo buildInfo;

			if (!usesDotnet) {
				var msbuild = ConfigureRunner (new MSBuildRunner (context, buildCommand));
				binlogBasePath = GetBinlogBasePath ("restore");
				if (!await msbuild.Run (projectPath, binlogBasePath, "Restore", configuration, args)) {
					return (false, null);
				}

				binlogBasePath = GetBinlogBasePath ("build");
				run.BinlogPath = GetRelativeBinlogPath (binlogBasePath);

				if (!await msbuild.Run (projectPath, binlogBasePath, "SignAndroidPackage", configuration, args)) {
					return (false, null);
				}

				buildInfo = FindFirstAndroidBuildInfo (await msbuild.GetBuildInfo (binlogBasePath));
				await Uninstall (buildInfo);

				binlogBasePath = GetBinlogBasePath ("install");
				if (!await msbuild.Run (projectPath, binlogBasePath, "Install", configuration, args)) {
					return (false, null);
				}

				builder = msbuild;
			} else {
				var dotnet = ConfigureRunner (new DotnetRunner (context, buildCommand));
				binlogBasePath = GetBinlogBasePath ("build");
				run.BinlogPath = GetRelativeBinlogPath (binlogBasePath);
				if (!await dotnet.Build (projectPath, binlogBasePath, configuration, args)) {
					return (false, null);
				}

				buildInfo = FindFirstAndroidBuildInfo (await dotnet.GetBuildInfo (binlogBasePath));
				await Uninstall (buildInfo);
				binlogBasePath = GetBinlogBasePath ("install");
				if (!await dotnet.Install (projectPath, binlogBasePath, buildInfo.TargetFramework, configuration, args)) {
					return (false, null);
				}

				builder = dotnet;
			}

			if (xaVersionNotDetectedYet) {
				const string NotGit = "not a git build";

				Log.BannerLine ("Retrieving Xamarin.Android version information");
				xaVersion = await GetXAVersion (builder, binlogBasePath);
				Log.InfoLabeled ("    Location", xaVersion.RootDir);
				Log.InfoLabeled ("     Version", xaVersion.Version);

				string hash = String.IsNullOrEmpty (xaVersion.Commit) ? NotGit : xaVersion.Commit;
				string branch = String.IsNullOrEmpty (xaVersion.Commit) ? NotGit : xaVersion.Branch;
				Log.InfoLabeled ("  Git branch", branch);
				Log.InfoLabeled ("  Git commit", hash);
				xaVersionNotDetectedYet = false;
			}

			return (true, buildInfo);

			async Task<bool> Uninstall (BuildInfo buildInfo)
			{
				(string packageName, _) = GetPackageAndMainActivityNames (buildInfo, run);

				var adb = new AdbRunner (context);
				return await adb.Uninstall (packageName);
			}

			T ConfigureRunner<T> (T runner) where T: MSBuildCommon
			{
				runner.WorkingDirectory = FullProjectDirPath;
				runner.EchoStandardOutput = true;
				runner.EchoStandardError = true;
				return runner;
			}

			string GetBinlogBasePath (string phase)
			{
				return GetLogBasePath (Constants.MSBuildLogDir, phase, run.LogTag, projectGitCommit, projectGitBranch);
			}

			string GetRelativeBinlogPath (string binlogBasePath)
			{
				return Path.GetRelativePath (FullDataDirectoryPath, $"{binlogBasePath}.binlog");
			}
		}

		async Task<XAVersionInfo> GetXAVersion (MSBuildCommon msbuild, string binlogBasePath)
		{
			var neededProperties = new HashSet<string> (StringComparer.Ordinal) {
				"TargetFrameworkRootPath",
				"XamarinAnalysisTargetsFile",
				"XamarinAndroidVersion",
			};

			Dictionary<string, string> properties = await msbuild.GetPropertiesFromBinlog (binlogBasePath, neededProperties);
			string? propertyValue;
			string rootDir = String.Empty;
			string branch = String.Empty;
			string commit = String.Empty;
			string gitRoot = String.Empty;

			if (properties.TryGetValue ("TargetFrameworkRootPath", out propertyValue) && !String.IsNullOrEmpty (propertyValue)) {
				await GetGitInfo (propertyValue);
			} else if (properties.TryGetValue ("XamarinAnalysisTargetsFile", out propertyValue) && !String.IsNullOrEmpty (propertyValue)) {
				string toolsDir = Path.GetDirectoryName (propertyValue) ?? String.Empty;
				if (!await GetGitInfo (toolsDir)) {
					TryGetInfoFromNuget (toolsDir);
				}
			}

			if (String.IsNullOrEmpty (rootDir)) {
				rootDir = "system";
			}

			string version = String.Empty;
			if (!properties.TryGetValue ("XamarinAndroidVersion", out propertyValue)) {
				version = "unknown";
			} else
				version = propertyValue;

			return new XAVersionInfo (
				version,
				branch,
				commit,
				rootDir
			);

			async Task<bool> GetGitInfo (string dir)
			{
				if (!UsesGit (dir, ref gitRoot)) {
					return false;
				}

				(commit, branch) = await GetCommitHashAndBranch (propertyValue);
				rootDir = gitRoot;
				return true;
			}

			void TryGetInfoFromNuget (string dir)
			{
				string versionCommitPath = Path.Combine (dir, "Version.commit");
				if (!File.Exists (versionCommitPath)) {
					return;
				}

				rootDir = "nuget";
				string[] lines = File.ReadAllLines (versionCommitPath);
				if (lines.Length == 0) {
					return;
				}

				string[] parts = lines[0].Trim ().Split ('/', StringSplitOptions.RemoveEmptyEntries);
				if (parts.Length < 3) {
					return;
				}

				branch = parts[1];
				commit = parts[2];
			}
		}

		public async Task<bool> Run ()
		{
			var adb = new AdbRunner (context);
			(bool gotDebugMonoLogValue, string debugMonoLogValue) = await adb.GetPropertyValue ("debug.mono.log");
			(bool gotWindowAnimationScaleValue, string windowAnimationScaleValue) = await adb.GetGlobalSettingValue ("window_animation_scale");
			(bool gotTransitionAnimationScale, string transitionAnimationScaleValue) = await adb.GetGlobalSettingValue ("transition_animation_scale");
			(bool gotAnimatorDurationScale, string animatorDurationScaleValue) = await adb.GetGlobalSettingValue ("animator_duration_scale");

			if (ProjectConfig != null) {
				string packagesDir = ProjectConfig.PackagesDir;
				if (!String.IsNullOrEmpty (packagesDir)) {
					if (!Path.IsPathRooted (packagesDir)) {
						packagesDir = Path.Combine (Path.GetDirectoryName (ProjectConfig.ConfigFilePath)!, packagesDir);
					}

					if (Directory.Exists (packagesDir)) {
						Log.InfoLine ($"Clearing NuGet packages cache: {packagesDir}");
						Directory.Delete (packagesDir, true);
					}
				}
			}

			try {
				return await Run (adb);
			} finally {
				if (gotDebugMonoLogValue) {
					await adb.SetPropertyValue ("debug.mono.log", debugMonoLogValue);
				}

				if (gotWindowAnimationScaleValue) {
					await SetGlobalSetting (adb, "window_animation_scale", windowAnimationScaleValue);
				}

				if (gotTransitionAnimationScale) {
					await SetGlobalSetting (adb, "transition_animation_scale", transitionAnimationScaleValue);
				}

				if (gotAnimatorDurationScale) {
					await SetGlobalSetting (adb, "animator_duration_scale", animatorDurationScaleValue);
				}
			}
		}

		async Task<bool> SetGlobalSetting (AdbRunner adb, string settingName, string settingValue)
		{
			if (!await adb.SetGlobalSettingValue (settingName, settingValue)) {
				Log.WarningLine ($"Failed to set global setting '{settingName}' value");
				return false;
			}

			return true;
		}

		async Task<bool> Run (AdbRunner adb)
		{
			AndroidDeviceInfo? info = await adb.GetDeviceInfo ();
			if (info == null) {
				Log.FatalLine ("Failed to obtain Android device info");
				return false;
			}
			adi = info;
			Log.InfoLabeled ("Device", adi.Model);
			Log.InfoLabeled ("Device architecture", adi.Architecture);
			Log.InfoLabeled ("Device SDK", adi.SdkVersion);

			string timingMode = context.UseFastTiming ? "fast-bare" : "bare";
			if (!await adb.SetPropertyValue ("debug.mono.log", $"default,timing={timingMode}")) {
				Log.FatalLine ("Failed to set Mono debugging properties");
				return false;
			}

			await SetGlobalSetting (adb, "window_animation_scale", "0");
			await SetGlobalSetting (adb, "transition_animation_scale", "0");
			await SetGlobalSetting (adb, "animator_duration_scale", "0");

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

		(string packageName, string activityName) GetPackageAndMainActivityNames (BuildInfo androidInfo, RunDefinition run)
		{
			string androidManifestPath = Path.Combine (FullProjectDirPath, androidInfo.ObjDir, "android", "AndroidManifest.xml");
			return Utilities.GetPackageAndActivityName (
				androidManifestPath,
				Utilities.FirstOf (context.PackageName, run.PackageName, ProjectConfig?.PackageName)
			);
		}

		string? SavePackage (BuildInfo androidInfo, string outputPath)
		{
			var package = Path.Combine (FullProjectDirPath, androidInfo.BinDir, androidInfo.PackageFilename);
			Log.DebugLine ($"Checking for {package}");
			if (!File.Exists (package)) {
				return null;
			}

			Log.MessageLine ($"Backing up {package} to {outputPath}");
			Directory.CreateDirectory (Path.GetDirectoryName (outputPath!)!);
			File.Copy (package, outputPath);
			return Path.GetRelativePath(FullDataDirectoryPath, outputPath);
		}

		async Task<bool> RunPerformanceTest (RunDefinition run, AdbRunner adb)
		{
			Log.MessageLine ();
			Log.BannerLine (run.Description);

			Utilities.DeleteDirectorySilent (FullBinDirPath);
			Utilities.DeleteDirectorySilent (FullObjDirPath);

			(bool success, BuildInfo? androidInfo) = await BuildAndInstall (run);
			if (!success) {
				return false;
			}

			if (androidInfo == null) {
				throw new InvalidOperationException ($"Unable to find Android build info for project {FullProjectFilePath}. Does the project have the correct targets?");
			}

			(string packageName, string activityName) = GetPackageAndMainActivityNames (androidInfo, run);

			Log.InfoLine ();
			Log.InfoLine ($"[{run.Summary}] precompiling Java bits of the application");
			if (!await adb.CompileForSpeed (packageName)) {
				Log.WarningLine ("Precompilation failed, continuing regardless");
			}

			string apkPath = GetLogBasePath (Constants.ApkDir, "package", $"{run.LogTag}{Path.GetExtension (androidInfo.PackageFilename)}", projectGitCommit, projectGitBranch);
			run.PackagePath = SavePackage (androidInfo, apkPath);

			for (uint i = 0; i < repetitionCount; i++) {
				uint runNum = i + 1;
				Log.InfoLine ($"  run {runNum} of {repetitionCount}");
				if (!await adb.ClearLogcat ()) {
					Log.WarningLine ("Failed to clear logcat buffer");
				}

				Log.MessageLine ($"    running application");
				if (!await adb.RunApp (packageName, activityName)) {
					Log.FatalLine ($"[{run.Summary}] application failed");
					return false;
				}

				if (context.UseFastTiming) {
					Log.MessageLine ($"    asking Xamarin.Android to dump timing data");
					if (!await adb.SendBroadcastIntent (packageName, "mono.android.app.DUMP_TIMING_DATA")) {
						Log.WarningLine ("Failed to send timing dump broadcast");
					}
				}

				Log.MessageLine ($"    pausing for {Constants.PauseBetweenRunsMS}ms");
				Thread.Sleep (Constants.PauseBetweenRunsMS);

				Log.MessageLine ($"    recording statistics");
				string logcatPath = GetLogBasePath (Constants.DeviceLogDir, "logcat", $"{run.LogTag}-{runNum:000}.txt", projectGitCommit, projectGitBranch);
				await adb.DumpLogcatToFile (logcatPath);
				run.Results.Add (GetPerfDataFromLogcat (run, logcatPath, packageName, activityName));

				Log.MessageLine ($"    forcibly stopping application");
				if (!await adb.ForceStop (packageName)) {
					Log.WarningLine ("Failed to forcibly stop application");
				}

				Log.MessageLine ($"    killing all app's background processes");
				if (!await adb.Kill (packageName)) {
					Log.WarningLine ("Failed to kill background processes");
				}
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
			string DisplayedMarker = $"ActivityTaskManager: Displayed {packageName}/{activityName}";

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
