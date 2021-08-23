using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace XAPerfTestRunner
{
	partial class AdbRunner : ToolRunner
	{
		protected override string DefaultToolExecutableName => "adb";
		protected override string ToolName => "ADB";

		public AdbRunner (Context context, string? toolPath = null)
			: base(context, toolPath)
		{}

		public async Task<bool> RunApp (string packageName, string activityName, bool waitForExit = true, bool killPreviousInstance = true)
		{
			var runner = CreateAdbRunner ();
			runner.AddArgument ("shell");
			runner.AddArgument ("am");
			runner.AddArgument ("start");
			runner.AddArgument ("-n");
			runner.AddQuotedArgument ($"{packageName}/{activityName}");
			if (waitForExit)
				runner.AddArgument ("-W");
			if (killPreviousInstance)
				runner.AddArgument ("-S");

			return await RunAdb (runner);
		}

		public async Task<bool> Uninstall (string packageName)
		{
			var runner = CreateAdbRunner ();
			runner
				.AddArgument ("uninstall")
				.AddQuotedArgument (packageName);

			return await RunAdb (runner);
		}

		public async Task<bool> DumpLogcatToFile (string filePath)
		{
			var runner = CreateAdbRunner ();
			runner.AddArgument ("logcat");
			runner.AddArgument ("-d");

			using (var outputSink = new OutputSink (filePath)) {
				runner.AddStandardOutputSink (outputSink);
				return await RunAdb (runner, setupOutputSink: false);
			}
		}

		public async Task<bool> ClearLogcat ()
		{
			var runner = CreateAdbRunner ();
			runner.AddArgument ("logcat");
			runner.AddArgument ("-c");

			return await RunAdb (runner);
		}

		public async Task<bool> SetLogcatBufferSize (string sizeString)
		{
			var runner = CreateAdbRunner ();
			runner.AddArgument ("logcat");
			runner.AddArgument ("-G");
			runner.AddQuotedArgument (sizeString);

			return await RunAdb (runner);
		}

		public async Task<bool> SetPropertyValue (string property, string value)
		{
			var runner = CreateAdbRunner ();
			runner.AddArgument ("shell");
			runner.AddArgument ("setprop");
			runner.AddQuotedArgument (property);
			runner.AddQuotedArgument (value);

			return await RunAdb (runner);
		}

		public async Task<AndroidDeviceInfo?> GetDeviceInfo ()
		{
			var runner = CreateAdbRunner ();

			bool success;
			string deviceModel;
			(success, deviceModel) = await GetPropertyValue (runner, "ro.product.model");
			if (!success)
				return null;

			string deviceArch;
			(success, deviceArch) = await GetPropertyValue (runner, "ro.product.cpu.abi");
			if (!success)
				return null;

			string sdkVersion;
			(success, sdkVersion) = await GetPropertyValue (runner, "ro.build.version.sdk");
			if (!success)
				return null;

			return new AndroidDeviceInfo (deviceModel, deviceArch, sdkVersion);
		}

		async Task<(bool success, string output)> GetPropertyValue (ProcessRunner runner, string propertyName)
		{
			runner.ClearArguments ();
			runner.ClearOutputSinks ();
			runner.AddArgument ("shell");
			runner.AddArgument ("getprop");
			runner.AddArgument (propertyName);

			return await CaptureAdbOutput (runner);
		}

		public async Task<(bool success, string output)> GetPropertyValue (string propertyName)
		{
			var runner = CreateAdbRunner ();
			return await GetPropertyValue (runner, propertyName);
		}

		ProcessRunner AddCommonSettingsArguments (ProcessRunner runner, string verb, string settingName, string settingNamespace)
		{
			runner
				.AddArgument ("shell")
				.AddArgument ("settings")
				.AddArgument (verb)
				.AddArgument (settingNamespace)
				.AddArgument (settingName);

			return runner;
		}

		public async Task<(bool success, string ouput)> GetSettingValue (string settingName, string settingNamespace)
		{
			var runner = CreateAdbRunner ();
			return await CaptureAdbOutput (AddCommonSettingsArguments (runner, "get", settingName, settingNamespace));
		}

		public async Task<(bool success, string ouput)> GetGlobalSettingValue (string settingName)
		{
			return await GetSettingValue (settingName, "global");
		}

		public async Task<bool> SetSettingValue (string settingName, string settingValue, string settingNamespace)
		{
			var runner = CreateAdbRunner ();

			AddCommonSettingsArguments (runner, "put", settingName, settingNamespace)
				.AddQuotedArgument (settingValue);

			return await RunAdb (runner);
		}

		public async Task<bool> SetGlobalSettingValue (string settingName, string settingValue)
		{
			return await SetSettingValue (settingName, settingValue, "global");
		}

		async Task<(bool success, string output)> CaptureAdbOutput (ProcessRunner runner, bool firstLineOnly = false)
		{
			string? outputLine = null;
			List<string>? lines = null;

			using (var outputSink = (OutputSink)SetupOutputSink (runner, ignoreStderr: true)) {
				outputSink.LineCallback = (string line) => {
					if (firstLineOnly) {
						if (outputLine != null)
							return;
						outputLine = line.Trim ();
						return;
					}

					if (lines == null)
						lines = new List<string> ();
					lines.Add (line.Trim ());
				};

				if (!await RunAdb (runner, setupOutputSink: false))
					return (false, String.Empty);
			}

			if (firstLineOnly)
				return (true, outputLine ?? String.Empty);

			return (true, lines != null ? String.Join (Environment.NewLine, lines) : String.Empty);
		}

		async Task<bool> RunAdb (ProcessRunner runner, bool setupOutputSink = true, bool ignoreStderr = true)
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

		ProcessRunner CreateAdbRunner ()
		{
			var runner = CreateProcessRunner ();
			return runner;
		}

		protected override TextWriter CreateLogSink (string? logFilePath)
		{
			return new OutputSink (logFilePath);
		}
	}
}
