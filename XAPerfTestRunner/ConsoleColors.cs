using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace XAPerfTestRunner
{
	enum Color
	{
		Accent,
		Banner,
		CommandLine,
		Debug,
		Default,
		Error,
		Fatal,
		Info,
		Message,
		ProgramStderr,
		ProgramStdout,
		Warning,
	}

	class ConsoleColors
	{
		static bool isWindows;

		static readonly Dictionary<Color, string> unixStandardSGR = new Dictionary<Color, string> {
			{ Color.Accent,        "0;1;36" },
			{ Color.Banner,        "0;1;32" },
			{ Color.CommandLine,   "0;36" },
			{ Color.Debug,         "0;1;34" },
			{ Color.Default,       "0" },
			{ Color.Error,         "0;1;31" },
			{ Color.Fatal,         "0;1;33;41" },
			{ Color.Info,          "0;1" },
			{ Color.Message,       "0" },
			{ Color.ProgramStderr, "0;31" },
			{ Color.ProgramStdout, "0;2;37" },
			{ Color.Warning,       "0;1;33" },
		};

		static readonly Dictionary<Color, string> unixTruecolorSGR = unixStandardSGR;
		static readonly Dictionary<Color, string>? unixSGR;

		static ConsoleColors ()
		{
			isWindows = RuntimeInformation.IsOSPlatform (OSPlatform.Windows);
			if (isWindows) {
				return;
			}

			string? term = Environment.GetEnvironmentVariable ("COLORTERM");
			if (!String.IsNullOrEmpty (term) && term.Contains ("truecolor", StringComparison.Ordinal)) {
				unixSGR = unixTruecolorSGR;
			} else {
				unixSGR = unixStandardSGR;
			}
		}

		public static void Set (Color color)
		{
			if (isWindows) {
				SetWindows (color);
			} else {
				SetUnix (color);
			}
		}

		public static void Reset ()
		{
			Set (Color.Default);
		}

		static void SetUnix (Color color)
		{
			if (unixSGR == null) {
				return;
			}

			WriteEcmaSGR (unixSGR[color]);
		}

		static void SetWindows (Color color)
		{}

		// Writes ECMA-48 Set Graphics Rendition sequence
		static void WriteEcmaSGR (string seq)
		{
			Console.Write ($"\x1b[{seq}m");
		}
	}
}
