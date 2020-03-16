using System;
using System.IO;
using System.Collections.Generic;

namespace XAPerfTestRunner
{
	class ProjectLocator
	{
		public List<string> ProjectPaths { get; } = new List<string> ();
		public ProjectConfig? ProjectConfig { get; private set; }

		public ProjectLocator (ParsedOptions commandLineOptions, List<string> args)
		{
			if (commandLineOptions.Compare) {
				if (args.Count < 2)
					throw new InvalidOperationException ("At least two paths to performance data directories must be given in comparison mode");
				LocateDataForComparison (commandLineOptions, args);
				return;
			}

			LocateSingleProject (commandLineOptions, args);
		}

		void LocateDataForComparison (ParsedOptions commandLineOptions, List<string> args)
		{}

		void LocateSingleProject (ParsedOptions commandLineOptions, List<string> args)
		{
			string projectPath = args.Count > 0 ? args [0] : String.Empty;
			// If a project file path is specified on command line...
			if (!String.IsNullOrEmpty (projectPath)) {
				// ...and it exists,load it and...
				if (File.Exists (projectPath)) {
					ProjectPaths.Add (projectPath);

					// ...if there's a sibling or specified config file, load it
					ProjectConfig = LoadConfigFile (commandLineOptions, Path.GetDirectoryName (projectPath) ?? String.Empty);
					return;
				}

				// If a project directory is specified on command line, however, and it exists...
				if (!Directory.Exists (projectPath))
					throw new InvalidOperationException ($"'{projectPath}' does not point either to a project file or a directory containing one");

				// ...find a project in it...
				ProjectConfig = LoadConfigFile (commandLineOptions, projectPath);
				string projectFile = FindProject (projectPath, ProjectConfig);

				// ...and holler if it's not found
				if (String.IsNullOrEmpty (projectFile))
					throw new InvalidOperationException ($"Could not determine how to load a supported project from '{projectPath}'");
				ProjectPaths.Add (projectFile);
				return;
			}

			// Otherwise, try to find compatible projects in the current directory...
			projectPath = Environment.CurrentDirectory;
			ProjectConfig = LoadConfigFile (commandLineOptions, projectPath);
			projectPath = FindProject (projectPath, ProjectConfig);

			// ...and shout if nothing's there
			if (String.IsNullOrEmpty (projectPath))
				throw new InvalidOperationException ("No supported project found in the current directory. Please pass the project path on command line.");
 			ProjectPaths.Add (projectPath);
		}

		static ProjectConfig? LoadConfigFile (ParsedOptions commandLineOptions, string dir)
		{
			string? filePath;

			if (!String.IsNullOrEmpty (commandLineOptions.ConfigFile))
				filePath = commandLineOptions.ConfigFile;
			else
				filePath = Path.Combine (dir, Constants.ConfigFileName);

			if (!File.Exists (filePath))
				return null;

			return new ProjectConfig (filePath);
		}

		static string FindProject (string directory, ProjectConfig? configFile)
		{
			string? configuredProject = configFile?.ProjectFilePath;
			Log.InfoLine ($"configuredProject == '{configuredProject}'");
			if (!String.IsNullOrEmpty (configuredProject)) {
				if (!Path.IsPathRooted (configuredProject)) {
					configuredProject = Path.Combine (Path.GetDirectoryName (configFile!.ConfigFilePath)!, configuredProject);
				}

				if (!File.Exists (configuredProject)) {
					Log.ErrorLine ($"Configured project '{configuredProject}' cannot be found under the '{directory}' directory");
					return String.Empty;
				}

				return configuredProject;
			}

			var xaProjects = new List<string> ();
			foreach (string p in Directory.EnumerateFiles (directory, "*.*proj")) {
				if (!Utilities.IsXamarinAndroidProject (p))
					continue;

				xaProjects.Add (Path.GetFullPath (p));
			}

			if (xaProjects.Count == 0)
				return String.Empty;

			if (xaProjects.Count == 1)
				return Path.GetFullPath (xaProjects [0]);

			Log.ErrorLine ($"More than one Xamarin.Android project found in {directory}:");
			foreach (string p in xaProjects) {
				Log.ErrorLine ($"  {p}");
			}

			Log.ErrorLine ("Please pass one of the above projects as a parameter to xaptr");
			return String.Empty;
		}
	}
}
