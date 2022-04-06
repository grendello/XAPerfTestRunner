using System;
using System.IO;
using System.Text;

namespace XAPerfTestRunner
{
    class ProcessStandardStreamWrapper : TextWriter
    {
	    string indentString = " | ";

	    public bool IndentOutput         { get; set; } = true;
	    public LogLevel LoggingLevel     { get; set; } = LogLevel.Debug;
	    public string CustomSeverityName { get; set; } = String.Empty;

	    public string IndentString {
		    get => indentString;
		    set {
			    indentString = value ?? String.Empty;
		    }
	    }

	    public override Encoding Encoding => Encoding.Default;

	    public ProcessStandardStreamWrapper ()
	    {}

	    public ProcessStandardStreamWrapper (IFormatProvider formatProvider)
		    : base (formatProvider)
        {}

	    public override void WriteLine (string? value)
	    {
		    DoWrite (value ?? String.Empty);
	    }

	    protected virtual (string message, Color color) PreprocessMessage (string message)
	    {
		    string severity = CustomSeverityName;
		    if (LoggingLevel != LogLevel.Message && String.IsNullOrEmpty (severity))
			    severity = LoggingLevel.ToString ();

		    if (!String.IsNullOrEmpty (severity)) {
			    Color color;

			    if (String.Compare (severity, "stderr", StringComparison.Ordinal) == 0) {
				    color = Color.ProgramStderr;
			    } else {
				    color = Color.ProgramStdout;
			    }

			    return ($"[{severity}] {message}", color);
		    }

		    return (message, Color.ProgramStdout);
	    }

	    void DoWrite (string message)
	    {
		    Color color;
		    (message, color) = PreprocessMessage (message);

		    if (IndentOutput)
			    Log.WriteLine (LoggingLevel, $"{IndentString}{message}", color);
		    else
			    Log.WriteLine (LoggingLevel, message, color);
	    }
    }
}
