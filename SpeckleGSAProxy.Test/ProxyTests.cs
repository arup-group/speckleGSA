using NUnit.Framework;
using SpeckleCore;
using SpeckleGSA;
using SpeckleUtil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SpeckleGSAProxy.Test
{
  [TestFixture]
  public class ProxyTests
  {
    public static string[] savedJsonFileNames = new[] { "lfsaIEYkR.json", "NaJD7d5kq.json", "U7ntEJkzdZ.json", "UNg87ieJG.json" };
    //private string expectedGwaPerIdsFileName = "TestGwaRecords.json";
    //private string savedGwaFileName = "Structural Demo 191010.gwa";
    //private string savedKeywordsFileName = "Keywords 191010.txt";

    private string testDataDirectory { get => AppDomain.CurrentDomain.BaseDirectory.TrimEnd(new[] { '\\' }) + @"\..\..\TestData\"; }

    [Test]
    public async Task ReceiveTest()
    {
      GSA.Init();

      var receiver = SetupReceiver(savedJsonFileNames, testDataDirectory);

      GSA.gsaProxy.NewFile();

      await receiver.Initialize("", "");

      receiver.Trigger(null, null);

      //Check cache

      GSA.gsaProxy.Close();
    }


    [TestCase("SET\tMEMB.7:{speckle_app_id:gh/a}\t5\tTheRest", "MEMB.7", 5, "gh/a", "MEMB.7:{speckle_app_id:gh/a}\t5\tTheRest")]
    [TestCase("MEMB.7:{speckle_app_id:gh/a}\t5\tTheRest", "MEMB.7", 5, "gh/a", "MEMB.7:{speckle_app_id:gh/a}\t5\tTheRest")]
    [TestCase("SET_AT\t2\tLOAD_2D_THERMAL.2:{speckle_app_id:gh/a}\tTheRest", "LOAD_2D_THERMAL.2", 2, "gh/a", "LOAD_2D_THERMAL.2:{speckle_app_id:gh/a}\tTheRest")]
    [TestCase("LOAD_2D_THERMAL.2:{speckle_app_id:gh/a}\tTheRest", "LOAD_2D_THERMAL.2", 0, "gh/a", "LOAD_2D_THERMAL.2:{speckle_app_id:gh/a}\tTheRest")]
    public void ParseGwaCommandTests(string gwa, string expKeyword, int expIndex, string expAppId, string expGwaWithoutSet)
    {
      gwa.ExtractKeywordApplicationId(out string keyword, out int? foundIndex, out string applicationId, out string gwaWithoutSet, out SpeckleGSAInterfaces.GwaSetCommandType? gwaSetCommandType);
      var index = foundIndex ?? 0;

      Assert.AreEqual(expKeyword, keyword);
      Assert.AreEqual(expIndex, index);
      Assert.AreEqual(expAppId, applicationId);
      Assert.AreEqual(expGwaWithoutSet, gwaWithoutSet);
    }

    [Test]
    public void TestProxy()
    {
      var proxy = new GSAProxy();
      proxy.OpenFile(@"C:\Nicolaas\Repo\SpeckleStructural\SpeckleStructuralGSA.Test\TestData\Structural Demo 191004.gwb");
      var data = proxy.GetGWAData(DesignLayerKeywords);
      proxy.Close();
    }

    #region private_methods
    private Receiver SetupReceiver(string[] savedJsonFileNames, string testDataDirectory)
    {
      var streamIds = savedJsonFileNames.Select(fn => fn.Split(new[] { '.' }).First()).ToList();
      /*
       * var streamIds = new List<string>();
      foreach (var fileName in savedJsonFileNames)
      {
        streamIds.Add(fileName.Split(new[] { '.' }).First());
      }
      */
      GSA.Receivers = streamIds.Select(si => new Tuple<string, string>(si, null)).ToList();

      var streamObjectsTuples = ExtractObjects(savedJsonFileNames, testDataDirectory);

      var receiver = new Receiver();

      for (int i = 0; i < streamIds.Count(); i++)
      {
        var streamObjects = streamObjectsTuples.Where(t => t.Item1 == streamIds[i]).Select(t => t.Item2).ToList();
        receiver.Receivers.Add(streamIds[i], new TestReceiver { Objects = streamObjects });
      }
      return receiver;
    }

    private List<Tuple<string, SpeckleObject>> ExtractObjects(string[] fileNames, string directory)
    {
      var speckleObjects = new List<Tuple<string, SpeckleObject>>();
      foreach (var fileName in fileNames)
      {
        var json = Helper.ReadFile(fileName, directory);
        var streamId = fileName.Split(new[] { '.' }).First();

        var response = ResponseObject.FromJson(json);
        for (int i = 0; i < response.Resources.Count(); i++)
        {
          speckleObjects.Add(new Tuple<string, SpeckleObject>(streamId, response.Resources[i]));
        }
      }
      return speckleObjects;
    }
    #endregion

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
