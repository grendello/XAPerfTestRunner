using System.IO;

namespace XAPerfTestRunner
{
	class BuildInfo
	{
		public string TargetFramework { get; }
		public string ObjDir { get; }
		public string BinDir { get; }

		public string PackageFilename { get; }

		public BuildInfo (string targetFramework, string objDir, string binDir, string packageFilename)
		{
			TargetFramework = FixPathSeparators (targetFramework);
			ObjDir = FixPathSeparators (objDir);
			BinDir = FixPathSeparators (binDir);
			PackageFilename = FixPathSeparators (packageFilename);
		}

		string FixPathSeparators (string path)
		{
			return path.Replace ('\\', Path.DirectorySeparatorChar);
		}
	}
}
