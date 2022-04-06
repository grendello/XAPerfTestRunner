using System.Collections.Generic;
using System.Threading.Tasks;

namespace XAPerfTestRunner
{
	partial class DotnetRunner : MSBuildCommon
	{
		protected override string DefaultToolExecutableName => "dotnet";
		protected override string ToolName                  => "dotnet";

		public DotnetRunner (Context context, string? msbuildPath = null)
			: base (context, msbuildPath)
		{}

		public async Task<bool> Install (string projectPath, string logBasePath, string targetFramework, string? configuration, List<string>? arguments = null)
		{
			ProcessRunner runner = CreateMSBuildRunner (forBinlog: false);
			string cfg = Utilities.FirstOf (configuration, Context.Configuration, Constants.DefaultConfiguration);
			runner
				.AddArgument ("build")
				.AddArgument ("-f")
				.AddQuotedArgument (targetFramework)
				.AddArgument ("--no-restore")
				.AddArgument ("--configuration")
				.AddQuotedArgument (cfg)
				.AddQuotedArgument ($"/bl:{logBasePath}.binlog")
				.AddQuotedArgument ("-t:Install");

			AddArguments (runner, arguments);
			runner.AddQuotedArgument (projectPath);

			return await RunMSBuild (runner);
		}

		public async Task<bool> Build (string projectPath, string logBasePath, string? configuration, List<string>? arguments = null)
		{
			ProcessRunner runner = CreateMSBuildRunner (forBinlog: false);
			string cfg = Utilities.FirstOf (configuration, Context.Configuration, Constants.DefaultConfiguration);
			runner
				.AddArgument ("build")
				.AddArgument ("--configuration")
				.AddQuotedArgument (cfg)
				.AddQuotedArgument ($"/bl:{logBasePath}.binlog");

			AddArguments (runner, arguments);
			runner.AddQuotedArgument (projectPath);

			return await RunMSBuild (runner);
		}

		protected override ProcessRunner CreateMSBuildRunner (bool forBinlog)
		{
			ProcessRunner runner = CreateProcessRunner ();

			if (forBinlog) {
				runner.AddArgument ("msbuild");
			}

			runner.WorkingDirectory = WorkingDirectory;

			AddArguments (runner, StandardArguments);

			return runner;
		}
	}
}
