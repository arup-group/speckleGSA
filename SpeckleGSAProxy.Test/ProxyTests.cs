using Interop.Gsa_10_0;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using SpeckleStructuralClasses;

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
    public void TestProxy()
    {
      var proxy = new GSAProxy();
      proxy.OpenFile(@"C:\Nicolaas\Repo\SpeckleStructural\SpeckleStructuralGSA.Test\TestData\Structural Demo 191004.gwb");
      var data = proxy.GetGWAData(DesignLayerKeywords);
      proxy.Close();
    }

    [Test]
    public void Reception_RawGWA()
    {
      var gsa = new ComAuto();
      object gwaResult;

      /*
      var gsaInterfacer = new GSAInterfacer() { Indexer = new Indexer() };

      //Load file with previously-received object (with Application ID saved from a Speckle stream in a previous life
      gsaInterfacer.OpenFile(Helper.ResolveFullPath("BasicTest.gwb", testDataDirectory), true, gsa);

      gsaInterfacer.InitializeReceiver();
      gsaInterfacer.Indexer.Reset();
      gsaInterfacer.Indexer.SetBaseline();

      //Receive update from Speckle which has a new record, deleted and changed compared with previously-saved
      gsaInterfacer.PreReceiving();
      gsaInterfacer.Indexer.ResetToBaseline();
      var lsNew = new object[] { "SET", "PROP_SPR.3:{speckle_app_id:gh/c}", 3, "LSPxTorsional", "NO_RGB", "GLOBAL", "TORSIONAL", 25, 0.21 };
      var lsChanged = new object[] { "SET", "PROP_SPR.3:{speckle_app_id:gh/a}", 1, "LSPxGeneral", "NO_RGB", "GLOBAL", "GENERAL", 0, 16, 0, 17, 0, 18, 0, 19, 0, 20, 0, 21, 0.21 };
      var lsRemoved = new object[] { "BLANK", "PROP_SPR.3", 2, "LSPxTorsional", "NO_RGB", "GLOBAL", "TORSIONAL", 25, 0.21 };
      gwaResult = gsaInterfacer.RunGWACommand(string.Join("\t", lsNew.Select(l => l.ToString())));
      var changedSID = gsaInterfacer.GetSID(lsChanged[1].ToString(), Convert.ToInt32(lsChanged[2])); //Add changed record to SID cache
      gwaResult = gsaInterfacer.RunGWACommand(string.Join("\t", lsChanged.Select(l => l.ToString()))); //Standard update should happen here
      gwaResult = gsaInterfacer.RunGWACommand(string.Join("\t", lsRemoved.Select(l => l.ToString())));
      gsaInterfacer.PostReceiving();
      gsaInterfacer.UpdateCasesAndTasks();
      gsaInterfacer.UpdateViews();

      //Manually update the file
      lsNew = new object[] { "SET", "PROP_SPR.3", 2, "LSPxCompression", "NO_RGB", "GLOBAL", "TORSIONAL", 12, 0.21 };
      gwaResult = gsa.GwaCommand(string.Join("\t", lsNew.Select(l => l.ToString())));

      //Receive update from Speckle which has a change to the previously received within this session of Speckle streams
      gsaInterfacer.PreReceiving();
      gsaInterfacer.Indexer.ResetToBaseline();
      lsChanged = new object[] { "SET", "PROP_SPR.3", 1, "LSPxGeneral", "NO_RGB", "GLOBAL", "GENERAL", 0, 9, 0, 0, 0, 9, 0, 0, 0, 0, 0, 0, 0 }; //Merging should happen here
      gwaResult = gsaInterfacer.RunGWACommand(string.Join("\t", lsChanged.Select(l => l.ToString()))); //Merging should happen here
      gsaInterfacer.PostReceiving();
      gsaInterfacer.UpdateCasesAndTasks();
      gsaInterfacer.UpdateViews();

      var sprProps = gsa.GwaCommand("GET_ALL\tPROP_SPR");

      gsaInterfacer.Close();

      var merger = new SpeckleUtil.SpeckleObjectMerger();
      merger.Initialise(new List<Type>() { typeof(StructuralSpringProperty) });

      var a = new StructuralSpringProperty() { Name = "Testah" };
      var b = new StructuralSpringProperty() { DampingRatio = 1.2, Stiffness = new StructuralVectorSix() { Value = new List<double> { 1, 2, 3, 4, 5, 6 } } };

      var testResult = merger.Merge(a, b);
      */
    }

    [Test]
    public void Reception_Interfacer()
    {
      /*
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
      var lsChanged = new object[] { "SET", "PROP_SPR.3:{speckle_app_id:gh/a}", 1, "LSPxGeneral", "NO_RGB", "GLOBAL", "GENERAL", 0, 16, 0, 17, 0, 18, 0, 19, 0, 20, 0, 21, 0.21 };
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

      var sprProps = gsa.GwaCommand("GET_ALL\tPROP_SPR");

      gsaProxy.Close();
      */
    }

    private void test()
    {
      //var testDataDirectory = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(new[] { '\\' }) + @"\..\..\TestData\";

      var keywords = Helper.ReadFile(savedKeywordsFileName, testDataDirectory).Replace("\r", "").Split(new[] { '\n' });

      //var rawData = Helper.ReadFile(expectedGwaPerIdsFileName, testDataDirectory);
      //var gwaRecords = Helper.DeserialiseJson<List<GwaRecord>>(rawData);

      var allGwaLines = Helper.ReadFile(savedGwaFileName, testDataDirectory).Replace("\r", "").Split(new[] { '\n' }).Where(l => !l.StartsWith("!")).ToList();
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

    public static string[] DesignLayerKeywords = new string[] { 
      "LOAD_2D_THERMAL.2",
      "ALIGN.1",
      "PATH.1",
      "USER_VEHICLE.1",
      "RIGID.3",
      "ASSEMBLY.3",
      "LOAD_GRAVITY.2",
      "PROP_SPR.3",
      "ANAL.1",
      "TASK.1",
      "GEN_REST.2",
      "ANAL_STAGE.3",
      "LIST.1",
      "LOAD_GRID_LINE.2",
      "POLYLINE.1",
      "GRID_SURFACE.1",
      "GRID_PLANE.4",
      "AXIS",
      "MEMB.7",
      "NODE.2",
      "LOAD_GRID_AREA.2",
      "LOAD_2D_FACE.2",
      "EL.3",
      "PROP_2D.5",
      "MAT_STEEL.3",
      "MAT_CONCRETE.16",
      "LOAD_BEAM",
      "LOAD_NODE.2",
      "COMBINATION.1",
      "LOAD_TITLE.2",
      "PROP_SEC.3",
      "PROP_MASS.2"
    };
  }
}
