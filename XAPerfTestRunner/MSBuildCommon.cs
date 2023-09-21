using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SL = Microsoft.Build.Logging.StructuredLogger;

namespace XAPerfTestRunner
{
	abstract partial class MSBuildCommon : ToolRunner
	{
		const string BuildInfoMarker = "[BuildInfo]:";
		const string TargetFramework = "TargetFramework";
		const string TargetFrameworkField = TargetFramework + "=";
		const string EmptyTargetFrameworkField = TargetFrameworkField + ";";
		const string ObjDir = "ObjDir";
		const string ObjDirField = ObjDir + "=";
		const string OutputDir = "OutputDir";
		const string OutputDirField = OutputDir + "=";

		const string PackageFilename = "PackageFilename";
		const string PackageFilenameField = PackageFilename + "=";

		public List<string> StandardArguments { get; }
		public string WorkingDirectory { get; set; } = String.Empty;

		protected MSBuildCommon (Context context, string? msbuildPath = null)
			: base (context, msbuildPath)
		{
			ProcessTimeout = TimeSpan.FromMinutes (30);
			StandardArguments = new List<string> ();
		}

		string? GetBinLog (string logBasePath)
		{
			string binlogPath = $"{logBasePath}.binlog";
			if (!File.Exists (Path.Combine (WorkingDirectory, binlogPath)))
				return null;
			return binlogPath;
		}

		public async Task<Dictionary<string, BuildInfo>> GetBuildInfo (string logBasePath)
		{
			var ret = new Dictionary<string, BuildInfo> (StringComparer.Ordinal);
			string? binlogPath = GetBinLog (logBasePath);
			if (binlogPath == null) {
				return ret;
			}

			bool echoOutputValue = EchoStandardOutput;
			EchoStandardOutput = false;

			try {
				ProcessRunner runner = CreateMSBuildRunner (forBinlog: true);
				runner.AddArgument ("/v:diag");
				runner.AddQuotedArgument (binlogPath);

				using (var outputSink = (OutputSink)SetupOutputSink (runner)) {
					outputSink.SuppressOutput = true;
					outputSink.LineCallback = (string l) => {
						string line = l.Trim ();
						if (!line.StartsWith (BuildInfoMarker, StringComparison.Ordinal)) {
							return;
						}

						BuildInfo? bi = ExtractBuildInfo (line);
						if (bi == null) {
							return;
						}

						if (ret.ContainsKey (bi.TargetFramework)) {
							throw new InvalidOperationException ($"Duplicated target framework '{bi.TargetFramework}' info");
						}

						ret.Add (bi.TargetFramework, bi);
					};

					if (!await RunMSBuild (runner, setupOutputSink: false))
						return ret;
				}
			} finally {
				EchoStandardOutput = echoOutputValue;
			}

			return ret;
		}

		BuildInfo? ExtractBuildInfo (string line)
		{
			if (line.Length == 0) {
				return null;
			}

			string info = line.Substring (BuildInfoMarker.Length);
			if (info.StartsWith (EmptyTargetFrameworkField, StringComparison.Ordinal)) {
				return null; // Probably outer build with dotnet and a multi-target project
			}

			string? targetFramework = null;
			string? objDir = null;
			string? binDir = null;
			string? packageFilename = null;

			foreach (string field in info.Split (';', StringSplitOptions.RemoveEmptyEntries)) {
				if (field.StartsWith (TargetFrameworkField, StringComparison.Ordinal)) {
					targetFramework = field.Substring (TargetFrameworkField.Length);
					continue;
				}

				if (field.StartsWith (ObjDirField, StringComparison.Ordinal)) {
					objDir = field.Substring (ObjDirField.Length);
					continue;
				}

				if (field.StartsWith (OutputDirField, StringComparison.Ordinal)) {
					binDir = field.Substring (OutputDirField.Length);
					continue;
				}

				if (field.StartsWith (PackageFilenameField, StringComparison.Ordinal)) {
					packageFilename = field.Substring (PackageFilenameField.Length);
					continue;
				}
			}

			ValidateField (targetFramework, TargetFramework);
			ValidateField (objDir, ObjDir);
			ValidateField (binDir, OutputDir);
			ValidateField (packageFilename, PackageFilename);

			return new BuildInfo (targetFramework!, objDir!, binDir!, packageFilename!);

			void ValidateField (string? fieldValue, string fieldName)
			{
				if (String.IsNullOrEmpty (fieldValue)) {
					throw new InvalidOperationException ($"Invalid build info: missing the {fieldName} field");
				}
			}
		}

