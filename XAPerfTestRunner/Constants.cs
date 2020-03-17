using System;
using System.IO;

namespace XAPerfTestRunner
{
	static class Constants
	{
		public const string DefaultConfiguration = "Debug";
		public const string DataRelativePath = "perfdata";
		public const string CompareResultsRelativePath = ".";
		public const string DefaultBuildCommand = "xabuild";
		public const string LogFileName = "session.log";
		public const string MSBuildLogDir = "build-logs";
		public const string DeviceLogDir = "device-logs";
		public const string ConfigFileName = ".xaptr.conf";
		public const string RawResultsFileName = "raw-results.xml";
		public const string DefaultLogTag = "default";
		public const string ReportFileName = "results.md";
		public const string ComparisonFileName = "compare-results.md";
		public const string Unknown = "unknown";
		public const string FasterIcon = "📈";
		public const string SlowerIcon = "📉";
		public const string NoChangeIcon = "≡";
		public const uint DefaultRepetitionCount = 10;
		public const bool DefaultRunPerformanceTest = true;
		public const bool DefaultRunManagedProfiler = false;
		public const bool DefaultRunNativeProfiler = false;
		public const bool DefaultCompare = false;
		public const int PauseBetweenRunsMS = 500;

		public static readonly Guid XAProjectType = new Guid ("EFBA0AD7-5A72-4C68-AF49-83D382785DCF");
		public static readonly string AndroidManifestRelativePath = Path.Combine ("android", "AndroidManifest.xml");
	}
}
