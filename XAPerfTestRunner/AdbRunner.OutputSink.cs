using System;

namespace XAPerfTestRunner
{
	partial class AdbRunner
	{
		class OutputSink : ToolRunner.ToolOutputSink
		{
			public Action<string>? LineCallback { get; set; }

			public OutputSink (string? logFilePath = null)
				: base (logFilePath)
			{}

			public override void WriteLine (string? value)
			{
				base.WriteLine (value);
				LineCallback?.Invoke (value ?? String.Empty);
			}
		}
	}
}
