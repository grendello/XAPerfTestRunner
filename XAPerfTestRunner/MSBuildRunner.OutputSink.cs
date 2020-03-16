using System;
using System.Text;

namespace XAPerfTestRunner
{
	partial class MSBuildRunner
	{
		class OutputSink : ToolRunner.ToolOutputSink
		{
			public override Encoding Encoding => Encoding.Default;
			public Action<string>? LineCallback { get; set; }
			public bool SuppressOutput { get; set; }

			public OutputSink (string? logFilePath)
				: base (logFilePath)
			{
			}

			public override void WriteLine (string? value)
			{
				if (!SuppressOutput)
					base.WriteLine (value);
				LineCallback?.Invoke (value ?? String.Empty);
			}
		}
	}
}
