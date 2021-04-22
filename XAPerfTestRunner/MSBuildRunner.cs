using System.Collections.Generic;
using System.Threading.Tasks;

namespace XAPerfTestRunner
{
	partial class MSBuildRunner : MSBuildCommon
	{
		protected override string DefaultToolExecutableName => Context.BuildCommand;
		protected override string ToolName                  => "MSBuild";

		public MSBuildRunner (Context context, string? msbuildPath = null)
			: base (context, msbuildPath)
		{}

		public async Task<bool> Run (string projectPath, string logBasePath, string target, List<string>? arguments = null)
		{
			return await Run (projectPath, logBasePath, target, null, arguments);
		}

		public async Task<bool> Run (string projectPath, string logBasePath, string target, string? configuration, List<string>? arguments = null)
		{
			ProcessRunner runner = CreateMSBuildRunner (forBinlog: false);
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

		protected override ProcessRunner CreateMSBuildRunner (bool forBinlog)
		{
			ProcessRunner runner = CreateProcessRunner ();
			runner.WorkingDirectory = WorkingDirectory;

			AddArguments (runner, StandardArguments);

			return runner;
		}
	}
}
