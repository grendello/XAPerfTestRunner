using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace XAPerfTestRunner
{
	class Report
	{
		abstract class ReportLine
		{
			public string Notes = String.Empty;
		}

		sealed class ReportLinePerformance : ReportLine
		{
			public string NativeToManaged = String.Empty;
			public string TotalInit = String.Empty;
			public string Displayed = String.Empty;
			public string TotalBuildTime = String.Empty;
			public string InstallTime = String.Empty;
		}

		sealed class ReportLineComparison : ReportLine
		{
			public string Before = String.Empty;
			public string After = String.Empty;
			public string Change = String.Empty;
			public decimal PerCent;
		}

		sealed class Column<T> where T: ReportLine
		{
			public readonly Func<T, string> GetData;
			public readonly string Title = String.Empty;
			public readonly int TitleLength;

			public Column (string title, Func<T, string> getData)
			{
				Title = title;
				GetData = getData;
				TitleLength = title.Length;
			}

			public Column (string title, bool isSortColumn, Func<T, string> getData)
				: this (FormatTitle (title, isSortColumn), getData)
			{}

			static string FormatTitle (string title, bool isSortColumn)
			{
				return isSortColumn ? $"*{title}" : title;
			}
		}

		sealed class ComparisonData
		{
			public RunDefinition Before { get; }
			public RunDefinition After { get; }

			public ReportAverages BeforeDisplayed { get; }
			public ReportAverages AfterDisplayed { get; }

			public ReportAverages BeforeNativeToManaged { get; }
			public ReportAverages AfterNativeToManaged { get; }

			public ReportAverages BeforeTotalInit { get; }
			public ReportAverages AfterTotalInit { get; }

			public ReportAverages BeforeBuild { get; }

			public ReportAverages AfterBuild { get; }

			public ReportAverages BeforeInstall { get; }

			public ReportAverages AfterInstall { get; }

			public ComparisonData (RunDefinition before, RunDefinition after,
			                       ReportAverages beforeDisplayed, ReportAverages afterDisplayed,
			                       ReportAverages beforeNativeToManaged, ReportAverages afterNativeToManaged,
			                       ReportAverages beforeTotalInit, ReportAverages afterTotalInit,
			                       ReportAverages beforeBuild, ReportAverages afterBuild)
			{
				Before = before;
				After = after;

				BeforeDisplayed = beforeDisplayed;
				AfterDisplayed = afterDisplayed;

				BeforeNativeToManaged = beforeNativeToManaged;
				AfterNativeToManaged = afterNativeToManaged;

				BeforeTotalInit = beforeTotalInit;
				AfterTotalInit = afterTotalInit;

				BeforeBuild = beforeBuild;
				AfterBuild = afterBuild;
			}
		}

		public string Compare (Context context, string resultsDir, string reportOnePath, string reportTwoPath)
		{
			var reportOne = RawResults.Load (context, reportOnePath);
			var reportTwo = RawResults.Load (context, reportTwoPath);
			var warnings = new List<string> ();

			if (reportOne.RepetitionCount != reportTwo.RepetitionCount) {
				warnings.Add ($"Reports were created based on different number of repetitions");
			}

			if (reportOne.Runs.Count != reportTwo.Runs.Count) {
				warnings.Add ($"Reports have a different number of runs ({reportOne.Runs.Count} and {reportTwo.Runs.Count}). Only the first {Math.Min (reportOne.Runs.Count, reportTwo.Runs.Count)} runs will be compared.");
			}

			var comparisons = new List<ComparisonData> ();
			for (int i = 0; i < Math.Min (reportOne.Runs.Count, reportTwo.Runs.Count); i++) {
				RunDefinition one = reportOne.Runs[i];
				RunDefinition two = reportTwo.Runs[i];

				bool argsDiffer = false;
				if (one.Args.Count != two.Args.Count) {
					argsDiffer = true;
				} else {
					var argsOne = new List<string> (one.Args);
					argsOne.Sort ();

					var argsTwo = new List<string> (two.Args);
					argsTwo.Sort ();

					for (int j = 0; j < argsOne.Count; j++) {
						if (String.Compare (argsOne[j].Trim (), argsTwo[j].Trim (), StringComparison.Ordinal) != 0) {
							argsDiffer = true;
							break;
						}
					}
				}

				if (argsDiffer) {
					warnings.Add ($"Arguments for run {i} differ between the two reports");
				}

				comparisons.Add (
					new ComparisonData (
						before: one,
						after: two,

						beforeDisplayed: new ReportAverages (one, reportOne.RepetitionCount, SortDisplayed),
						afterDisplayed: new ReportAverages (two, reportTwo.RepetitionCount, SortDisplayed),

						beforeNativeToManaged: new ReportAverages (one, reportOne.RepetitionCount, SortNativeToManaged),
						afterNativeToManaged: new ReportAverages (two, reportTwo.RepetitionCount, SortNativeToManaged),

						beforeTotalInit: new ReportAverages (one, reportOne.RepetitionCount, SortTotalInit),
						afterTotalInit: new ReportAverages (two, reportTwo.RepetitionCount, SortTotalInit),

						beforeBuild: new ReportAverages (one, reportOne.RepetitionCount, SortBuild),
						afterBuild: new ReportAverages (two, reportTwo.RepetitionCount, SortBuild)
					)
				);
			}

			var displayedLinesAll = new List<ReportLineComparison> ();
			var displayedLinesNoOutliers = new List<ReportLineComparison> ();
			var displayedLinesNoSlowest = new List<ReportLineComparison> ();
			var nativeToManagedLinesAll = new List<ReportLineComparison> ();
			var nativeToManagedLinesNoOutliers = new List<ReportLineComparison> ();
			var nativeToManagedLinesNoSlowest = new List<ReportLineComparison> ();
			var totalInitLinesAll = new List<ReportLineComparison> ();
			var totalInitLinesNoOutliers = new List<ReportLineComparison> ();
			var totalInitLinesNoSlowest = new List<ReportLineComparison> ();
			var buildAndInstallLinesAll = new List<ReportLineComparison> ();
			var buildAndInstallLinesNoOutliers = new List<ReportLineComparison> ();
			var buildAndInstallLinesNoSlowest = new List<ReportLineComparison> ();

			foreach (ComparisonData cdata in comparisons) {
				string notes;

				if (String.Compare (cdata.Before.Summary, cdata.After.Summary, StringComparison.OrdinalIgnoreCase) == 0) {
					notes = cdata.Before.Summary;
				} else {
					notes = $"{cdata.Before.Summary} / {cdata.After.Summary}";
				}

				displayedLinesAll.Add (CreateComparisonLine (cdata.BeforeDisplayed.All.Displayed, cdata.AfterDisplayed.All.Displayed, notes));

				if (cdata.BeforeDisplayed.NoOutliers != null) {
					displayedLinesNoOutliers.Add (CreateComparisonLine (cdata.BeforeDisplayed.NoOutliers.Displayed, cdata.AfterDisplayed.NoOutliers!.Displayed, notes));
				}

				if (cdata.BeforeDisplayed.NoSlowest != null) {
					displayedLinesNoSlowest.Add (CreateComparisonLine (cdata.BeforeDisplayed.NoSlowest.Displayed, cdata.AfterDisplayed.NoSlowest!.Displayed, notes));
				}

				nativeToManagedLinesAll.Add (CreateComparisonLine (cdata.BeforeNativeToManaged.All.NativeToManaged, cdata.AfterNativeToManaged.All.NativeToManaged, notes));

				if (cdata.BeforeNativeToManaged.NoOutliers != null) {
					nativeToManagedLinesNoOutliers.Add (CreateComparisonLine (cdata.BeforeNativeToManaged.NoOutliers.NativeToManaged, cdata.AfterNativeToManaged.NoOutliers!.NativeToManaged, notes));
				}

				if (cdata.BeforeNativeToManaged.NoSlowest != null) {
					nativeToManagedLinesNoSlowest.Add (CreateComparisonLine (cdata.BeforeNativeToManaged.NoSlowest.NativeToManaged, cdata.AfterNativeToManaged.NoSlowest!.NativeToManaged, notes));
				}

				totalInitLinesAll.Add (CreateComparisonLine (cdata.BeforeTotalInit.All.TotalInit, cdata.AfterTotalInit.All.TotalInit, notes));

				if (cdata.BeforeTotalInit.NoOutliers != null) {
					totalInitLinesNoOutliers.Add (CreateComparisonLine (cdata.BeforeTotalInit.NoOutliers.TotalInit, cdata.AfterTotalInit.NoOutliers!.TotalInit, notes));
				}

				if (cdata.BeforeTotalInit.NoSlowest != null) {
					totalInitLinesNoSlowest.Add (CreateComparisonLine (cdata.BeforeTotalInit.NoSlowest.TotalInit, cdata.AfterTotalInit.NoSlowest!.TotalInit, notes));
				}

				buildAndInstallLinesAll.Add (CreateComparisonLine (cdata.BeforeBuild.All.TotalBuildTime, cdata.AfterBuild.All.TotalBuildTime, notes));

				if (cdata.BeforeBuild.NoOutliers != null) {
					buildAndInstallLinesNoOutliers.Add (CreateComparisonLine (cdata.BeforeBuild.NoOutliers.TotalBuildTime, cdata.AfterBuild.NoOutliers!.TotalBuildTime, notes));
				}

				if (cdata.BeforeBuild.NoSlowest != null) {
					buildAndInstallLinesNoSlowest.Add (CreateComparisonLine (cdata.BeforeBuild.NoSlowest.TotalBuildTime, cdata.AfterBuild.NoSlowest!.TotalBuildTime, notes));
				}
			}

			displayedLinesAll.Sort (ComparePercentages);
			displayedLinesNoOutliers.Sort (ComparePercentages);
			displayedLinesNoSlowest.Sort (ComparePercentages);

			nativeToManagedLinesAll.Sort (ComparePercentages);
			nativeToManagedLinesNoOutliers.Sort (ComparePercentages);
			nativeToManagedLinesNoSlowest.Sort (ComparePercentages);

			totalInitLinesAll.Sort (ComparePercentages);
			totalInitLinesNoOutliers.Sort (ComparePercentages);
			totalInitLinesNoSlowest.Sort (ComparePercentages);

			buildAndInstallLinesAll.Sort (ComparePercentages);
			buildAndInstallLinesNoOutliers.Sort (ComparePercentages);
			buildAndInstallLinesNoSlowest.Sort (ComparePercentages);

			string reportFile = Path.Combine (resultsDir, Constants.ComparisonFileName);
			using (var sw = new StreamWriter (reportFile, false, Utilities.UTF8NoBOM)) {
				sw.WriteLine ("# Reports");

				sw.WriteLine ("## Before");
				WriteDescription (sw, reportOne.DateUTC, reportOne.Configuration, reportOne.XAVersion, reportOne.AndroidDevice);

				sw.WriteLine ("## After");
				WriteDescription (sw, reportTwo.DateUTC, reportTwo.Configuration, reportTwo.XAVersion, reportTwo.AndroidDevice);

				sw.WriteLine ("# Comparison");
				if (warnings.Count > 0) {
					sw.WriteLine ();
					sw.WriteLine ("**Warnings**:  ");

					foreach (string w in warnings) {
						sw.WriteLine ($"  * {w}");
					}
					sw.WriteLine ();
				}

				if (displayedLinesAll.Any ()) {
					sw.WriteLine ();
					WriteHeading (sw, "Displayed");
					WriteComparison (sw, "All runs", displayedLinesAll);
					WriteComparison (sw, "Without slowest and fastest runs", displayedLinesNoOutliers);
					WriteComparison (sw, "Without the slowest runs", displayedLinesNoSlowest);
				}

				if (nativeToManagedLinesAll.Any ()) {
					sw.WriteLine ();
					WriteHeading (sw, "Native to managed");
					WriteComparison (sw, "All runs", nativeToManagedLinesAll);
					WriteComparison (sw, "Without slowest and fastest runs", nativeToManagedLinesNoOutliers);
					WriteComparison (sw, "Without the slowest runs", nativeToManagedLinesNoSlowest);
				}

				if (totalInitLinesAll.Any ()) {
					sw.WriteLine ();
					WriteHeading (sw, "Total init");
					WriteComparison (sw, "All runs", totalInitLinesAll);
					WriteComparison (sw, "Without slowest and fastest runs", totalInitLinesNoOutliers);
					WriteComparison (sw, "Without the slowest runs", totalInitLinesNoSlowest);
				}

				if (buildAndInstallLinesAll.Any ()) {
					sw.WriteLine ();
					WriteHeading (sw, "Build Times");
					WriteComparison (sw, "All runs", buildAndInstallLinesAll);
					WriteComparison (sw, "Without slowest and fastest runs", buildAndInstallLinesNoOutliers);
					WriteComparison (sw, "Without the slowest runs", buildAndInstallLinesNoSlowest);
				}

				sw.Flush ();
			}

			return reportFile;

			void WriteHeading (StreamWriter sw, string title)
			{
				sw.WriteLine ($"## {title} (milliseconds, sorted on {Constants.DeltaIcon})");
			}

			int ComparePercentages (ReportLineComparison a, ReportLineComparison b) => b.PerCent.CompareTo (a.PerCent);
		}

		ReportLineComparison CreateComparisonLine (decimal before, decimal after, string notes)
		{
			int changeDir = after.CompareTo (before);
			string changeSign = String.Empty;
			string changeIcon = String.Empty;
			decimal percent;

			// This affects the further sorting. Since we want to promote speed ups, we reverse the sort in that the
			// lowest negative values are sorted first with the highest positive ones sorted last.
			decimal deltaDir;

			if (changeDir < 0) {
				percent = after / before;
				changeIcon = Constants.FasterIcon;
				changeSign = "-";
				deltaDir = 1.0m;
			} else if (changeDir == 0) {
				percent = 0.0m;
				changeIcon = Constants.NoChangeIcon;
				deltaDir = -1.0m;
			} else {
				percent = before / after;
				changeIcon = Constants.SlowerIcon;
				changeSign = "+";
				deltaDir = -1.0m;
			}
			percent = 100.0m - (percent * 100.0m);

			return new ReportLineComparison {
				Before = ToMilliseconds (before),
				After = ToMilliseconds (after),
				Change = $"{changeSign}{ToPercent (percent)} {changeIcon}",
				Notes = notes,
				PerCent = deltaDir * percent,
			};
		}

		void WriteComparison (StreamWriter sw, string title, List<ReportLineComparison> lines)
		{
			if (lines.Count == 0)
				return;

			var columns = new List<Column<ReportLineComparison>> {
				new Column<ReportLineComparison> ("Before", rl => rl.Before),
				new Column<ReportLineComparison> ("After", rl => rl.After),
				new Column<ReportLineComparison> (Constants.DeltaIcon, rl => rl.Change),
				new Column<ReportLineComparison> ("Notes", rl => rl.Notes),
			};

			sw.WriteLine ();
			sw.WriteLine ($"### {title}");
			WriteTable (sw, lines, columns);
		}

		public string Generate (Project project)
		{
			string reportFile = Path.Combine (project.FullDataDirectoryPath, Constants.ReportFileName);
			using (var sw = new StreamWriter (reportFile, false, Utilities.UTF8NoBOM)) {
				sw.WriteLine ($"# {project.Description}");
				WriteDescription (sw, project.WhenUTC, project.Configuration, project.XAVersion, project.AndroidDevice);

				sw.WriteLine ("# Results");
				sw.WriteLine ($"## Averages (over {project.RepetitionCount} runs)");
				if (project.Runs.Any (x => x.RunPerformanceTest)) {
					DisplayedAverages (sw, project);
					NativeToManagedAverages (sw, project);
					TotalInitAverages (sw, project);
					UnsortedAverages (sw, project);
				}
				if (project.Runs.Any (x=> x.RunBuildAndInstallProfiler)) {
					DisplayBuildAverages (sw, project);
				}

				sw.Flush ();
			}

			return reportFile;
		}

		void WriteDescription (StreamWriter sw, DateTime date, string configuration, XAVersionInfo xaVersion, AndroidDeviceInfo adi)
		{
			sw.WriteLine ();
			sw.WriteLine ($"Date (UTC): **{date}**  ");
			sw.WriteLine ($"App configuration: **{configuration}**  ");
			sw.WriteLine ();
			sw.WriteLine ("Xamarin.Android  ");
			sw.WriteLine ($"  - Version: **{xaVersion.Version}**  ");
			sw.WriteLine ($"  - Branch: **{xaVersion.Branch}**  ");
			sw.WriteLine ($"  - Commit: **{xaVersion.Commit}**  ");
			sw.WriteLine ();
			sw.WriteLine ("Device  ");
			sw.WriteLine ($"  - Model: **{adi.Model}**  ");
			sw.WriteLine ($"  - Native architecture: **{adi.Architecture}**  ");
			sw.WriteLine ($"  - SDK version: **{adi.SdkVersion}**  ");
			sw.WriteLine ();
		}

		void UnsortedAverages (StreamWriter sw, Project project)
		{
			var reportLines = new List<ReportLinePerformance> ();

			sw.WriteLine ();
			sw.WriteLine ("### Unsorted");
			sw.WriteLine ();

			foreach (RunDefinition run in project.Runs) {
				decimal nativeToManaged = 0, totalInit = 0, displayed = 0, totalBuildTime = 0, installTime = 0;
				decimal count = run.Results.Count;
				foreach (RunResults results in run.Results) {
					nativeToManaged += results.NativeToManaged / count;
					totalInit += results.TotalInit / count;
					displayed += results.Displayed / count;
					totalBuildTime += results.TotalBuildTime / count;
					installTime += results.InstallTime / count;
				}

				reportLines.Add (
					new ReportLinePerformance {
						NativeToManaged = ToMilliseconds (nativeToManaged),
						TotalInit = ToMilliseconds (totalInit),
						Displayed = ToMilliseconds (displayed),
						TotalBuildTime = ToMilliseconds (totalBuildTime),
						InstallTime = ToMilliseconds (installTime),
						Notes = run.Description,
					}
				);
			}

			var columns = new List<Column<ReportLinePerformance>> {
				new Column<ReportLinePerformance> ("Native to managed", rl => rl.NativeToManaged),
				new Column<ReportLinePerformance> ("Total init", rl => rl.TotalInit),
				new Column<ReportLinePerformance> ("Displayed", rl => rl.Displayed),
				new Column<ReportLinePerformance> ("Total Build Time", rl => rl.TotalBuildTime),
				new Column<ReportLinePerformance> ("Install Time", rl => rl.InstallTime),
				new Column<ReportLinePerformance> ("Notes", rl => rl.Notes),
			};

			WriteTable (sw, reportLines, columns);
		}

		void SortDisplayed (List<RunResults> rl)
		{
			rl.Sort ((RunResults x, RunResults y) => x.Displayed.CompareTo (y.Displayed));
		}

		void SortBuild (List<RunResults> rl)
		{
			rl.Sort ((RunResults x, RunResults y) => x.TotalBuildTime.CompareTo (y.TotalBuildTime));
		}

		void DisplayBuildAverages (StreamWriter sw, Project project)
		{
				var columns = new List<Column<ReportLinePerformance>> {
				new Column<ReportLinePerformance> ("Build", true, rl => rl.TotalBuildTime),
				new Column<ReportLinePerformance> ("Install", rl => rl.InstallTime),
				new Column<ReportLinePerformance> ("Notes", rl => rl.Notes),
			};

			Averages (sw, "Build and Install", project, columns, SortBuild);
		}

		void DisplayedAverages (StreamWriter sw, Project project)
		{
			var columns = new List<Column<ReportLinePerformance>> {
				new Column<ReportLinePerformance> ("Native to managed", rl => rl.NativeToManaged),
				new Column<ReportLinePerformance> ("Total init", rl => rl.TotalInit),
				new Column<ReportLinePerformance> ("Displayed", true, rl => rl.Displayed),
				new Column<ReportLinePerformance> ("Notes", rl => rl.Notes),
			};

			Averages (sw, "Displayed", project, columns, SortDisplayed);
		}

		void SortNativeToManaged (List<RunResults> rl)
		{
			rl.Sort ((RunResults x, RunResults y) => x.NativeToManaged.CompareTo (y.NativeToManaged));
		}

		void NativeToManagedAverages (StreamWriter sw, Project project)
		{
			var columns = new List<Column<ReportLinePerformance>> {
				new Column<ReportLinePerformance> ("Native to managed", true, rl => rl.NativeToManaged),
				new Column<ReportLinePerformance> ("Total init", rl => rl.TotalInit),
				new Column<ReportLinePerformance> ("Displayed", rl => rl.Displayed),
				new Column<ReportLinePerformance> ("Notes", rl => rl.Notes),
			};

			Averages (sw, "Native to managed", project, columns, SortNativeToManaged);
		}

		void SortTotalInit (List<RunResults> rl)
		{
			rl.Sort ((RunResults x, RunResults y) => x.TotalInit.CompareTo (y.TotalInit));
		}

		void TotalInitAverages (StreamWriter sw, Project project)
		{
			var columns = new List<Column<ReportLinePerformance>> {
				new Column<ReportLinePerformance> ("Native to managed", rl => rl.NativeToManaged),
				new Column<ReportLinePerformance> ("Total init", true, rl => rl.TotalInit),
				new Column<ReportLinePerformance> ("Displayed", rl => rl.Displayed),
				new Column<ReportLinePerformance> ("Notes", rl => rl.Notes),
			};

			Averages (sw, "Total init", project, columns, SortTotalInit);
		}

		void Averages (StreamWriter sw, string title, Project project, List<Column<ReportLinePerformance>> columns, Action<List<RunResults>> sorter)
		{
			sw.WriteLine ();
			sw.WriteLine ($"### {title}");
			sw.WriteLine ();

			var reportLines = new List<ReportLinePerformance> ();
			var sortedAverages = new List<RunResults> ();

			var reportLinesNoOutliers = new List<ReportLinePerformance> ();
			var sortedAveragesNoOutliers = new List<RunResults> ();

			var reportLinesNoSlowest = new List<ReportLinePerformance> ();
			var sortedAveragesNoSlowest = new List<RunResults> ();

			foreach (RunDefinition run in project.Runs) {
				var averages = new ReportAverages (run, project.RepetitionCount, sorter);

				sortedAverages.Add (averages.All);
				reportLines.Add (new ReportLinePerformance ());

				if (averages.NoOutliers != null) {
					sortedAveragesNoOutliers.Add (averages.NoOutliers);
					reportLinesNoOutliers.Add (new ReportLinePerformance ());
				}

				if (averages.NoSlowest != null) {
					sortedAveragesNoSlowest.Add (averages.NoSlowest);
					reportLinesNoSlowest.Add (new ReportLinePerformance ());
				}
			}

			bool haveOutliers = sortedAveragesNoOutliers.Count > 0;
			bool haveNoSlowest = sortedAveragesNoSlowest.Count > 0;

			SortAndPrepare (sortedAverages, reportLines);

			if (haveOutliers || haveNoSlowest)
				sw.WriteLine ("All runs:");
			WriteTable (sw, reportLines, columns);

			if (haveNoSlowest) {
				SortAndPrepare (sortedAveragesNoSlowest, reportLinesNoSlowest);
				sw.WriteLine ();
				sw.WriteLine ("Slowest run removed:");
				WriteTable (sw, reportLinesNoSlowest, columns);
			}

			if (haveOutliers) {
				SortAndPrepare (sortedAveragesNoOutliers, reportLinesNoOutliers);
				sw.WriteLine ();
				sw.WriteLine ("Slowest and fastest runs removed:");
				WriteTable (sw, reportLinesNoOutliers, columns);
			}

			void SortAndPrepare (List<RunResults> averages, List<ReportLinePerformance> lines)
			{
				sorter (averages);
				for (int i = 0; i < averages.Count; i++) {
					RunResults average = averages[i];
					ReportLinePerformance rl = lines[i];

					rl.NativeToManaged = ToMilliseconds (average.NativeToManaged);
					rl.TotalInit = ToMilliseconds (average.TotalInit);
					rl.Displayed = ToMilliseconds (average.Displayed);
					rl.TotalBuildTime = ToTimeStamp (average.TotalBuildTime);
					rl.InstallTime = ToTimeStamp (average.InstallTime);
					rl.Notes = average.Owner.Description;
				}
			}
		}

		void WriteTable<T> (StreamWriter sw, List<T> data, List<Column<T>> columns) where T: ReportLine
		{
			var widths = new List<int> ();
			for (int i = 0; i < columns.Count; i++) {
				widths.Add (0);
			}

			foreach (T rl in data) {
				for (int i = 0; i < columns.Count; i++) {
					Column<T> c = columns[i];
					int width = Math.Max (c.TitleLength, c.GetData (rl).Length);
					if (width > widths[i])
						widths[i] = width;
				}
			}

			int tableWidth = (columns.Count * 2) + (columns.Count + 1) + widths.Sum ();
			var horizLine = new StringBuilder ();
			for (int i = 0; i < columns.Count; i++) {
				Column<T> c = columns[i];
				int width = widths[i];

				if (i == 0) {
					sw.Write ("| ");
					horizLine.Append ("| ");
				} else {
					sw.Write (' ');
					horizLine.Append (' ');
				}
				horizLine.Append ("-".PadLeft (width, '-'));
				sw.Write (c.Title.PadRight (width));
				sw.Write (" |");
				horizLine.Append (" |");
			}

			sw.WriteLine ();
			sw.WriteLine (horizLine.ToString ());

			foreach (T rl in data) {
				for (int i = 0; i < columns.Count; i++) {
					Column<T> c = columns[i];
					int width = widths[i];

					if (i == 0)
						sw.Write ("| ");
					else
						sw.Write (' ');

					sw.Write (c.GetData (rl).PadRight (width));
					sw.Write (" |");
				}
				sw.WriteLine ();
			}
		}

		static string ToMilliseconds (decimal ns)
		{
			decimal ms = ns / 1000000;

			return ms.ToString (".000");
		}

		static string ToTimeStamp (decimal ms)
		{
			return TimeSpan.FromMilliseconds ((double)ms).ToString ();
		}

		static string ToPercent (decimal percent)
		{
			string val = percent.ToString ("0.00");
			return $"{val}%";
		}
	}
}
