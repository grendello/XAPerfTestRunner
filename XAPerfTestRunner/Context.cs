namespace XAPerfTestRunner
{
	class Context
	{
		public string? Configuration { get; set; }
		public string BuildCommand { get; set; } = Constants.DefaultBuildCommand;
		public string? OutputDirectory { get; set; }
		public uint? RepetitionCount { get; set; }
		public string? PackageName { get; set; }
		public bool? RunPerformanceTest { get; set; }
		public bool? RunManagedProfiler { get; set; }
		public bool? RunNativeProfiler { get; set; }
		public bool UseFastTiming { get; set; } = true;
	}
}
