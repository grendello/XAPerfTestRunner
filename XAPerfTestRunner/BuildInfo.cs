using System.IO;

namespace XAPerfTestRunner
{
	class BuildInfo
	{
		public string TargetFramework { get; }
		public string ObjDir { get; }
		public string BinDir { get; }

		public BuildInfo (string targetFramework, string objDir, string binDir)
		{
			TargetFramework = FixPathSeparators (targetFramework);
			ObjDir = FixPathSeparators (objDir);
			BinDir = FixPathSeparators (binDir);
		}

		string FixPathSeparators (string path)
		{
			return path.Replace ('\\', Path.DirectorySeparatorChar);
		}
	}
}
