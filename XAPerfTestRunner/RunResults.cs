namespace XAPerfTestRunner
{
	class RunResults
	{
		public decimal NativeToManaged { get; set; }
		public decimal TotalInit { get; set; }
		public decimal Displayed { get; set; }
		public decimal Decompression { get; set; }
		public decimal TotalBuildTime { get; set;}
		public decimal InstallTime { get; set;}
		public string? LogcatPath { get; set; }
		public RunDefinition Owner { get; }

		public RunResults (RunDefinition owner)
		{
			Owner = owner;
		}
	}
}
