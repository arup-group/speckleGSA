using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSAResults.Test
{
  [TestFixture]    
  public class ResultsTests
  {
    private string TestDataDirectory { get => AppDomain.CurrentDomain.BaseDirectory.TrimEnd(new[] { '\\' }) + @"\..\..\TestData\"; }

    [TestCase("Mu1-Podium-Rev04 v10.1.gwb")]
    public void GsaShellExportCsvTest(string fileName)
    {
      var filePath = Path.Combine(TestDataDirectory, fileName);
      Assert.IsTrue(File.Exists(filePath));

      var resultsPath = Path.Combine(TestDataDirectory, SpeckleGSAResultsHelper.gsaResultsSubdirectoryName);
      if (Directory.Exists(resultsPath))
      {
        Directory.Delete(resultsPath, true);
      }

      Assert.IsTrue(SpeckleGSAResultsHelper.ExtractCsvResults(filePath, out string resultsDir, out List<string> errMsgs));
      Assert.IsNotEmpty(resultsDir);
      Assert.IsTrue(errMsgs == null || errMsgs.Count() == 0);

      Directory.Exists(resultsDir);
      var files = Directory.GetFiles(resultsDir);
      Assert.IsTrue(files.Count() > 0);
    }

    [Test]
    public void ImportResultCsvFiles()
    {
      var resultsPath = Path.Combine(TestDataDirectory, SpeckleGSAResultsHelper.gsaResultsSubdirectoryName);
      var gsaResultsContext = new SpeckleGSAResultsContext();

      var tableNames = new List<string>() { "result_node" };

      gsaResultsContext.ImportResultsFromFileDir(resultsPath, tableNames);

      //gsaResultsContext.Query("SELECT * FROM ")
    }

  }
}
