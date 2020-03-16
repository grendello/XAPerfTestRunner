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

		public static void WriteLine (LogLevel level, string message)
		{
			switch (level) {
				case LogLevel.Fatal:
					FatalLine (message);
					break;

				case LogLevel.Error:
					ErrorLine (message);
					break;

				case LogLevel.Warning:
					WarningLine (message);
					break;

				case LogLevel.Info:
					InfoLine (message);
					break;

				case LogLevel.Message:
					MessageLine (message);
					break;

				case LogLevel.Debug:
					DebugLine (message);
					break;
			}
		}

		public static void DebugLine (string message)
		{
			DoWrite (message, "D", logToFileOnly: true);
		}

		public static void MessageLine ()
		{
			MessageLine (String.Empty);
		}

		public static void MessageLine (string message)
		{
			DoWrite (message);
		}

		public static void InfoLine ()
		{
			DoWrite (String.Empty, String.Empty);
		}

		public static void InfoLine (string message)
		{
			DoWrite (message, "I");
		}

		public static void WarningLine (string message)
		{
			DoWrite (message, "W");
		}

		public static void ErrorLine (string message)
		{
			DoWrite (message, "E");
		}

		public static void FatalLine (string message)
		{
			DoWrite (message, "F");
			Environment.Exit (1);
		}

		static void DoWrite (string message, string? severity = null, bool writeLine = true, bool logToFileOnly = false)
		{
			if (!String.IsNullOrEmpty (severity))
				message = $"[{severity}] {message}";

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

				if (writeLine)
					Console.WriteLine (message);
				else
					Console.Write (message);
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
