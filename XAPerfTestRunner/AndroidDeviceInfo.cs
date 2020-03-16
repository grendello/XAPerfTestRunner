namespace XAPerfTestRunner
{
	class AndroidDeviceInfo
	{
		public string Model { get; }
		public string Architecture { get; }
		public string SdkVersion { get; }

		public AndroidDeviceInfo (string model, string architecture, string sdkVersion)
		{
			Model = model;
			Architecture = architecture;
			SdkVersion = sdkVersion;
		}

		public AndroidDeviceInfo ()
			: this (Constants.Unknown, Constants.Unknown, Constants.Unknown)
		{}
	}
}
