using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace SpeckleGSAResults
{
  public static class SpeckleGSAResultsHelper
  {
    public static string gsaShellPath = @"C:\Program Files\Oasys\GSA 10.1\GsaShell.exe";
    public static string gsaResultsSubdirectoryName = "Results";

    public static bool ExtractResults(string filePath, out string resultsDirPath, out List<string> errMsgs)
    {
      resultsDirPath = Path.Combine(Path.GetDirectoryName(filePath), gsaResultsSubdirectoryName);
      errMsgs = new List<string>();

      return RunGsaShellCommand(filePath, resultsDirPath);
    }

    private static bool RunGsaShellCommand(string gsaFilePath, string resultsDirPath)
    {
      // Use ProcessStartInfo class
      var startInfo = new ProcessStartInfo
      {
        CreateNoWindow = false,
        UseShellExecute = false,
        FileName = gsaShellPath,
        WindowStyle = ProcessWindowStyle.Hidden,
        Arguments = "--action export-csv --gsafile \"" + gsaFilePath + "\" --outfile \"" + resultsDirPath + "\""
      };

      try
      {
        // Start the process with the info we specified.
        // Call WaitForExit and then the using statement will close.
        using (Process exeProcess = Process.Start(startInfo))
        {
          exeProcess.WaitForExit();
        }
      }
      catch
      {
        // Log error.
        return false;
      }
      return true;
    }
  }
}
