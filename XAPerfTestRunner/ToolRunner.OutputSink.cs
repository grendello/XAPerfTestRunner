using System;
using System.IO;
using System.Text;

namespace XAPerfTestRunner
{
	abstract partial class ToolRunner
	{
		protected abstract class ToolOutputSink : TextWriter
		{
			protected StreamWriter? Writer { get; private set; }

			public override Encoding Encoding => Encoding.Default;

			protected ToolOutputSink (string? logFilePath)
			{
				if (!String.IsNullOrEmpty (logFilePath)) {
					Utilities.CreateDirectory (Path.GetDirectoryName (logFilePath)!);
					Writer = new StreamWriter (File.Open (logFilePath, FileMode.Create, FileAccess.Write), Utilities.UTF8NoBOM);
				}
			}

			public override void WriteLine (string? value)
			{
				Writer?.WriteLine (value ?? String.Empty);
			}

			protected override void Dispose (bool disposing)
			{
				if (disposing && Writer != null) {
					Writer.Flush ();
					Writer.Dispose ();
					Writer = null;
				}

				base.Dispose (disposing);
			}
		}
    }
}
