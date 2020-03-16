using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace XAPerfTestRunner
{
	partial class GitRunner : ToolRunner
	{
		// These are passed to `git` itself *before* the command
		static readonly List<string> standardGlobalOptions = new List<string> {
			"--no-pager"
		};

		protected override string DefaultToolExecutableName => "git";
		protected override string ToolName                  => "Git";

		public GitRunner (Context context, string? gitPath = null)
			: base (context, gitPath)
		{
			ProcessTimeout = TimeSpan.FromMinutes (30);
		}

		public async Task<string> GetTopCommitHash (string workingDirectory)
		{
			var runner = CreateGitRunner (workingDirectory);
			runner.AddArgument ("rev-parse");
			runner.AddArgument ("HEAD");

			string hash = String.Empty;
			using (var outputSink = (OutputSink)SetupOutputSink (runner)) {
				outputSink.LineCallback = (string line) => {
					if (!String.IsNullOrEmpty (hash))
						return;
					hash = line.Trim ();
				};

				if (!await RunGit (runner))
					return String.Empty;

				return hash;
			}
		}

		public async Task<string> GetCurrentBranch (string workingDirectory)
		{
			var runner = CreateGitRunner (workingDirectory);
			runner.AddArgument ("branch");
			runner.AddArgument ("--show-current");

			string branch = String.Empty;
			using (var outputSink = (OutputSink)SetupOutputSink (runner)) {
				outputSink.LineCallback = (string line) => {
					if (!String.IsNullOrEmpty (branch))
						return;
					branch = line.Trim ();
				};

				if (!await RunGit (runner))
					return String.Empty;

				return branch;
			}
		}

		ProcessRunner CreateGitRunner (string workingDirectory, List<string>? arguments = null)
		{
			var runner = CreateProcessRunner ();
			runner.WorkingDirectory = workingDirectory;
			SetGitArguments (runner, workingDirectory, arguments);

			return runner;
		}

		async Task<bool> RunGit (ProcessRunner runner)
		{
			return await RunTool (
				() => {
					using (var outputSink = (OutputSink)SetupOutputSink (runner)) {
						return runner.Run ();
					}
				}
			);
		}

		protected override TextWriter CreateLogSink (string? logFilePath)
		{
			return new OutputSink (logFilePath);
		}

		void SetCommandArguments (ProcessRunner runner, string command, List<string>? commandArguments)
		{
			runner.AddArgument (command);
			if (commandArguments == null || commandArguments.Count == 0)
				return;
			AddArguments (runner, commandArguments);
		}

		void SetGitArguments (ProcessRunner runner, string? workingDirectory, List<string>? gitArguments)
		{
			foreach (string arg in standardGlobalOptions) {
				runner.AddArgument (arg);
			}

			if (!String.IsNullOrEmpty (workingDirectory)) {
				runner.AddArgument ("-C");
				runner.AddQuotedArgument (workingDirectory!);
			}

			AddArguments (runner, gitArguments);
		}
	}
}
