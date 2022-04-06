using System;
using System.IO;

namespace XAPerfTestRunner
{
	static class Log
	{
		static readonly object writeLock = new object();

		static TextWriter? logWriter;
		static string logFilePath = String.Empty;

		public static string LogFilePath {
			get => LogFilePath;
			set {
				lock (writeLock) {
					logFilePath = value;
					OpenLogFile (value);
				}
			}
		}

		public static void WriteLine (LogLevel level, string message, Color? color = null)
		{
			switch (level) {
				case LogLevel.Fatal:
					FatalLine (message, color ?? Color.Fatal);
					break;

				case LogLevel.Error:
					ErrorLine (message, color ?? Color.Error);
					break;

				case LogLevel.Warning:
					WarningLine (message, color ?? Color.Warning);
					break;

				case LogLevel.Info:
					InfoLine (message, color ?? Color.Info);
					break;

				case LogLevel.Message:
					MessageLine (message, color ?? Color.Message);
					break;

				case LogLevel.Debug:
					DebugLine (message, color ?? Color.Debug);
					break;
			}
		}

		public static void BannerLine (string message, Color color = Color.Banner)
		{
			DoWrite (message, color);
		}

		public static void MessageLabeled (string labelName, string labelValue, Color nameColor = Color.Message, Color labelColor = Color.Accent)
		{
			Message ($"{labelName}: ", nameColor);
			MessageLine (labelValue, labelColor);
		}

		public static void InfoLabeled (string labelName, string labelValue, Color nameColor = Color.Info, Color labelColor = Color.Accent)
		{
			Info ($"{labelName}: ", nameColor);
			InfoLine (labelValue, labelColor);
		}

		public static void DebugLine (string message, Color color = Color.Debug)
		{
			DoWrite (message, color, logToFileOnly: true);
		}

		public static void Message (string message, Color color = Color.Message)
		{
			DoWrite (message, color, writeLine: false);
		}

		public static void MessageLine ()
		{
			MessageLine (String.Empty);
		}

		public static void MessageLine (string message, Color color = Color.Message)
		{
			DoWrite (message, color);
		}

		public static void Info (string message, Color color = Color.Info)
		{
			DoWrite (message, color, writeLine: false);
		}

		public static void InfoLine ()
		{
			DoWrite (String.Empty);
		}

		public static void InfoLine (string message, Color color = Color.Info)
		{
			DoWrite (message, color);
		}

		public static void WarningLine (string message, Color color = Color.Warning)
		{
			DoWrite (message, color);
		}

		public static void ErrorLine (string message, Color color = Color.Error)
		{
			DoWrite (message, color);
		}

		public static void FatalLine (string message, Color color = Color.Fatal)
		{
			DoWrite (message, color);
			Environment.Exit (1);
		}

		static void DoWrite (string message, Color color = Color.Default, bool writeLine = true, bool logToFileOnly = false)
		{
			lock (writeLock) {
				if (logWriter != null) {
					if (writeLine)
						logWriter.WriteLine (message);
					else
						logWriter.Write (message);
					logWriter.Flush ();

					if (logToFileOnly)
						return;
				}

				try {
					ConsoleColors.Set (color);
					if (writeLine) {
						Console.WriteLine (message);
					} else {
						Console.Write (message);
					}
				} finally {
					ConsoleColors.Reset ();
				}
			}
		}

		static void OpenLogFile (string path)
		{
			if (logWriter != null) {
				logWriter.Flush ();
				logWriter.Close ();
				logWriter.Dispose ();
				logWriter = null;
			}

			if (String.IsNullOrEmpty (path))
				return;

			string dir = Path.GetDirectoryName (Path.GetFullPath (path))!;
			Utilities.CreateDirectory (dir);
			logWriter = new StreamWriter (path, false, Utilities.UTF8NoBOM);
		}
	}
}
