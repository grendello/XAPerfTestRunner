using System;
using System.Collections.Generic;
using System.Xml;

namespace XAPerfTestRunner
{
	class RunDefinition
	{
		public string LogTag { get; private set; }
		public string Summary { get; private set; }
		public string Description { get; private set; }
		public string Configuration { get; private set; }
		public string BuildCommand { get; private set; }
		public string PackageName { get; private set; }
		public bool RunPerformanceTest { get; private set; }
		public bool RunManagedProfiler { get; private set; }
		public bool RunNativeProfiler { get; private set; }
		public List<string> Args { get; private set; }
		public List<RunResults> Results { get; } = new List<RunResults> ();
		public string? BinlogPath { get; set; }

		public RunDefinition (Context context, ProjectConfigSingleRunDefinition runDefinition, ProjectConfig projectConfig)
		{
			LogTag = Utilities.FirstOf (runDefinition.LogTag, Constants.DefaultLogTag);
			Summary = runDefinition.Summary;
			Description = Utilities.FirstOf (runDefinition.Description, runDefinition.Summary);
			Configuration = Utilities.FirstOf (runDefinition.Configuration, projectConfig.Configuration, context.Configuration, Constants.DefaultConfiguration);
			BuildCommand = Utilities.FirstOf (runDefinition.BuildCommand, projectConfig.BuildCommand, context.BuildCommand, Constants.DefaultBuildCommand);
			PackageName = Utilities.FirstOf (runDefinition.PackageName, projectConfig.PackageName, context.PackageName);

			bool b = false;
			string yesno = Utilities.FirstOf (runDefinition.RunPerformanceTest, projectConfig.RunPerformanceTest);
			if (Utilities.ParseBoolean (yesno, ref b))
				RunPerformanceTest = b;
			else
				RunPerformanceTest = Constants.DefaultRunPerformanceTest;

			yesno = Utilities.FirstOf (runDefinition.RunManagedProfiler, projectConfig.RunManagedProfiler);
			if (Utilities.ParseBoolean (yesno, ref b))
				RunManagedProfiler = b;
			else
				RunManagedProfiler = Constants.DefaultRunManagedProfiler;

			yesno = Utilities.FirstOf (runDefinition.RunNativeProfiler, projectConfig.RunNativeProfiler);
			if (Utilities.ParseBoolean (yesno, ref b))
				RunNativeProfiler = b;
			else
				RunNativeProfiler = Constants.DefaultRunNativeProfiler;

			Args = new List<string> ();
			if (runDefinition.Properties.Count == 0)
				return;

			foreach (string p in runDefinition.Properties) {
				if (String.IsNullOrEmpty (p))
					continue;

				Args.Add ($"/p:{p}");
			}
		}

		public RunDefinition (Context context)
		{
			LogTag = Constants.DefaultLogTag;
			Summary = "Run with default application options";
			Description = Summary;
			Configuration = Utilities.FirstOf (context.Configuration, Constants.DefaultConfiguration);
			BuildCommand = context.BuildCommand;
			PackageName = context.PackageName != null ? context.PackageName : String.Empty;
			RunPerformanceTest = context.RunPerformanceTest.HasValue ? context.RunPerformanceTest.Value : Constants.DefaultRunPerformanceTest;
			RunManagedProfiler = context.RunManagedProfiler.HasValue ? context.RunManagedProfiler.Value : Constants.DefaultRunManagedProfiler;
			RunNativeProfiler = context.RunNativeProfiler.HasValue ? context.RunNativeProfiler.Value : Constants.DefaultRunNativeProfiler;
			Args = new List<string> ();
		}