		public async Task<Dictionary<string, string>> GetPropertiesFromBinlog (string logBasePath, HashSet<string> neededProperties)
		{
			var ret = new Dictionary<string, string> (StringComparer.Ordinal);
			string? binlogPath = GetBinLog (logBasePath);
			if (binlogPath == null) {
				return ret;
			}

			bool echoOutputValue = EchoStandardOutput;
			EchoStandardOutput = false;
			try {
				ProcessRunner runner = CreateMSBuildRunner (forBinlog: true);
				runner.AddArgument ("/v:diag");
				runner.AddQuotedArgument (binlogPath);

				bool propertiesStarted = false;
				int propertiesFound = 0;
				using (var outputSink = (OutputSink)SetupOutputSink (runner)) {
					outputSink.SuppressOutput = true;
					outputSink.LineCallback = (string l) => {
						string line = l.Trim ();
						if (!propertiesStarted) {
							propertiesStarted = line.EndsWith ("Initial Properties:");
							return;
						} else if (propertiesFound == neededProperties.Count)
							return;

						int idx = line.IndexOf ('=');
						if (idx < 0)
							return;

						string propName = line.Substring (0, idx - 1);
						if (!neededProperties.Contains (propName)) {
							return;
						}

						ret [propName] = line.Substring (idx + 1).Trim ();
						propertiesFound++;
					};

					if (!await RunMSBuild (runner, setupOutputSink: false))
						return ret;
				}
			} finally {
				EchoStandardOutput = echoOutputValue;
			}

			return ret;
		}

		protected async Task<bool> RunMSBuild (ProcessRunner runner, bool setupOutputSink = true, bool ignoreStderr = true)
		{
			LogCommandLine (runner);
			return await RunTool (
				() => {
					OutputSink? sink = null;
					if (setupOutputSink) {
						sink = (OutputSink)SetupOutputSink (runner, ignoreStderr: ignoreStderr);
					}

					try {
						return runner.Run ();
					} finally {
						sink?.Dispose ();
					}
				}
			);
		}

		public decimal GetDurationFromBinLog (string logBasePath)
		{
			var build = SL.BinaryLog.ReadBuild ($"{logBasePath}.binlog");
			var duration = build
				.FindChildrenRecursive<SL.Project> ()
				.Aggregate (TimeSpan.Zero, (duration, project) => duration + project.Duration);

			if (duration == TimeSpan.Zero)
				throw new InvalidDataException ($"No project build duration found in {logBasePath}.binlog");

			return (decimal)duration.TotalMilliseconds;
		}

		public decimal GetTaskDurationFromBinLog (string logBasePath, string task)
		{
			var build = SL.BinaryLog.ReadBuild ($"{logBasePath}.binlog");
			var duration = build
				.FindChildrenRecursive<SL.Task> ()
				.LastOrDefault ((t) => t.Name == task, new SL.Task ()).Duration;

			if (duration == TimeSpan.Zero)
				throw new InvalidDataException ($"No task {task} duration found in {logBasePath}.binlog");

			return (decimal)duration.TotalMilliseconds;
		}

		protected override TextWriter CreateLogSink (string? logFilePath)
		{
			return new OutputSink (logFilePath);
		}

		protected abstract ProcessRunner CreateMSBuildRunner (bool forBinlog);
	}
}
