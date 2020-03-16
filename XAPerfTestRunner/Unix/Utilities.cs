using System;
using System.IO;
using System.Xml;

using Mono.Unix.Native;

namespace XAPerfTestRunner
{
	static partial class Utilities
	{
		const FilePermissions ExecutableBits = FilePermissions.S_IXUSR | FilePermissions.S_IXGRP | FilePermissions.S_IXOTH;

		public const bool IsUnix = true;
		public const bool IsWindows = false;

		static void InitOS ()
		{}

		public static string GetManagedProgramRunner (string programPath)
		{
			if (String.IsNullOrEmpty (programPath))
				return String.Empty;

			if (programPath.EndsWith (".exe", StringComparison.OrdinalIgnoreCase) || programPath.EndsWith (".dll", StringComparison.OrdinalIgnoreCase))
				return "mono"; // Caller will find the exact mono executable, we just provide a name

			return String.Empty;
		}
		
		public static string Which (string programPath, bool required = true)
		{
			return WhichCommon (programPath, required);
		}

		public static bool FileExists (string path)
		{
			if (path.Length == 0 || !File.Exists (path))
				return false;

			if (FileIsDanglingSymlink (path)) {
				Log.WarningLine ($"File {path} is a dangling symlink. Treating as NOT existing.");
				return false;
			}

			return true;
		}

		static bool FileIsDanglingSymlink (string path)
		{
			int ret = Syscall.stat (path, out Stat sbuf);
			if (ret < 0)
				Log.DebugLine ($"stat on {path} returned {ret}. Errno: {Stdlib.GetLastError ()}");
			if (ret == 0 || (ret < 0 && Stdlib.GetLastError () != Errno.ENOENT)) {
				// Either a valid symlink or an error other than ENOENT
				return false;
			}

			return true;
		}

		static string AssertIsExecutable (string fullPath)
		{
			IsExecutable (fullPath, true);
			return fullPath;
		}

		static bool IsExecutable (string fullPath, bool throwOnErrors = false)
		{
			Stat sbuf;
			int ret = Syscall.stat (fullPath, out sbuf);

			if (ret < 0) {
				if (throwOnErrors)
					throw new InvalidOperationException ($"Failed to stat file '{fullPath}': {Stdlib.strerror (Stdlib.GetLastError ())}");
				return false;
			}

			if ((sbuf.st_mode & ExecutableBits) == 0) {
				if (throwOnErrors)
					throw new InvalidOperationException ($"File '{fullPath}' is not executable");
				return false;
			}

			return true;
		}
	}
}
