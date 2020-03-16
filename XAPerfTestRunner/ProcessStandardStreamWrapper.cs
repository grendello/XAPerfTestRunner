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

	    protected virtual string PreprocessMessage (string message)
	    {
		    string severity = CustomSeverityName;
		    if (LoggingLevel != LogLevel.Message && String.IsNullOrEmpty (severity))
			    severity = LoggingLevel.ToString ();

		    if (!String.IsNullOrEmpty (severity))
			    return $"[{severity}] {message}";

		    return message;
	    }

	    void DoWrite (string message)
	    {
		    message = PreprocessMessage (message) ?? String.Empty;

		    if (IndentOutput)
			    Log.WriteLine (LoggingLevel, $"{IndentString}{message}");
		    else
			    Log.WriteLine (LoggingLevel, message);
	    }
    }
}
