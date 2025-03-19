# Xamarin.Android performance test runner

A utility to automate running Xamarin.Android performance tests.

Run `xaptr --help` to get a summary of available command line options.

To configure the test, copy and modify the provided `xaptr.conf-sample`
file to `.xaptr.conf` in your project's directory (or use `xaptr -x path/to/config`).

Before the runner can test your application, copy the `Directory.Build.targets.copyme`
to the same directory where your .csproj lives, renaming the file to `Directory.Build.targets`.

Results of two runs can be compared using the `-d` parameter.
