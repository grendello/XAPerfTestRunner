using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace XAPerfTestRunner
{
	partial class MSBuildRunner : ToolRunner
	{
		protected override string DefaultToolExecutableName => Context.BuildCommand;
		protected override string ToolName                  => "MSBuild";

		public List<string> StandardArguments { get; }
		public string WorkingDirectory { get; set; } = String.Empty;

		public MSBuildRunner (Context context, string? msbuildPath = null)
			: base (context, msbuildPath)
		{
			ProcessTimeout = TimeSpan.FromMinutes (30);
			StandardArguments = new List<string> ();
		}

		public async Task<bool> Run (string projectPath, string logBasePath, string target, List<string>? arguments = null)
		{
			return await Run (projectPath, logBasePath, target, null, arguments);
		}

		public async Task<bool> Run (string projectPath, string logBasePath, string target, string? configuration, List<string>? arguments = null)
		{
			ProcessRunner runner = CreateMSBuildRunner ();
			string cfg = Utilities.FirstOf (configuration, Context.Configuration, Constants.DefaultConfiguration);
			runner.AddQuotedArgument ($"/p:Configuration={cfg}");
			runner.AddQuotedArgument ($"/bl:{logBasePath}.binlog");
			runner.AddQuotedArgument ($"/t:{target}");
			AddArguments (runner, arguments);
			runner.AddQuotedArgument (projectPath);

			string message = GetLogMessage (runner);
			Log.InfoLine (message);

			return await RunMSBuild (runner);
		}

		public async Task<Dictionary<string, string>> GetPropertiesFromBinlog (string logBasePath, HashSet<string> neededProperties)
		{
			var ret = new Dictionary<string, string> (StringComparer.Ordinal);
			string binlogPath = $"{logBasePath}.binlog";
			if (!File.Exists (Path.Combine (WorkingDirectory, binlogPath)))
				return ret;

			bool echoOutputValue = EchoStandardOutput;
			EchoStandardOutput = false;
			try {
				ProcessRunner runner = CreateMSBuildRunner ();
				runner.AddArgument ("/v:diag");
				runner.AddQuotedArgument (binlogPath);

				bool propertiesStarted = false;
				int propertiesFound = 0;
				using (var outputSink = (OutputSink)SetupOutputSink (runner)) {
					outputSink.SuppressOutput = true;
					outputSink.LineCallback = (string line) => {
						if (!propertiesStarted) {
							propertiesStarted = line.StartsWith ("Initial Properties:");
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

		async Task<bool> RunMSBuild (ProcessRunner runner, bool setupOutputSink = true, bool ignoreStderr = true)
		{
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

		ProcessRunner CreateMSBuildRunner ()
		{
			ProcessRunner runner = CreateProcessRunner ();
			runner.WorkingDirectory = WorkingDirectory;

			AddArguments (runner, StandardArguments);

			return runner;
		}

		protected override TextWriter CreateLogSink (string? logFilePath)
		{
			return new OutputSink (logFilePath);
		}
	}
}
