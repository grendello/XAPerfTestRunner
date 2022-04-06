using System;
using System.Collections.Generic;
using System.Xml;

namespace XAPerfTestRunner
{
	class ProjectConfigSingleRunDefinition
	{
		public string BuildCommand { get; private set; } = String.Empty;
		public string Configuration { get; private set; } = String.Empty;
		public string PackageName { get; private set; } = String.Empty;
		public string LogTag { get; private set; } = String.Empty;
		public string Summary { get; private set; } = String.Empty;
		public string Description { get; private set; } = String.Empty;
		public string RunPerformanceTest { get; private set; } = String.Empty;
		public string RunManagedProfiler { get; private set; } = String.Empty;
		public string RunNativeProfiler { get; private set; } = String.Empty;
		public List<string> Properties { get; } = new List<string> ();

		public ProjectConfigSingleRunDefinition (XmlNode node)
		{
			Load (node);
		}

		void Load (XmlNode run)
		{
			XmlNode? node = run.Attributes?.GetNamedItem ("logTag");
			if (node == null)
				throw new InvalidOperationException ("The 'logTag' attribute missing from a '<run/>' element");

			LogTag = node.InnerText.Trim ();
			if (String.IsNullOrEmpty (LogTag))
				throw new InvalidOperationException ("The 'logTag' attribute of a '<run/>' element must not have an empty value");

			node = run.SelectSingleNode ("./summary");
			if (node != null) {
				Summary = node.InnerText.Trim ();
			}

			node = run.SelectSingleNode ("./description");
			if (node != null) {
				Description = node.InnerText.Trim ();
			}
			if (String.IsNullOrEmpty (Description))
			    Description = Summary;

			node = run.SelectSingleNode ("./buildCommand");
			if (node != null) {
				BuildCommand = node.InnerText;
			}

			node = run.SelectSingleNode ("./configuration");
			if (node != null) {
				Configuration = node.InnerText;
			}

			node = run.SelectSingleNode ("./packageName");
			if (node != null) {
				PackageName = node.InnerText;
			}

			node = run.SelectSingleNode ("./runPerformanceTest");
			if (node != null) {
				RunPerformanceTest = node.InnerText;
			}

			node = run.SelectSingleNode ("./runManagedProfiler");
			if (node != null) {
				RunManagedProfiler = node.InnerText;
			}

			node = run.SelectSingleNode ("./runNativeProfiler");
			if (node != null) {
				RunNativeProfiler = node.InnerText;
			}

			XmlNodeList? properties = run.SelectNodes ("./property");
			if (properties == null || properties.Count == 0)
				return;

			foreach (XmlNode? p in properties) {
				if (p == null)
					continue;

				string property = p.InnerText.Trim ();
				if (String.IsNullOrEmpty (property))
					continue;

				Properties.Add (property);
			}
		}
	}
}
