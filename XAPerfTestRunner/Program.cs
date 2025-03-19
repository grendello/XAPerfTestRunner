using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Mono.Options;

namespace XAPerfTestRunner
{
	class Program
	{
		static async Task<int> Main (string [] args)
		{
			var parsedOptions = new ParsedOptions ();

			var opts = new OptionSet {
				"Usage: xaptr [OPTIONS] [(path/to/project.csproj | path/to/project/directory | path/to/project/perfdata)]...",
				"",
				"The application must be run in a directory which contains the .csproj file for the application to test, or a path to the project file must be passed as a parameter.",
				"Only Xamarin.Android projects are supported.",
				"",
				{"d|compare", "Compare two sets of performance data and generate a report. All data directory paths must be given on command line.", v => parsedOptions.Compare = v == null ? false : true},
				{"p|perf", $"Run performance test (default: {parsedOptions.RunPerfTest})", v => parsedOptions.RunPerfTest = v == null ? false : true},
				{"m|profile-managed", $"Profile managed portion of the app (default: {parsedOptions.RunManagedProfiler})", v => parsedOptions.RunManagedProfiler = v == null ? false : true},
				{"n|profile-native", $"Profile native portion of the app (default: {parsedOptions.RunNativeProfiler})", v => parsedOptions.RunNativeProfiler = v == null ? false : true},
				{"r|runs=", $"Number of runs for the performance test (default: {parsedOptions.RepetitionCount})", v => parsedOptions.RepetitionCount = uint.Parse (v)},
				{"f|fast-timing", $"Enable fast timing mode in Xamarin.Android, requires commit 1efa0cf46c9079fc06669a4434f597faf47504af, enabled by default", v => parsedOptions.UseFastTiming = v == null ? false : true},
				{"no-fast-timing", $"Disable fast timing mode in Xamarin.Android", v => parsedOptions.UseFastTiming = false},
				"",
				{"a|app=", "Use the specified Android app id/package name (default is to autodetect)", v => parsedOptions.PackageName = v},
				{"c|configuration=", $"Build application in the specified CONFIGURATION (default: {parsedOptions.Configuration})", v => parsedOptions.Configuration = v ?? parsedOptions.Configuration},
				{"b|build-command=", $"Use COMMAND to build the package (default: {parsedOptions.BuildCommand})", v => parsedOptions.BuildCommand = v ?? parsedOptions.BuildCommand},
				"",
				{"x|config-file=", "Pass a PATH to an xaptr configuration file", v => parsedOptions.ConfigFile = v},
				{"o|output-dir=", $"Path to base directory where to store the data and report files", v => parsedOptions.OutputDirectory = v},
				"",
				{"h|help", "Show this help message", v => parsedOptions.ShowHelp = true},
			};

			List<string> rest = opts.Parse (args);
			if (parsedOptions.ShowHelp) {
				opts.WriteOptionDescriptions (Console.Out);
				return 0;
			}

			var context = new Context {
				BuildCommand = parsedOptions.BuildCommand,
				RunNativeProfiler = parsedOptions.RunNativeProfiler,
				RunManagedProfiler = parsedOptions.RunManagedProfiler,
				RunPerformanceTest = parsedOptions.RunPerfTest,
			};

			if (!String.IsNullOrEmpty (parsedOptions.Configuration))
				context.Configuration = parsedOptions.Configuration;

			if (!String.IsNullOrEmpty (parsedOptions.PackageName))
				context.PackageName = parsedOptions.PackageName;

			if (parsedOptions.RepetitionCount.HasValue)
				context.RepetitionCount = parsedOptions.RepetitionCount.Value;

			if (!String.IsNullOrEmpty (parsedOptions.OutputDirectory))
				context.OutputDirectory = parsedOptions.OutputDirectory;

			bool result = true;
			if (parsedOptions.Compare)
				result = CompareReports (context, parsedOptions, rest);
			else
				result = await RunPerfTests (context, parsedOptions, rest);

			return result ? 0 : 1;
		}

		static bool CompareReports (Context context, ParsedOptions parsedOptions, List<string> rest)
		{
			if (rest.Count < 2) {
				Log.FatalLine ("Paths to two directories containing performance reports must be passed on command line.");
				return false;
			}

			string pathOne = Path.GetFullPath (rest [0]);
			string pathTwo = Path.GetFullPath (rest [1]);

			if (String.Compare (pathOne, pathTwo, StringComparison.Ordinal) == 0) {
				Log.FatalLine ("Performance data paths must point to different directories");
				return false;
			}

			bool success;
			string perfDataOne;
			(success, perfDataOne) = EnsureDirExistsAndHasPerfData (pathOne, rest [0]);
			if (!success)
				return false;

			string perfDataTwo;
			(success, perfDataTwo) = EnsureDirExistsAndHasPerfData (pathTwo, rest [1]);
			if (!success)
				return false;

			var report = new Report ();
			string reportFile = report.Compare (context, Utilities.FirstOf (context.OutputDirectory, Constants.CompareResultsRelativePath), perfDataOne, perfDataTwo);

			return true;

			(bool success, string perfDataPath) EnsureDirExistsAndHasPerfData (string path, string originalPath)
			{
				if (!Directory.Exists (path)) {
					Log.FatalLine ($"Directory '{originalPath}' does not exist.");
					return (false, String.Empty);
				}

				string perfData = Path.Combine (path, Constants.RawResultsFileName);
				if (!File.Exists (perfData)) {
					Log.FatalLine ($"Raw performance data file not found at '{perfData}'");
					return (false, String.Empty);
				}

				return (true, perfData);
			}
		}

		static async Task<bool> RunPerfTests (Context context, ParsedOptions parsedOptions, List<string> rest)
		{
			context.UseFastTiming = parsedOptions.UseFastTiming;
			if (context.UseFastTiming) {
				Log.InfoLine ("Running application with fast timing support enabled");
			}

			var locator = new ProjectLocator (parsedOptions, rest);
			string xaProjectPath = locator.ProjectPaths.Count == 0 ? String.Empty : locator.ProjectPaths [0];
			if (String.IsNullOrEmpty (xaProjectPath)) {
				Log.FatalLine ("No Xamarin.Android project found or specified on command line");
				return false;
			}

			var project = new Project (context, xaProjectPath, locator.ProjectConfig);
			Log.LogFilePath = Path.Combine (project.FullDataDirectoryPath, Constants.LogFileName);
			Log.InfoLine ();
			Log.InfoLabeled ("Using Xamarin.Android project", project.FullProjectFilePath);

			bool result = await project.Run ();
			if (!result) {
				Log.FatalLine ("Run failed");
				return false;
			}

			var report = new Report ();
			string reportPath = report.Generate (project);

			Log.InfoLine ();
			Log.InfoLabeled ("Project", project.FullProjectFilePath);
			Log.InfoLabeled ("Configuration", project.Configuration);
			Log.InfoLabeled ("Output directory", project.FullDataDirectoryPath);
			if (!String.IsNullOrEmpty (reportPath)) {
				Log.InfoLabeled ("Results report", reportPath);
				return true;
			}

			Log.InfoLine ();
			Log.WarningLine ("No results report generated");
			return false;
		}

		static bool ParseBoolean (string? value, bool defaultValue = false)
		{
			string? v = value?.Trim ();
			if (String.IsNullOrEmpty (v))
				return defaultValue;

			bool ret = false;
			if (Utilities.ParseBoolean (value, ref ret))
				return ret;

			throw new InvalidOperationException ($"Unknown boolean value: {value}");
		}
	}
}
