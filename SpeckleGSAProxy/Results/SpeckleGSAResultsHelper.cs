using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace SpeckleGSAProxy.Results
{
  public static class SpeckleGSAResultsHelper
  {
    public static string gsaShellPath = @"C:\Program Files\Oasys\GSA 10.1\GsaShell.exe";
    public static string gsaResultsSubdirectoryName = "Results";

    // Copied from: https://stackoverflow.com/questions/876473/is-there-a-way-to-check-if-a-file-is-in-use/33150038#33150038
    public static bool FileLocked(string FileName)
    {
      FileStream fs = null;

      try
      {
        // NOTE: This doesn't handle situations where file is opened for writing by another process but put into write shared mode, it will not throw an exception and won't show it as write locked
        fs = File.Open(FileName, FileMode.Open, FileAccess.ReadWrite, FileShare.None); // If we can't open file for reading and writing then it's locked by another process for writing
      }
      catch (UnauthorizedAccessException) // https://msdn.microsoft.com/en-us/library/y973b725(v=vs.110).aspx
      {
        // This is because the file is Read-Only and we tried to open in ReadWrite mode, now try to open in Read only mode
        try
        {
          fs = File.Open(FileName, FileMode.Open, FileAccess.Read, FileShare.None);
        }
        catch (Exception)
        {
          return true; // This file has been locked, we can't even open it to read
        }
      }
      catch (Exception)
      {
        return true; // This file has been locked
      }
      finally
      {
        if (fs != null)
          fs.Close();
      }
      return false;
    }

    public enum ResultUnitType
    {
      None = 0,
      Force,
      Length,
      Disp,
      Mass,
      Time,
      Temp,
      Stress,
      Accel,
      Angle // not supported in GWA but added here to reflect its use in the UI; being unsupported in GWA, the code will hard-wire values
            //energy and others don't seem to be supported in GWA but also not needed in result extraction code so they're left out
    }

    //These span distance, force and other unit types, so that they can be put into an array which represents x per x per x, e.g. "N/m"
    internal enum ResultUnit
    {
      N,
      KN,
      mm,
      m,
      Pa,
      kPa,
      rad
    }
  }
}
