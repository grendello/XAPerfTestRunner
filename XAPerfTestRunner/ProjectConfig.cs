using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace XAPerfTestRunner
{
	class ProjectConfig
	{
		public string ConfigFilePath { get; }
		public string ProjectFilePath { get; private set; } = String.Empty;
		public string OutputDirectory { get; private set; } = String.Empty;
		public string PackageName { get; private set; } = String.Empty;
		public string Description { get; private set; } = String.Empty;
		public string BuildCommand { get; private set; } = String.Empty;
		public string Configuration { get; private set; } = String.Empty;
		public string RunPerformanceTest { get; private set; } = String.Empty;
		public string RunManagedProfiler { get; private set; } = String.Empty;
		public string RunNativeProfiler { get; private set; } = String.Empty;
		public string Repetitions { get; private set; } = String.Empty;
		public string PackagesDir { get; private set; } = String.Empty;
		public bool ClearPackages { get; private set; } = true;

		public List<ProjectConfigSingleRunDefinition> RunDefinitions { get; } = new List<ProjectConfigSingleRunDefinition> ();

		public ProjectConfig (string path)
		{
			ConfigFilePath = Path.GetFullPath (path);
			Load (path);
		}

		void Load (string path)
		{
			Log.InfoLine ($"Loading config from {path}");
			if (path.Length == 0)
				throw new ArgumentException ("Must be a valid path to config file", nameof (path));

			var doc = new XmlDocument ();
			doc.Load (path);

			XmlElement root = doc.DocumentElement;
			XmlNode? node = root.SelectSingleNode ("//description");
			if (node != null) {
				Description = node.InnerText.Trim ();
			} else
				Log.InfoLine ("No description");

			node = root.SelectSingleNode ("//projectFilePath");
			if (node != null) {
				ProjectFilePath = node.InnerText;
			}

			node  = root.SelectSingleNode ("//outputDirectory");
			if (node != null) {
				OutputDirectory = node.InnerText;
			}

			node = root.SelectSingleNode ("//repetitions");
			if (node != null) {
				Repetitions = node.InnerText.Trim ();
			}

			node = root.SelectSingleNode ("//buildCommand");
			if (node != null) {
				BuildCommand = node.InnerText;
			}

			node = root.SelectSingleNode ("//configuration");
			if (node != null) {
				Configuration = node.InnerText;
			}

			node = root.SelectSingleNode ("//packageName");
			if (node != null) {
				PackageName = node.InnerText;
			}

			node = root.SelectSingleNode ("//runPerformanceTest");
			if (node != null) {
				RunPerformanceTest = node.InnerText;
			}

			node = root.SelectSingleNode ("//runManagedProfiler");
			if (node != null) {
				RunManagedProfiler = node.InnerText;
			}

			node = root.SelectSingleNode ("//runNativeProfiler");
			if (node != null) {
				RunNativeProfiler = node.InnerText;
			}

			node = root.SelectSingleNode ("//packagesDir");
			if (node != null) {
				PackagesDir = node.InnerText;
				XmlNode? attr = node.Attributes.GetNamedItem ("clear");
				if (attr != null) {
					if (!Boolean.TryParse (attr.InnerText.Trim (), out bool clear)) {
						throw new InvalidOperationException ($"Failed to parse the 'clear' attribute of tag '<packagesDir>' as boolean (value was '{attr.InnerText}')");
					}
					ClearPackages = clear;
				}
			}

			XmlNodeList runs = doc.SelectNodes ("//runDefinitions/run");
			if (runs == null)
				return;

			foreach (XmlNode? run in runs) {
				if (run == null)
					continue;

				RunDefinitions.Add (new ProjectConfigSingleRunDefinition (run));
			}
		}
	}
}
