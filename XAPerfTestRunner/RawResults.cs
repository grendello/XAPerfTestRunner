using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace XAPerfTestRunner
{
	class RawResults
	{
		public DateTime DateUTC { get; private set; }
		public AndroidDeviceInfo AndroidDevice { get; private set; } = new AndroidDeviceInfo ();
		public string GitCommit { get; private set; } = String.Empty;
		public string GitBranch { get; private set; } = String.Empty;
		public XAVersionInfo XAVersion { get; private set; } = new XAVersionInfo ();
		public string SessionLogPath { get; private set; } = String.Empty;
		public uint RepetitionCount { get; private set; }
		public string Configuration { get; private set; } = String.Empty;
		public List<RunDefinition> Runs { get; } = new List<RunDefinition> ();

		public static void Save (AndroidDeviceInfo adi, Project project)
		{
			var settings = new XmlWriterSettings {
				CloseOutput = true,
				Encoding = Utilities.UTF8NoBOM,
				Indent = true,
				IndentChars = "\t",
				NewLineChars = Environment.NewLine,
			};

			string outputFile = Path.Combine (project.FullDataDirectoryPath, Constants.RawResultsFileName);
			using (var writer = XmlWriter.Create (outputFile, settings)) {
				writer.WriteStartElement ("xptrRaw");
				writer.WriteAttributeString ("ticksUTC", project.WhenUTC.Ticks.ToString ());
				writer.WriteAttributeString ("configuration", project.Configuration);

				writer.WriteStartElement ("device");
				writer.WriteAttributeString ("model", adi.Model);
				writer.WriteAttributeString ("architecture", adi.Architecture);
				writer.WriteAttributeString ("sdk", adi.SdkVersion);
				writer.WriteEndElement (); // device

				writer.WriteStartElement ("projectGitInfo");
				writer.WriteAttributeString ("branch", project.GitBranch ?? String.Empty);
				writer.WriteAttributeString ("commit", project.GitCommit ?? String.Empty);
				writer.WriteEndElement (); // projectGitInfo

				writer.WriteStartElement ("xamarinAndroidVersion");
				writer.WriteAttributeString ("version", project.XAVersion?.Version ?? String.Empty);
				writer.WriteAttributeString ("branch", project.XAVersion?.Branch ?? String.Empty);
				writer.WriteAttributeString ("commit", project.XAVersion?.Commit ?? String.Empty);
				writer.WriteAttributeString ("rootDir", project.XAVersion?.RootDir ?? String.Empty);
				writer.WriteEndElement (); // xamarinAndroidVersion

				writer.WriteStartElement ("sessionLog");
				writer.WriteAttributeString ("path", Constants.LogFileName);
				writer.WriteEndElement (); // sessionLog

				writer.WriteStartElement ("runs");
				writer.WriteAttributeString ("repetitions", project.RepetitionCount.ToString ());

				foreach (RunDefinition run in project.Runs) {
					run.SaveRaw (writer);
				}

				writer.WriteEndElement (); // runs

				writer.WriteEndElement (); // xptrRaw
				writer.Flush ();
			}
		}

		public static RawResults Load (Context context, string path)
		{
			var ret = new RawResults ();

			var doc = new XmlDocument ();
			doc.Load (path);

			XmlElement root = doc.DocumentElement;
			ret.DateUTC = ReadTimeStamp (path, root);
			ret.Configuration = ReadConfiguration (path, root);
			ret.AndroidDevice = ReadDeviceInfo (path, root.SelectSingleNode ("//device"));
			ReadGitInfo (path, ret, root.SelectSingleNode ("//projectGitInfo"));
			ret.XAVersion = ReadXamarinAndroidVersion (path, root.SelectSingleNode ("//xamarinAndroidVersion"));
			ret.SessionLogPath = ReadSessionLogPath (path, root.SelectSingleNode ("//sessionLog"));

			XmlNode? runs = root.SelectSingleNode ("//runs");
			ret.RepetitionCount = ReadRepetitions (path, runs);

			foreach (XmlNode? runNode in runs.SelectNodes ("./run")) {
				if (runNode == null)
					continue;

				var run = new RunDefinition (context);
				run.LoadRaw (runNode);
				ret.Runs.Add (run);
			}

			return ret;
		}

		static uint ReadRepetitions (string path, XmlNode? node)
		{
			if (node == null)
				throw new InvalidOperationException ($"Raw results '{path}' are missing run data");

			string repetitionsValue = Utilities.GetAttributeValue (node, "repetitions");
			if (String.IsNullOrEmpty (repetitionsValue)) {
				Log.WarningLine ($"Raw results '{path}' are missing run repetitions info");
				return 0;
			}

			if (!UInt32.TryParse (repetitionsValue, out uint repetitions)) {
				Log.WarningLine ($"Raw results '{path}' have invalid value for run repetitions ('{repetitionsValue}')");
				return 0;
			}

			return repetitions;
		}

		static string ReadSessionLogPath (string path, XmlNode? node)
		{
			if (node == null) {
				Log.WarningLine ($"Raw results '{path}' are missing session log path.");
				return String.Empty;
			}

			string sessionLog = Utilities.GetAttributeValue (node, "path");
			if (String.IsNullOrEmpty (sessionLog) || Path.IsPathRooted (sessionLog)) {
				return sessionLog;
			}

			return Path.Combine (Path.GetDirectoryName (path)!, sessionLog);
		}

		static XAVersionInfo ReadXamarinAndroidVersion (string path, XmlNode? node)
		{
			if (node == null) {
				Log.WarningLine ($"Raw results '{path}' are missing Xamarin.Android version info.");
				return new XAVersionInfo ();
			}

			return new XAVersionInfo (
				version: Utilities.GetAttributeValue (node, "version"),
				branch: Utilities.GetAttributeValue (node, "branch"),
				commit: Utilities.GetAttributeValue (node, "commit"),
				rootDir: Utilities.GetAttributeValue (node, "rootDir")
			);
		}

		static void ReadGitInfo (string path, RawResults ret, XmlNode? node)
		{
			if (node == null) {
				ret.GitCommit = Constants.Unknown;
				ret.GitBranch = Constants.Unknown;
				Log.WarningLine ($"Raw results '{path}' are missing Git info.");
				return;
			}

			ret.GitCommit = Utilities.GetAttributeValue (node, "commit");
			ret.GitBranch = Utilities.GetAttributeValue (node, "branch");
		}

		static AndroidDeviceInfo ReadDeviceInfo (string path, XmlNode? node)
		{
			if (node == null) {
				Log.WarningLine ($"Raw results '{path}' are missing Android device info.");
				return new AndroidDeviceInfo ();
			}

			return new AndroidDeviceInfo (
				model: Utilities.GetAttributeValue (node, "model"),
				architecture: Utilities.GetAttributeValue (node, "architecture"),
				sdkVersion: Utilities.GetAttributeValue (node, "sdk")
			);
		}

		static string ReadConfiguration (string path, XmlNode node)
		{
			return Utilities.GetAttributeValue (node, "configuration");
		}

		static DateTime ReadTimeStamp (string path, XmlNode node)
		{
			string ticksValue = Utilities.GetAttributeValue (node, "ticksUTC", String.Empty);
			if (String.IsNullOrEmpty (ticksValue)) {
				Log.WarningLine ($"Raw results '{path}' are missing the time stamp. Current time will be used instead.");
				return DateTime.UtcNow;
			}

			if (!Int64.TryParse (ticksValue, out long ticks)) {
				Log.WarningLine ($"Raw results '{path}' have invalid time stamp value ({ticksValue}). Current time will be used instead.");
				return DateTime.UtcNow;
			}

			return new DateTime (ticks, DateTimeKind.Utc);
		}
	}
}
