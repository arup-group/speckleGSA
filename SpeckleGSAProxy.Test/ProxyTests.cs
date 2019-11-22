using NUnit.Framework;
using System;

namespace SpeckleGSAProxy.Test
{
  [TestFixture]
  public class ProxyTests
  {

    private string expectedGwaPerIdsFileName = "TestGwaRecords.json";
    private string savedGwaFileName = "Structural Demo 191010.gwa";
    private string savedKeywordsFileName = "Keywords 191010.txt";

    private string testDataDirectory { get => AppDomain.CurrentDomain.BaseDirectory.TrimEnd(new[] { '\\' }) + @"\..\..\TestData\"; }

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
