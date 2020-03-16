using System;

namespace XAPerfTestRunner
{
	sealed class ParsedOptions
	{
		public bool ShowHelp { get; set; }
		public bool Compare { get; set; }
		public bool? RunPerfTest { get; set; }
		public bool? RunManagedProfiler { get; set; }
		public bool? RunNativeProfiler { get; set; }
		public uint? RepetitionCount { get; set; }
		public string LogTag { get; set; } = String.Empty;
		public string PackageName { get; set; } = String.Empty;
		public string Configuration { get; set; } = Constants.DefaultConfiguration;
		public string BuildCommand { get; set; } = Constants.DefaultBuildCommand;
		public string ConfigFile { get; set; } = String.Empty;
		public string OutputDirectory { get; set; } = String.Empty;
	}
}
