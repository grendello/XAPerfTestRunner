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
		}

		sealed class ReportLineComparison : ReportLine
		{
			public string Before = String.Empty;
			public string After = String.Empty;
			public string Change = String.Empty;
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

			public ComparisonData (RunDefinition before, RunDefinition after,
			                       ReportAverages beforeDisplayed, ReportAverages afterDisplayed,
			                       ReportAverages beforeNativeToManaged, ReportAverages afterNativeToManaged,
			                       ReportAverages beforeTotalInit, ReportAverages afterTotalInit)
			{
				Before = before;
				After = after;

				BeforeDisplayed = beforeDisplayed;
				AfterDisplayed = afterDisplayed;

				BeforeNativeToManaged = beforeNativeToManaged;
				AfterNativeToManaged = afterNativeToManaged;

				BeforeTotalInit = beforeTotalInit;
				AfterTotalInit = afterTotalInit;
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
						afterTotalInit: new ReportAverages (two, reportTwo.RepetitionCount, SortTotalInit)
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
			}

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

				sw.WriteLine ();
				sw.WriteLine ("## Displayed");
				WriteComparison (sw, "All runs", displayedLinesAll);
				WriteComparison (sw, "Without slowest and fastest runs", displayedLinesNoOutliers);
				WriteComparison (sw, "Without the slowest runs", displayedLinesNoSlowest);

				sw.WriteLine ();
				sw.WriteLine ("## Native to managed");
				WriteComparison (sw, "All runs", nativeToManagedLinesAll);
				WriteComparison (sw, "Without slowest and fastest runs", nativeToManagedLinesNoOutliers);
				WriteComparison (sw, "Without the slowest runs", nativeToManagedLinesNoSlowest);

				sw.WriteLine ();
				sw.WriteLine ("## Total init");
				WriteComparison (sw, "All runs", totalInitLinesAll);
				WriteComparison (sw, "Without slowest and fastest runs", totalInitLinesNoOutliers);
				WriteComparison (sw, "Without the slowest runs", totalInitLinesNoSlowest);

				sw.Flush ();
			}

			return reportFile;
		}

		ReportLineComparison CreateComparisonLine (decimal before, decimal after, string notes)
		{
			int changeDir = after.CompareTo (before);
			string changeSign = String.Empty;
			string changeIcon = String.Empty;
			decimal percent;

			if (changeDir < 0) {
				percent = after / before;
				changeIcon = Constants.FasterIcon;
				changeSign = "-";
			} else if (changeDir == 0) {
				percent = 0m;
				changeIcon = Constants.NoChangeIcon;
			} else {
				percent = before / after;
				changeIcon = Constants.SlowerIcon;
				changeSign = "+";
			}
			percent = 100 - (percent * 100);

			return new ReportLineComparison {
				Before = ToMilliseconds (before),
				After = ToMilliseconds (after),
				Change = $"{changeSign}{ToPercent (percent)} {changeIcon}",
				Notes = notes
			};
		}

		void WriteComparison (StreamWriter sw, string title, List<ReportLineComparison> lines)
		{
			if (lines.Count == 0)
				return;

			var columns = new List<Column<ReportLineComparison>> {
				new Column<ReportLineComparison> ("Before", rl => rl.Before),
				new Column<ReportLineComparison> ("After", rl => rl.After),
				new Column<ReportLineComparison> ("Change", rl => rl.Change),
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
				DisplayedAverages (sw, project);
				NativeToManagedAverages (sw, project);
				TotalInitAverages (sw, project);
				UnsortedAverages (sw, project);

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
				decimal nativeToManaged = 0, totalInit = 0, displayed = 0;
				decimal count = run.Results.Count;
				foreach (RunResults results in run.Results) {
					nativeToManaged += results.NativeToManaged / count;
					totalInit += results.TotalInit / count;
					displayed += results.Displayed / count;
				}

				reportLines.Add (
					new ReportLinePerformance {
						NativeToManaged = ToMilliseconds (nativeToManaged),
						TotalInit = ToMilliseconds (totalInit),
						Displayed = ToMilliseconds (displayed),
						Notes = run.Description,
					}
				);
			}

			var columns = new List<Column<ReportLinePerformance>> {
				new Column<ReportLinePerformance> ("Native to managed", rl => rl.NativeToManaged),
				new Column<ReportLinePerformance> ("Total init", rl => rl.TotalInit),
				new Column<ReportLinePerformance> ("Displayed", rl => rl.Displayed),
				new Column<ReportLinePerformance> ("Notes", rl => rl.Notes),
			};

			WriteTable (sw, reportLines, columns);
		}

		void SortDisplayed (List<RunResults> rl)
		{
			rl.Sort ((RunResults x, RunResults y) => x.Displayed.CompareTo (y.Displayed));
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

		static string ToPercent (decimal percent)
		{
			string val = percent.ToString ("0.00");
			return $"{val}%";
		}
	}
}
