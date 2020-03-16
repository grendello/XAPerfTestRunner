using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace XAPerfTestRunner
{
	abstract partial class ToolRunner
	{
		static readonly TimeSpan DefaultProcessTimeout = TimeSpan.FromMinutes (15);

		protected const ConsoleColor CommandMessageColor = ConsoleColor.White;

		protected abstract string DefaultToolExecutableName { get; }
		protected abstract string ToolName                  { get; }

		protected Context Context              { get; }
		public string FullToolPath             { get; }

		public bool EchoCmdAndArguments        { get; set; } = true;
		public bool EchoStandardError          { get; set; } = true;
		public bool EchoStandardOutput         { get; set; }
		public string LogMessageIndent         { get; set; } = String.Empty;
		public virtual TimeSpan ProcessTimeout { get; set; } = DefaultProcessTimeout;

		protected ToolRunner (Context context, string? toolPath = null)
		{
			Context = context;

			if (String.IsNullOrEmpty (toolPath)) {
				Log.DebugLine ($"Locating {ToolName} executable '{DefaultToolExecutableName}'");
				FullToolPath = Utilities.Which (DefaultToolExecutableName);
			} else {
				Log.DebugLine ($"Custom {ToolName} path: {toolPath}");
				if (toolPath!.IndexOf (Path.DirectorySeparatorChar) < 0) {
					Log.DebugLine ($"Locating custom {ToolName} executable '{toolPath}'");
					FullToolPath = Utilities.Which (toolPath);
				} else if (Path.IsPathRooted (toolPath)) {
					Log.DebugLine ($"{ToolName} executable path is rooted, using verbatim");
					FullToolPath = toolPath;
				} else {
					Log.DebugLine ($"{ToolName} executable path is a relative to the current directory");
					FullToolPath = Path.Combine (Environment.CurrentDirectory, toolPath);
				}
			}

			if (String.IsNullOrEmpty (FullToolPath))
				throw new InvalidOperationException ($"{ToolName} executable path must be specified");

			Log.DebugLine ($"Full {ToolName} executable path value: {FullToolPath}");
			if (!File.Exists (FullToolPath))
				throw new InvalidOperationException ($"{ToolName} executable '{FullToolPath}' not found");
		}

		protected virtual string GetLogMessage (ProcessRunner runner)
		{
			string message = $"{LogMessageIndent}{Path.GetFileName (FullToolPath)}";
			string formattedArguments = runner.Arguments;
			if (!String.IsNullOrEmpty (formattedArguments))
				message = $"{message} {runner.Arguments}";

			return $"{message} ";
		}

		protected void AddArguments (ProcessRunner runner, IEnumerable<string>? arguments)
		{
			if (arguments == null)
				return;

			foreach (string a in arguments) {
				string arg = a.Trim ();
				if (String.IsNullOrEmpty (arg))
					continue;

				runner.AddQuotedArgument (arg);
			}
		}

		protected virtual ProcessRunner CreateProcessRunner (params string[] initialParams)
		{
			bool usingManagedRunner;
			string command = Utilities.GetManagedProgramRunner (FullToolPath);
			if (command.Length > 0) {
				command = Utilities.Which (command);
				usingManagedRunner = true;
			} else {
				command = FullToolPath;
				usingManagedRunner = false;
			}

			var runner = new ProcessRunner (command) {
				ProcessTimeout = ProcessTimeout,
				EchoCmdAndArguments = EchoCmdAndArguments,
				EchoStandardError = EchoStandardError,
				EchoStandardOutput = EchoStandardOutput,
			};

			if (usingManagedRunner)
				runner.AddQuotedArgument (FullToolPath);

			runner.AddArguments (initialParams);
			return runner;
		}

		protected virtual async Task<bool> RunTool (Func<bool> runner)
		{
			return await Task.Run (runner);
		}

		protected TextWriter SetupOutputSink (ProcessRunner runner, string? tags = null, string? messagePrefix = null, bool ignoreStderr = false)
		{
			string? logFilePath = null;

			// if (!String.IsNullOrEmpty (tags)) {
			// 	logFilePath = Context.GetLogFilePath (tags ?? String.Empty);
			// 	if (String.IsNullOrEmpty (messagePrefix))
			// 		messagePrefix = "running";
			// 	Log.MessageLine ($"{LogMessageIndent}[{ToolName}] {messagePrefix}");
			// 	Log.MessageLine ($"[{ToolName}] log file: ", $"{Utilities.GetRelativePath (BuildPaths.XamarinAndroidSourceRoot, logFilePath)}", tailColor: Log.DestinationColor);
			// }

			TextWriter ret = CreateLogSink (logFilePath);

			if (!ignoreStderr)
				runner.AddStandardErrorSink (ret);
			runner.AddStandardOutputSink (ret);

			return ret;
		}

		protected virtual TextWriter CreateLogSink (string? logFilePath)
		{
			throw new NotSupportedException ("Child class must implement this method if it uses GetLogFileSink");
		}
	}
}
