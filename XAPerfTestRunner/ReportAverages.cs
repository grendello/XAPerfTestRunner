using System;
using System.Collections.Generic;

namespace XAPerfTestRunner
{
	class ReportAverages
	{
		public RunResults All { get; }
		public RunResults? NoOutliers { get; }
		public RunResults? NoSlowest { get; }

		public ReportAverages (RunDefinition run, uint repetitionCount, Action<List<RunResults>> sorter)
		{
			All = new RunResults (run);

			if (repetitionCount >= 2) {
				NoSlowest = new RunResults (run);
				if (repetitionCount >= 3)
					NoOutliers = new RunResults (run);
			}

			Calculate (run, sorter);
		}

		void Calculate (RunDefinition run, Action<List<RunResults>>? sorter)
		{
			bool haveOutliers = NoOutliers != null;
			bool haveNoSlowest = NoSlowest != null;

			var runResults = new List<RunResults> (run.Results);
			decimal count = runResults.Count;

			if ((haveOutliers || haveNoSlowest) && sorter != null) {
				sorter (runResults);
			}

			for (int i = 0; i < count; i++) {
				RunResults results = runResults [i];

				if ((haveOutliers || haveNoSlowest) && i < runResults.Count - 1) {
					if (i > 0) {
						NoOutliers!.NativeToManaged += results.NativeToManaged / (count - 2);
						NoOutliers.TotalInit += results.TotalInit / (count - 2);
						NoOutliers.Displayed += results.Displayed / (count - 2);
					}

					NoSlowest!.NativeToManaged += results.NativeToManaged / (count - 1);
					NoSlowest.TotalInit += results.TotalInit / (count - 1);
					NoSlowest.Displayed += results.Displayed / (count - 1);
				}

				All.NativeToManaged += results.NativeToManaged / count;
				All.TotalInit += results.TotalInit / count;
				All.Displayed += results.Displayed / count;
			}
		}
	}
}
