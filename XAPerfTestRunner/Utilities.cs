using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;

namespace XAPerfTestRunner
{
	static partial class Utilities
	{
		static readonly TimeSpan IOExceptionRetryInitialDelay = TimeSpan.FromMilliseconds (250);
		static readonly int IOExceptionRetries = 5;
		static readonly List<string> ExecutableExtensions = new List<string> ();

		public static readonly Encoding UTF8NoBOM = new UTF8Encoding (false);
		public static readonly Regex AndroidTFM = new Regex ("^net\\d+\\.\\d+-android", RegexOptions.Compiled);

		static Utilities ()
		{
			InitOS ();
		}

		public static string GetAttributeValue (XmlNode? node, string name, string defaultValue = Constants.Unknown)
		{
			XmlNode? attr = node?.Attributes?.GetNamedItem (name);
			if (String.IsNullOrEmpty (attr?.Value)) {
				return defaultValue;
			}

			return attr.Value.Trim ();
		}

		public static T FirstOf<T> (T? first, T? second = null, T? third =null, T? fourth = null) where T: struct
		{
			if (first.HasValue)
				return first.Value;

			if (second.HasValue)
				return second.Value;

			if (third.HasValue)
				return third.Value;

			if (fourth.HasValue)
				return fourth.Value;

			return default(T);
		}

		public static string FirstOf (string? first, string? second = null, string? third = null, string? fourth = null)
		{
			if (!String.IsNullOrEmpty (first))
				return first;

			if (!String.IsNullOrEmpty (second))
				return second;

			if (!String.IsNullOrEmpty (third))
				return third;

			if (!String.IsNullOrEmpty (fourth))
				return fourth;

			return String.Empty;
		}

		public static bool ParseBoolean (string? value, ref bool target)
		{
			string? v = value?.Trim ();
			if (String.IsNullOrEmpty (v))
				return false;

			if (String.Compare ("yes", v, StringComparison.OrdinalIgnoreCase) == 0 || String.Compare ("true", v, StringComparison.OrdinalIgnoreCase) == 0) {
				target = true;
				return true;
			}

			if (String.Compare ("no", v, StringComparison.OrdinalIgnoreCase) == 0 || String.Compare ("false", v, StringComparison.OrdinalIgnoreCase) == 0) {
				target = false;
				return true;
			}

			return false;
		}

		public static void CreateDirectory (string directoryPath)
		{
			if (String.IsNullOrEmpty (directoryPath))
				throw new ArgumentException ("must not be empty", nameof (directoryPath));

			if (Directory.Exists (directoryPath))
				return;

			Directory.CreateDirectory (directoryPath);
		}

		static string WhichCommon (string programPath, bool required = true)
		{
			if (String.IsNullOrEmpty (programPath)) {
				goto doneAndOut;
			}

			string match;
			// If it's any form of path, just return it as-is, possibly with executable extension added
			if (programPath.IndexOf (Path.DirectorySeparatorChar) >= 0) {
				match = GetExecutableWithExtension (programPath, (string ext) => {
						string fp = $"{programPath}{ext}";
						if (Utilities.FileExists (fp))
							return fp;
						return String.Empty;
					}
				);

				if (match.Length == 0 && Utilities.FileExists (programPath))
					match = programPath;

				if (match.Length > 0)
					return match;
				else if (required) {
					goto doneAndOut;
				}

				return programPath;
			}

			List<string> directories = GetPathDirectories ();
			match = GetExecutableWithExtension (programPath, (string ext) => FindProgram ($"{programPath}{ext}", directories));
			if (match.Length > 0)
				return AssertIsExecutable (match);

			match = FindProgram ($"{programPath}", directories);
			if (match.Length > 0)
				return AssertIsExecutable (match);

		  doneAndOut:
			if (required)
				throw new InvalidOperationException ($"Required program '{programPath}' could not be found");

			return String.Empty;
		}

		static string GetExecutableWithExtension (string programPath, Func<string, string> finder)
		{
			List<string>? extensions = ExecutableExtensions;
			if (extensions == null || extensions.Count == 0)
				return String.Empty;

			foreach (string extension in extensions) {
				string match = finder (extension);
				if (match.Length > 0)
					return match;
			}

			return String.Empty;
		}

		static string FindProgram (string programName, List<string> directories)
		{
			foreach (string dir in directories) {
				string path = Path.Combine (dir, programName);
				if (Utilities.FileExists (path))
					return path;
			}

			return String.Empty;
		}

