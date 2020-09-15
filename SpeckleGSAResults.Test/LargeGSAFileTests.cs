using Castle.DynamicProxy.Contributors;
using NUnit.Framework;
using SpeckleGSAInterfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSAResults.Test
{
  [TestFixture]
  public class LargeGSAFileTests
  {
    private static string TestDataDirectory = @"C:\LargeGSAFiles";

    /*
    private static string[] opviews = new[] {
      "OP_VIEW.6\t\"[EL1DFRCAUTOPTS,STRIPEYOUTPUT,REFSBYNAME,INTRESONTRUSS]\"\tAssemblyDisp\t32\t32\t1095\t507\t1\t9\t1\t4\t1\t-4\t1\t3\t1\t-1\t25\t2\t1\t-4\t3\t20\t56\t153\t156\t-1\t159\t162\t-1\t165\t167\t-1\t170\t172\t-1\t175\t177\t-1\t180\t182\t183\t184\t11\t1\t-4\t1\t18001400\t0\t-10\tkN\t0.001\tm\t1\tt\t0.001\ts\t1\t°C\t1\tmm\t1000\tN/mm²\t1e-06\tm/s²\t1\tcm\t100\t-Infinity\tInfinity\t0\t1\t4\t2\t4\t1e-12\t1\t3\t0\t2",
      "OP_VIEW.6\t\"[EL1DFRCAUTOPTS,STRIPEYOUTPUT,REFSBYNAME,INTRESONTRUSS]\"\tNodalDisp\t32\t32\t1461\t663\t1\t9\t1\t4\t1\t-4\t1\t3\t1\t-1\t25\t2\t3\t82286\t-1\t82310\t3\t20\t56\t153\t156\t-1\t159\t162\t-1\t165\t167\t-1\t170\t172\t-1\t175\t177\t-1\t180\t182\t183\t184\t11\t1\t-4\t1\t12001000\t0\t-10\tkN\t0.001\tm\t1\tt\t0.001\ts\t1\t°C\t1\tmm\t1000\tN/mm²\t1e-06\tm/s²\t1\tcm\t100\t-Infinity\tInfinity\t0\t1\t4\t2\t4\t1e-12\t1\t3\t0\t2",
      "OP_VIEW.6\t\"[EL1DFRCAUTOPTS,STRIPEYOUTPUT,REFSBYNAME,INTRESONTRUSS]\"\tBeamStress\t72\t103\t1053\t633\t1\t9\t1\t4\t1\t-4\t1\t25\t20\t25\t26\t28\t29\t30\t33\t37\t60\t61\t64\t90\t95\t119\t123\t167\t168\t170\t172\t182\t193\t240\t241\t242\t247\t2\t1\t-4\t3\t3\t1\t-1\t25\t11\t1\t-4\t1\t14003000\t-1\t-10\tkN\t0.001\tm\t1\tt\t0.001\ts\t1\t°C\t1\tmm\t1000\tN/mm²\t1e-06\tm/s²\t1\tcm\t100\t-Infinity\tInfinity\t0\t1\t4\t2\t4\t1e-12\t1\t3\t0\t2"
      };
    */

    [SetUp]
    public void Setup()
    {
      var resultsPath = Path.Combine(TestDataDirectory, SpeckleGSAResultsHelper.gsaResultsSubdirectoryName);
      if (Directory.Exists(resultsPath))
      {
        Directory.Delete(resultsPath, true);
      }
    }

    [TestCase("Mu1-Podium-Rev04 v10.1_results.gwb")]
    public void ExportCsv(string fileName)
    {
      var filePath = Path.Combine(TestDataDirectory, fileName);

      var resultsContext = new SpeckleGSAResultsContext();
      var com = new Interop.Gsa_10_1.ComAuto();

      Console.WriteLine(fileName);

      try
      {
        com.Open(filePath);
        com.DisplayGsaWindow(true);

        var startTime = DateTime.Now;

        com.Save();

        TimeSpan duration = DateTime.Now - startTime;
        Console.WriteLine("Duration of saving to file: " + duration.ToString(@"hh\:mm\:ss"));

        startTime = DateTime.Now;

        SpeckleGSAResultsHelper.ExtractCsvResults(filePath, out string resultsPath, out List<string> errMsgs);

        duration = DateTime.Now - startTime;
        Console.WriteLine("Duration of export-csv: " + duration.ToString(@"hh\:mm\:ss"));
      }
      catch { }
      finally
      {
        com.Close();
      }
    }

    [TestCase("Mu1-Podium-Rev04 v10.0_results.gwb")]
    [TestCase("MU1 rev10- composite column_10.0.gwb")]
    public void ExportCsv_100(string fileName)
    {
      var filePath = Path.Combine(TestDataDirectory, fileName);

      var resultsContext = new SpeckleGSAResultsContext();
      var com = new Interop.Gsa_10_0.ComAuto();

      Console.WriteLine(fileName);

      try
      {
        com.Open(filePath);
        com.DisplayGsaWindow(true);

        var startTime = DateTime.Now;

        com.Save();

        TimeSpan duration = DateTime.Now - startTime;
        Console.WriteLine("Duration of saving to file: " + duration.ToString(@"hh\:mm\:ss"));

        startTime = DateTime.Now;

        SpeckleGSAResultsHelper.gsaShellPath = SpeckleGSAResultsHelper.gsaShellPath.Replace("10.1", "10.0");

        SpeckleGSAResultsHelper.ExtractCsvResults(filePath, out string resultsPath, out List<string> errMsgs);

        duration = DateTime.Now - startTime;
        Console.WriteLine("Duration of export-csv: " + duration.ToString(@"hh\:mm\:ss"));
      }
      catch { }
      finally
      {
        com.Close();
      }
    }

    [TestCase("Mu1-Podium-Rev04 v10.0_results.gwb")]
    [TestCase("MU1 rev10- composite column_10.0.gwb")]
    public void ExportOutputViews_100(string fileName)
    {
      var filePath = Path.Combine(TestDataDirectory, fileName);

      var ops = new[] { "MembDefl", "MembDisp", "ElemDisp", "ElemStrain", "2DStressErr", "2DProjStress", "2DDerivedStress" };

      var com = new Interop.Gsa_10_0.ComAuto();

      Console.WriteLine("File name: " + fileName);

      try
      {
        com.Open(filePath);
        com.DisplayGsaWindow(true);

        foreach (var op in OpViews.OpViews10_0.Take(15))
        {
            com.GwaCommand(op);
        }

        com.UpdateViews();

        var startTime = DateTime.Now;

        com.SaveViewToFile("ALL_SOV", "CSV");

        TimeSpan duration = DateTime.Now - startTime;
        Console.WriteLine("Duration of saving output views to file: " + duration.ToString(@"hh\:mm\:ss"));
      }
      catch { }
      finally
      {
        com.Close();
      }
    }

    [TestCase("Mu1-Podium-Rev04_v10.1_results.gwb")]
    public void ExportOutputViews(string fileName)
    {
      var filePath = Path.Combine(TestDataDirectory, fileName);

      var ops = new[] { "MembDefl", "MembDisp", "ElemDisp", "ElemStrain", "2DStressErr", "2DProjStress", "2DDerivedStress" };

      var com = new Interop.Gsa_10_1.ComAuto();

      Console.WriteLine("File name: " + fileName);

      try
      {
        com.Open(filePath);
        com.DisplayGsaWindow(true);

        foreach (var op in OpViews.OpViews10_1.Take(15))
        {
          com.GwaCommand(op);
        }

        com.UpdateViews();

        var startTime = DateTime.Now;

        com.SaveViewToFile("ALL_SOV", "CSV");

        TimeSpan duration = DateTime.Now - startTime;
        Console.WriteLine("Duration of saving output views to file: " + duration.ToString(@"hh\:mm\:ss"));
      }
      catch { }
      finally
      {
        com.Close();
      }
    }

    //[TestCase("MU1 rev10- composite column_10.1.gwb")]
    [TestCase("Mu1-Podium-Rev04 v10.1.gwb")]
    public void OpView(string fileName)
    {
      var gwaAssemblyDisp = "OP_VIEW.6\t\"[EL1DFRCAUTOPTS,STRIPEYOUTPUT,REFSBYNAME,INTRESONTRUSS]\"\tAssemblyDisp\t32\t32\t1095\t507\t1\t9\t1\t4\t1\t-4\t1\t3\t1\t-1\t25\t2\t1\t-4\t3\t20\t56\t153\t156\t-1\t159\t162\t-1\t165\t167\t-1\t170\t172\t-1\t175\t177\t-1\t180\t182\t183\t184\t11\t1\t-4\t1\t18001400\t0\t-10\tkN\t0.001\tm\t1\tt\t0.001\ts\t1\t°C\t1\tmm\t1000\tN/mm²\t1e-06\tm/s²\t1\tcm\t100\t-Infinity\tInfinity\t0\t1\t4\t2\t4\t1e-12\t1\t3\t0\t2";
      var gwaNodalDisp = "OP_VIEW.6\t\"[EL1DFRCAUTOPTS,STRIPEYOUTPUT,REFSBYNAME,INTRESONTRUSS]\"\tNodalDisp\t32\t32\t1461\t663\t1\t9\t1\t4\t1\t-4\t1\t3\t1\t-1\t25\t2\t3\t82286\t-1\t82310\t3\t20\t56\t153\t156\t-1\t159\t162\t-1\t165\t167\t-1\t170\t172\t-1\t175\t177\t-1\t180\t182\t183\t184\t11\t1\t-4\t1\t12001000\t0\t-10\tkN\t0.001\tm\t1\tt\t0.001\ts\t1\t°C\t1\tmm\t1000\tN/mm²\t1e-06\tm/s²\t1\tcm\t100\t-Infinity\tInfinity\t0\t1\t4\t2\t4\t1e-12\t1\t3\t0\t2";
      var gwaBeamStress = "OP_VIEW.6\t\"[EL1DFRCAUTOPTS,STRIPEYOUTPUT,REFSBYNAME,INTRESONTRUSS]\"\tBeamStress\t72\t103\t1053\t633\t1\t9\t1\t4\t1\t-4\t1\t25\t20\t25\t26\t28\t29\t30\t33\t37\t60\t61\t64\t90\t95\t119\t123\t167\t168\t170\t172\t182\t193\t240\t241\t242\t247\t2\t1\t-4\t3\t3\t1\t-1\t25\t11\t1\t-4\t1\t14003000\t-1\t-10\tkN\t0.001\tm\t1\tt\t0.001\ts\t1\t°C\t1\tmm\t1000\tN/mm²\t1e-06\tm/s²\t1\tcm\t100\t-Infinity\tInfinity\t0\t1\t4\t2\t4\t1e-12\t1\t3\t0\t2";
      var filePath = Path.Combine(TestDataDirectory, fileName);

      var com = new Interop.Gsa_10_1.ComAuto();
      com.Open(filePath);
      com.DisplayGsaWindow(true);
      com.GwaCommand(gwaAssemblyDisp);
      com.GwaCommand(gwaNodalDisp);
      com.GwaCommand(gwaBeamStress);
      com.UpdateViews();
      //com.SaveViewToFile("ALL_SOV", "CSV");
      com.Close();
    }
  }
}
