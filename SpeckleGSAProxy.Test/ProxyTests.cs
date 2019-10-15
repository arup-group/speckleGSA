using Interop.Gsa_10_0;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSAProxy.Test
{
  [TestFixture]
  public class ProxyTests
  {

    private string expectedGwaPerIdsFileName = "TestGwaRecords.json";
    private string savedGwaFileName = "Structural Demo 191010.gwa";
    private string savedKeywordsFileName = "Keywords 191010.txt";

    private string testDataDirectory { get => AppDomain.CurrentDomain.BaseDirectory.TrimEnd(new[] { '\\' }) + @"\..\..\TestData\"; }


    [Test]
    public void Reception()
    {
      var gsa = new ComAuto();
      object gwaResult;

      var gsaProxy = new GSAInterfacer() { Indexer = new Indexer() };

      //Load file with previously-received object (with Application ID saved from a Speckle stream in a previous life
      gsaProxy.OpenFile(Helper.ResolveFullPath("BasicTest.gwb", testDataDirectory), true, gsa);

      gsaProxy.InitializeReceiver();
      gsaProxy.Indexer.Reset();
      gsaProxy.Indexer.SetBaseline();

      //Receive update from Speckle which has a new record, deleted and changed compared with previously-saved
      gsaProxy.PreReceiving();
      gsaProxy.Indexer.ResetToBaseline();
      var lsNew = new object[] { "SET", "PROP_SPR.3:{speckle_app_id:gh/c}", 3, "LSPxTorsional", "NO_RGB", "GLOBAL", "TORSIONAL", 25, 0.21 };
      var lsChanged = new object[] { "SET", "PROP_SPR.3:{speckle_app_id:gh/a}",	1,  "LSPxGeneral", "NO_RGB", "GLOBAL", "GENERAL", 0, 16, 0, 17,	0, 18, 0, 19, 0, 20, 0, 21, 0.21 };
      var lsRemoved = new object[] { "BLANK", "PROP_SPR.3", 2, "LSPxTorsional", "NO_RGB", "GLOBAL", "TORSIONAL", 25, 0.21 };
      gwaResult = gsaProxy.RunGWACommand(string.Join("\t", lsNew.Select(l => l.ToString())));
      var changedSID = gsaProxy.GetSID(lsChanged[1].ToString(), Convert.ToInt32(lsChanged[2])); //Add changed record to SID cache
      gwaResult = gsaProxy.RunGWACommand(string.Join("\t", lsChanged.Select(l => l.ToString()))); //Standard update should happen here
      gwaResult = gsaProxy.RunGWACommand(string.Join("\t", lsRemoved.Select(l => l.ToString())));
      gsaProxy.PostReceiving();
      gsaProxy.UpdateCasesAndTasks();
      gsaProxy.UpdateViews();

      //Manually update the file
      lsNew = new object[] { "SET", "PROP_SPR.3", 2, "LSPxCompression", "NO_RGB", "GLOBAL", "TORSIONAL", 12, 0.21 };
      gwaResult = gsa.GwaCommand(string.Join("\t", lsNew.Select(l => l.ToString())));

      //Receive update from Speckle which has a change to the previously received within this session of Speckle streams
      gsaProxy.PreReceiving();
      gsaProxy.Indexer.ResetToBaseline();
      lsChanged = new object[] { "SET", "PROP_SPR.3", 1, "LSPxGeneral", "NO_RGB", "GLOBAL", "GENERAL", 0, 9, 0, 0, 0, 9, 0, 0, 0, 0, 0, 0, 0 }; //Merging should happen here
      gwaResult = gsaProxy.RunGWACommand(string.Join("\t", lsChanged.Select(l => l.ToString()))); //Merging should happen here
      gsaProxy.PostReceiving();
      gsaProxy.UpdateCasesAndTasks();
      gsaProxy.UpdateViews();

      gsaProxy.Close();
      //
    }

    private void test()
    { 
      //var testDataDirectory = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(new[] { '\\' }) + @"\..\..\TestData\";

      var keywords = Helper.ReadFile(savedKeywordsFileName, testDataDirectory).Replace("\r", "").Split(new[] { '\n' });

      //var rawData = Helper.ReadFile(expectedGwaPerIdsFileName, testDataDirectory);
      //var gwaRecords = Helper.DeserialiseJson<List<GwaRecord>>(rawData);

      var allGwaLines = Helper.ReadFile(savedGwaFileName, testDataDirectory).Replace("\r","").Split(new[] { '\n' }).Where(l => !l.StartsWith("!")).ToList();
      var gwaLines = new List<string>();
      var otherKeywords = new List<string>();
      for (var i = 0; i < allGwaLines.Count(); i++)
      {
        var lineKeyword = allGwaLines[i].Split(new[] { '\t', ':' })[0];
        if (keywords.Any(k => k == lineKeyword))
        {
          gwaLines.Add(allGwaLines[i]);
        }
        else
        {
          otherKeywords.AddIfNotContains(lineKeyword);
        }
      }


      var gsa = new GSAInterfacer() { Indexer = new Indexer() };
      gsa.NewFile();


      for (var i = 0; i < gwaLines.Count(); i++)
      {
        gsa.RunGWACommand("SET\t" + gwaLines[i]);
      }

      gsa.UpdateViews();

      Assert.IsTrue(true);
      gsa.Close();
    }
  }
}