		static List <string> GetPathDirectories ()
		{
			var ret = new List <string> ();
			string path = Environment.GetEnvironmentVariable ("PATH")?.Trim () ?? String.Empty;
			if (String.IsNullOrEmpty (path))
				return ret;

			ret.AddRange (path.Split (new []{ Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries));
			return ret;
		}

		public static bool IsXamarinAndroidProject (string projectFilePath)
		{
			try {
				return DetectXamarinAndroidProject (projectFilePath);
			} catch (Exception ex) {
				Log.DebugLine ($"{projectFilePath} is not a Xamarin.Android project");
				Log.DebugLine (ex.ToString ());
				return false;
			}
		}

		static bool DetectXamarinAndroidProject (string projectFilePath)
		{
			if (String.IsNullOrEmpty (projectFilePath))
				throw new ArgumentException ("must not be empty", nameof (projectFilePath));

			var doc = new XmlDocument ();
			doc.Load (projectFilePath);

			var nsmgr = new XmlNamespaceManager (doc.NameTable);
			nsmgr.AddNamespace ("msbuild", "http://schemas.microsoft.com/developer/msbuild/2003");

			// TODO: add support for `<TargetFrameworks>` etc
			XmlNode? guids = doc.DocumentElement?.SelectSingleNode ("//msbuild:Project/msbuild:PropertyGroup/msbuild:ProjectTypeGuids", nsmgr);
			if (guids == null)
				return false;

			foreach (string v in guids.InnerText.Split (';')) {
				if (!Guid.TryParse (v, out Guid guid))
					continue;
				if (guid == Constants.XAProjectType)
					return true;
			}

			return false;
		}

		public static (string packageName, string activityName) GetPackageAndActivityName (string manifestPath, string? userPackageName = null)
		{
			if (String.IsNullOrEmpty (manifestPath))
				throw new ArgumentException ("must not be empty", nameof (manifestPath));

			var doc = new XmlDocument ();
			doc.Load (manifestPath);

			var nsmgr = new XmlNamespaceManager (doc.NameTable);
			nsmgr.AddNamespace ("android", "http://schemas.android.com/apk/res/android");

			XmlNode? node = doc.DocumentElement?.SelectSingleNode ("//manifest", nsmgr);
			if (node?.Attributes == null) {
				Log.WarningLine ($"'manifest' element not found in {manifestPath}");
				goto returnNada;
			}

			string packageName = String.Empty;
			if (String.IsNullOrEmpty (userPackageName)) {
				packageName = node.Attributes ["package"]?.Value ?? String.Empty;
				if (String.IsNullOrEmpty (packageName)) {
					Log.WarningLine ($"'package' attribute not found on the 'manifest' element in {manifestPath}");
					goto returnNada;
				}
			} else
				packageName = userPackageName;

			string activityName = String.Empty;
			XmlNodeList? nodes = doc.DocumentElement?.SelectNodes ("//manifest/application/activity[@android:name]", nsmgr);
			if (nodes == null) {
				Log.WarningLine ($"No named activity nodes in {manifestPath}");
				goto returnNada;
			}

			foreach (XmlNode? activity in nodes) {
				if (activity == null)
					continue;

				XmlNode? intent = activity.SelectSingleNode ("./intent-filter/action[@android:name='android.intent.action.MAIN']", nsmgr);
				if (intent == null)
					continue;
				intent = activity.SelectSingleNode ("./intent-filter/category[@android:name='android.intent.category.LAUNCHER']", nsmgr);
				if (intent == null)
					continue;

				if (activity.Attributes == null)
					continue;

				activityName = activity.Attributes ["android:name"]?.Value ?? String.Empty;
				if (String.IsNullOrEmpty (activityName))
					Log.WarningLine ($"Launcher activity has no 'android:name' attribute in {manifestPath}");
				break;
			}

			return (packageName, activityName);

		  returnNada:
			return (String.Empty, String.Empty);
		}

		public static void DeleteDirectory (string directoryPath, bool ignoreErrors = false, bool recurse = true)
		{
			if (String.IsNullOrEmpty (directoryPath))
				throw new ArgumentException ("must not be null or empty", nameof (directoryPath));

			if (!Directory.Exists (directoryPath))
				return;

			try {
				Log.DebugLine ($"Deleting directory recursively: {directoryPath}");
				DeleteDirectoryWithRetry (directoryPath, recurse);
			} catch (Exception ex) {
				if (ignoreErrors) {
					Log.DebugLine ($"Failed to delete directory: {directoryPath}");
					Log.DebugLine (ex.ToString ());
					return;
				}

				throw;
			}
		}

		public static void DeleteDirectorySilent (string directoryPath, bool recurse = true)
		{
			if (String.IsNullOrEmpty (directoryPath))
				return;

			DeleteDirectory (directoryPath, ignoreErrors: true, recurse: true);
		}

		public static void DeleteDirectoryWithRetry (string directoryPath, bool recursive)
		{
			TimeSpan delay = IOExceptionRetryInitialDelay;
			Exception? ex = null;
			bool tryResetFilePermissions = false;

			for (int i = 0; i < IOExceptionRetries; i++) {
				ex = null;
				try {
					if (tryResetFilePermissions) {
						tryResetFilePermissions = false;
						ResetFilePermissions (directoryPath);
					}
					Log.DebugLine ($"Deleting directory {directoryPath} (recursively? {recursive})");
					Directory.Delete (directoryPath, recursive);
					return;
				} catch (IOException e) {
					ex = e;
				} catch (UnauthorizedAccessException e) {
					ex = e;
					tryResetFilePermissions = true;
				}

				WaitAWhile ($"Directory {directoryPath} deletion", i, ref ex, ref delay);
			}

			if (ex != null)
				throw ex;
		}

		static void WaitAWhile (string what, int which, ref Exception ex, ref TimeSpan delay)
		{
			Log.DebugLine ($"{what} attempt no. {which + 1} failed, retrying after delay of {delay}");
			if (ex != null)
				Log.DebugLine ($"Failure cause: {ex.Message}");
			Thread.Sleep (delay);
			delay = TimeSpan.FromMilliseconds (delay.TotalMilliseconds * 2);
		}

		static void ResetFilePermissions (string directoryPath)
		{
			foreach (string file in Directory.EnumerateFiles (directoryPath, "*", SearchOption.AllDirectories)) {
				FileAttributes attrs = File.GetAttributes (file);
				if ((attrs & (FileAttributes.Hidden | FileAttributes.ReadOnly | FileAttributes.System)) == 0)
					continue;

				File.SetAttributes (file, FileAttributes.Normal);
			}
		}

        public static void ReadProperties (XmlNodeList? propertyNodes, List<string> properties)
        {
            if (propertyNodes == null || propertyNodes.Count == 0) {
                return;
            }

            foreach (XmlNode? p in propertyNodes) {
				if (p == null) {
					continue;
                }

				string property = p.InnerText.Trim ();
				if (String.IsNullOrEmpty (property)) {
					continue;
                }

				properties.Add (property);
			}
        }
	}
}
