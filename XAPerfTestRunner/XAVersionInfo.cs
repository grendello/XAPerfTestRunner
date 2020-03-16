using System;

namespace XAPerfTestRunner
{
	class XAVersionInfo
	{
		public string Version { get; }
		public string Branch { get; }
		public string Commit { get; }
		public string RootDir { get; }

		public XAVersionInfo (string version, string branch, string commit, string rootDir)
		{
			Version = version;
			Branch = branch;
			Commit = commit;
			RootDir = rootDir;
		}

		public XAVersionInfo ()
			: this (Constants.Unknown, Constants.Unknown, Constants.Unknown, String.Empty)
		{}
	}
}