		public void SaveRaw (XmlWriter writer)
		{
			writer.WriteStartElement ("run");
			writer.WriteAttributeString ("logTag", LogTag);
			writer.WriteAttributeString ("summary", Summary);
			writer.WriteAttributeString ("configuration", Configuration);
			writer.WriteAttributeString ("buildCommand", BuildCommand);
			writer.WriteAttributeString ("packageName", PackageName);
			writer.WriteAttributeString ("runPerformanceTest", GetBooleanString (RunPerformanceTest));
			writer.WriteAttributeString ("runManagedProfiler", GetBooleanString (RunManagedProfiler));
			writer.WriteAttributeString ("runNativeProfiler", GetBooleanString (RunNativeProfiler));

			writer.WriteStartElement ("description");
			writer.WriteString (Description);
			writer.WriteEndElement (); // </description>

			writer.WriteStartElement ("arguments");
			foreach (string arg in Args) {
				if (String.IsNullOrEmpty (arg))
					continue;
				writer.WriteStartElement ("argument");
				writer.WriteString (arg);
				writer.WriteEndElement (); // </argument>
			}
			writer.WriteEndElement ();

			writer.WriteStartElement ("buildLog");
			writer.WriteAttributeString ("path", BinlogPath ?? String.Empty);
			writer.WriteEndElement (); // </buildLog>

			writer.WriteStartElement ("results");
			foreach (RunResults results in Results) {
				writer.WriteStartElement ("result");
				writer.WriteAttributeString ("nativeToManaged", results.NativeToManaged.ToString ());
				writer.WriteAttributeString ("totalInit", results.TotalInit.ToString ());
				writer.WriteAttributeString ("displayed", results.Displayed.ToString ());

				writer.WriteStartElement ("logcat");
				writer.WriteAttributeString ("path", results.LogcatPath ?? String.Empty);
				writer.WriteEndElement (); // </logcat>"

				writer.WriteEndElement (); // </result>
			}
			writer.WriteEndElement (); // </results>

			writer.WriteEndElement (); // </run>
		}

		public void LoadRaw (XmlNode runNode)
		{
			LogTag = Utilities.GetAttributeValue (runNode, "logTag", Constants.DefaultLogTag);
			Summary = Utilities.GetAttributeValue (runNode, "summary", "No summary specified");
			Configuration = Utilities.GetAttributeValue (runNode, "configuration");
			BuildCommand = Utilities.GetAttributeValue (runNode, "buildCommand");
			PackageName = Utilities.GetAttributeValue (runNode, "packageName", String.Empty);
			RunPerformanceTest = ReadBooleanAttribute (runNode, "runPerformanceTest", true);
			RunManagedProfiler = ReadBooleanAttribute (runNode, "runManagedProfiler");
			RunNativeProfiler = ReadBooleanAttribute (runNode, "runNativeProfiler");

			XmlNode? node = runNode.SelectSingleNode ("./description");
			if (node == null)
				Description = Summary;
			else
				Description = node.InnerText;

			BinlogPath = ReadBuildLogPath (runNode);
			ReadArguments (runNode.SelectSingleNode ("./arguments"), Args);
			ReadResults (runNode.SelectSingleNode ("./results"), Results);
		}

		void ReadResults (XmlNode? node, List<RunResults> results)
		{
			if (node == null)
				return;

			foreach (XmlNode? resultNode in node.SelectNodes ("./result")) {
				if (resultNode == null)
					continue;

				var result = new RunResults (this) {
					NativeToManaged = ReadDecimalAttribute (resultNode, "nativeToManaged"),
					TotalInit = ReadDecimalAttribute (resultNode, "totalInit"),
					Displayed = ReadDecimalAttribute (resultNode, "displayed"),
				};

				XmlNode? log = resultNode.SelectSingleNode ("./logcat");
				if (log != null)
					result.LogcatPath = Utilities.GetAttributeValue (log, "path", String.Empty);
				results.Add (result);
			}
		}

		static decimal ReadDecimalAttribute (XmlNode node, string name)
		{
			string retValue = Utilities.GetAttributeValue (node, name);
			if (String.IsNullOrEmpty (retValue))
				return 0m;

			if (!Decimal.TryParse (retValue, out decimal ret))
				return 0m;

			return ret;
		}

		static string ReadBuildLogPath (XmlNode? node)
		{
			if (node == null)
				return String.Empty;

			return Utilities.GetAttributeValue (node, "path", String.Empty);
		}

		static void ReadArguments (XmlNode? node, List<string> args)
		{
			if (node == null)
				return;

			foreach (XmlNode? argNode in node.SelectNodes ("./argument")) {
				if (argNode == null)
					continue;

				args.Add (argNode.InnerText);
			}
		}

		static bool ReadBooleanAttribute (XmlNode runNode, string name, bool defaultValue = false)
		{
			string value = Utilities.GetAttributeValue (runNode, name);
			if (String.IsNullOrEmpty (value))
				return defaultValue;

			if (!Boolean.TryParse (value, out bool ret))
				return defaultValue;

			return ret;
		}

		static string GetBooleanString (bool yesno)
		{
			return yesno ? "true" : "false";
		}
	}
}
