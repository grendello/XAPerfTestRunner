using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace XAPerfTestRunner
{
	class Report
	{
		sealed class ReportLine
		{
			public string NativeToManaged = String.Empty;
			public string TotalInit = String.Empty;
			public string Displayed = String.Empty;
			public string Notes = String.Empty;
		}

		sealed class Column
		{
			public readonly Func<ReportLine, string> GetData;
			public readonly string Title = String.Empty;
			public readonly int TitleLength;

			public Column (string title, Func<ReportLine, string> getData)
			{
				Title = title;
				GetData = getData;
				TitleLength = title.Length;
			}

			public Column (string title, bool isSortColumn, Func<ReportLine, string> getData)
				: this (FormatTitle (title, isSortColumn), getData)
			{}

			static string FormatTitle (string title, bool isSortColumn)
			{
				return isSortColumn ? $"*{title}" : title;
			}
		}

		public string Compare (string resultsDir, string reportOne, string reportTwo)
		{
			string reportFile = Path.Combine (resultsDir, Constants.ComparisonFileName);

			return reportFile;
		}

		public string Generate (Project project)
		{
			string reportFile = Path.Combine (project.FullDataDirectoryPath, Constants.ReportFileName);
			using (var sw = new StreamWriter (reportFile, false, Utilities.UTF8NoBOM)) {
				sw.WriteLine ($"# {project.Description}");

				sw.WriteLine ();
				sw.WriteLine ($"Date (UTC): **{project.WhenUTC}**  ");
				sw.WriteLine ($"App configuration: **{project.Configuration}**  ");
				sw.WriteLine ();
				sw.WriteLine ("Xamarin.Android  ");
				sw.WriteLine ($"  - Version: **{project.XAVersion.Version}**  ");
				sw.WriteLine ($"  - Branch: **{project.XAVersion.Branch}**  ");
				sw.WriteLine ($"  - Commit: **{project.XAVersion.Commit}**  ");
				sw.WriteLine ();
				sw.WriteLine ("Device  ");
				sw.WriteLine ($"  - Model: **{project.AndroidDevice.Model}**  ");
				sw.WriteLine ($"  - Native architecture: **{project.AndroidDevice.Architecture}**  ");
				sw.WriteLine ($"  - SDK version: **{project.AndroidDevice.SdkVersion}**  ");
				sw.WriteLine ();

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

		void UnsortedAverages (StreamWriter sw, Project project)
		{
			var reportLines = new List<ReportLine> ();

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
					new ReportLine {
						NativeToManaged = ToMilliseconds (nativeToManaged),
						TotalInit = ToMilliseconds (totalInit),
						Displayed = ToMilliseconds (displayed),
						Notes = run.Description,
					}
				);
			}

			var columns = new List<Column> {
				new Column ("Native to managed", rl => rl.NativeToManaged),
				new Column ("Total init", rl => rl.TotalInit),
				new Column ("Displayed", rl => rl.Displayed),
				new Column ("Notes", rl => rl.Notes),
			};

			WriteTable (sw, reportLines, columns);
		}

		void DisplayedAverages (StreamWriter sw, Project project)
		{
			var columns = new List<Column> {
				new Column ("Native to managed", rl => rl.NativeToManaged),
				new Column ("Total init", rl => rl.TotalInit),
				new Column ("Displayed", true, rl => rl.Displayed),
				new Column ("Notes", rl => rl.Notes),
			};

			Averages (sw, "Displayed", project, columns, (List<RunResults> rl) => rl.Sort ((RunResults x, RunResults y) => x.Displayed.CompareTo (y.Displayed)));
		}

		void NativeToManagedAverages (StreamWriter sw, Project project)
		{
			var columns = new List<Column> {
				new Column ("Native to managed", true, rl => rl.NativeToManaged),
				new Column ("Total init", rl => rl.TotalInit),
				new Column ("Displayed", rl => rl.Displayed),
				new Column ("Notes", rl => rl.Notes),
			};

			Averages (sw, "Native to managed", project, columns, (List<RunResults> rl) => rl.Sort ((RunResults x, RunResults y) => x.NativeToManaged.CompareTo (y.NativeToManaged)));
		}

		void TotalInitAverages (StreamWriter sw, Project project)
		{
			var columns = new List<Column> {
				new Column ("Native to managed", rl => rl.NativeToManaged),
				new Column ("Total init", true, rl => rl.TotalInit),
				new Column ("Displayed", rl => rl.Displayed),
				new Column ("Notes", rl => rl.Notes),
			};

			Averages (sw, "Total init", project, columns, (List<RunResults> rl) => rl.Sort ((RunResults x, RunResults y) => x.TotalInit.CompareTo (y.TotalInit)));
		}

		void Averages (StreamWriter sw, string title, Project project, List<Column> columns, Action<List<RunResults>> sorter)
		{
			sw.WriteLine ();
			sw.WriteLine ($"### {title}");
			sw.WriteLine ();

			bool haveOutliers = project.RepetitionCount >= 3;
			bool haveNoSlowest = project.RepetitionCount >= 2;

			var reportLines = new List<ReportLine> ();
			var sortedAverages = new List<RunResults> ();

			var reportLinesNoOutliers = new List<ReportLine> ();
			var sortedAveragesNoOutliers = new List<RunResults> ();

			var reportLinesNoSlowest = new List<ReportLine> ();
			var sortedAveragesNoSlowest = new List<RunResults> ();

			foreach (RunDefinition run in project.Runs) {
				var runResults = new List<RunResults> (run.Results);
				var average = new RunResults (run);
				var averageNoOutliers = new RunResults (run);
				var averageNoSlowest = new RunResults (run);
				decimal count = runResults.Count;

				if (haveOutliers || haveNoSlowest) {
					sorter (runResults);
				}

				for (int i = 0; i < count; i++) {
					RunResults results = runResults [i];

					if ((haveOutliers || haveNoSlowest) && i < runResults.Count - 1) {
						if (i > 0) {
							averageNoOutliers.NativeToManaged += results.NativeToManaged / (count - 2);
							averageNoOutliers.TotalInit += results.TotalInit / (count - 2);
							averageNoOutliers.Displayed += results.Displayed / (count - 2);
						}

						averageNoSlowest.NativeToManaged += results.NativeToManaged / (count - 1);
						averageNoSlowest.TotalInit += results.TotalInit / (count - 1);
						averageNoSlowest.Displayed += results.Displayed / (count - 1);
					}

					average.NativeToManaged += results.NativeToManaged / count;
					average.TotalInit += results.TotalInit / count;
					average.Displayed += results.Displayed / count;
				}
				sortedAverages.Add (average);
				reportLines.Add (new ReportLine ());

				if (haveOutliers) {
					sortedAveragesNoOutliers.Add (averageNoOutliers);
					reportLinesNoOutliers.Add (new ReportLine ());
				}

				if (haveNoSlowest) {
					sortedAveragesNoSlowest.Add (averageNoSlowest);
					reportLinesNoSlowest.Add (new ReportLine ());
				}
			}

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

			void SortAndPrepare (List<RunResults> averages, List<ReportLine> lines)
			{
				sorter (averages);
				for (int i = 0; i < averages.Count; i++) {
					RunResults average = averages[i];
					ReportLine rl = lines[i];

					rl.NativeToManaged = ToMilliseconds (average.NativeToManaged);
					rl.TotalInit = ToMilliseconds (average.TotalInit);
					rl.Displayed = ToMilliseconds (average.Displayed);
					rl.Notes = average.Owner.Description;
				}
			}
		}

		void WriteTable (StreamWriter sw, List<ReportLine> data, List<Column> columns)
		{
			var widths = new List<int> ();
			for (int i = 0; i < columns.Count; i++) {
				widths.Add (0);
			}

			foreach (ReportLine rl in data) {
				for (int i = 0; i < columns.Count; i++) {
					Column c = columns[i];
					int width = Math.Max (c.TitleLength, c.GetData (rl).Length);
					if (width > widths[i])
						widths[i] = width;
				}
			}

			int tableWidth = (columns.Count * 2) + (columns.Count + 1) + widths.Sum ();
			var horizLine = new StringBuilder ();
			for (int i = 0; i < columns.Count; i++) {
				Column c = columns[i];
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

			foreach (ReportLine rl in data) {
				for (int i = 0; i < columns.Count; i++) {
					Column c = columns[i];
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
	}
}
