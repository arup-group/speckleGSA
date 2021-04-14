using NUnit.Framework;
using SpeckleCore;
using SpeckleGSA;
using SpeckleUtil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SpeckleGSAInterfaces;
using System.IO;
using System.Diagnostics;
using Newtonsoft.Json;

namespace SpeckleGSAProxy.Test
{
  [TestFixture]
  public class CacheTests
  {
    private string TestDataDirectory { get => AppDomain.CurrentDomain.BaseDirectory.TrimEnd(new[] { '\\' }) + @"\..\..\TestData\"; }

    private string designLayerExpectedFile = "DesignLayerSpeckleObjects.json";

    [TestCase("test_model.gwb", "A1, A2 A2 to A3;C1-\nA2,C2to A1 and C2 to C10", 3, 2)]
    [TestCase("test_model.gwb", "A1c1", 1, 1)]
    [TestCase("test_model.gwb", "All all", 3, 4)]
    public void LoadCaseNameTest(string filename, string testCaseString, int expectedAs, int expectedCs)
    {
      var gsaProxy = new GSAProxy();
      gsaProxy.OpenFile(Path.Combine(TestDataDirectory, filename), false);
      var data = gsaProxy.GetGwaData(new List<string> { "ANAL", "COMBINATION" }, false);
      gsaProxy.Close();

      var cache = new GSACache();
      foreach (var r in data)
      {
        cache.Upsert(r.Keyword, r.Index, r.GwaWithoutSet, r.StreamId, r.ApplicationId, r.GwaSetType);
      }

      var expandedLoadCases = cache.ExpandLoadCasesAndCombinations(testCaseString);

      Assert.AreEqual(expectedAs, expandedLoadCases.Where(c => char.ToLowerInvariant(c[0]) == 'a').Count());
      Assert.AreEqual(expectedCs, expandedLoadCases.Where(c => char.ToLowerInvariant(c[0]) == 'c').Count());
    }

    [Test]
    public void ReserveMultipleIndicesSet()
    {
      var cache = new GSACache();

      cache.Upsert("MEMB", 1, "MEMB.8:{speckle_app_id:Slab0}\t1\tSlab 0\tNO_RGB\tSLAB\t1\t6\t36 37 38 39 40 41 42\t0\t0\t5\tMESH\tLINEAR\t0\t0\t0\t0\t0\tACTIVE\tNO\t0\tALL", "abcdefgh", "Slab0", GwaSetCommandType.Set);

      var newIndices = new List<int>
      {
        cache.ResolveIndex("MEMB", "Slab1"),
        cache.ResolveIndex("MEMB", "Slab2"),
        cache.ResolveIndex("MEMB", "Slab3")
      };

      Assert.AreEqual(1, cache.LookupIndices("MEMB").Where(i => i.HasValue).Select(i => i.Value).Count());
      Assert.AreEqual(1, cache.LookupIndices("MEMB", new[] { "Slab0", "Slab1", "Slab2", "Slab3", "Slab4" }).Where(i => i.HasValue).Select(i => i.Value).Count());

      //Try upserting a latest record before converting a provisional to latest
      Assert.IsTrue(cache.Upsert("MEMB", 5, "MEMB.8:{speckle_app_id:Slab4}\t5\tSlab 4\tNO_RGB\tSLAB\t1\t6\t36 37 38 39 40 41 42\t0\t0\t5\tMESH\tLINEAR\t0\t0\t0\t0\t0\tACTIVE\tNO\t0\tALL", "abcdefgh", "Slab4", GwaSetCommandType.Set));
      Assert.AreEqual(2, cache.LookupIndices("MEMB").Where(i => i.HasValue).Select(i => i.Value).Count());
      Assert.AreEqual(2, cache.LookupIndices("MEMB", new[] { "Slab0", "Slab1", "Slab2", "Slab3", "Slab4" }).Where(i => i.HasValue).Select(i => i.Value).Count());

      //Now convert a provisional to latest and check that the number of records hasn't increased
      Assert.IsTrue(cache.Upsert("MEMB", 2, "MEMB.8:{speckle_app_id:Slab1}\t2\tSlab 1\tNO_RGB\tSLAB\t1\t6\t36 37 38 39 40 41 42\t0\t0\t5\tMESH\tLINEAR\t0\t0\t0\t0\t0\tACTIVE\tNO\t0\tALL", "abcdefgh", "Slab1", GwaSetCommandType.Set));
      Assert.AreEqual(3, cache.LookupIndices("MEMB").Where(i => i.HasValue).Select(i => i.Value).Count());
      Assert.AreEqual(3, cache.LookupIndices("MEMB", new[] { "Slab0", "Slab1", "Slab2", "Slab3", "Slab4" }).Where(i => i.HasValue).Select(i => i.Value).Count());

      //Check that asking to resolve a previously-created provisional index returns that same one
      Assert.AreEqual(3, cache.ResolveIndex("MEMB", "Slab2"));

      //Check that the next index recognises (and doesn't re-use) the current provisional indices
      Assert.AreEqual(6, cache.ResolveIndex("MEMB"));
    }

    [Test]
    public void ReserveMultipleIndicesSetAt()
    {
      var cache = new GSACache();

      cache.Upsert("LOAD_2D_THERMAL", 1, "LOAD_2D_THERMAL.2\tGeneral\tG6\t3\tDZ\t239\t509", "abcdefgh", "", GwaSetCommandType.SetAt);

      cache.ResolveIndex("LOAD_2D_THERMAL");
      cache.ResolveIndex("LOAD_2D_THERMAL");

      //Try upserting a latest record before converting a provisional to latest
      Assert.IsTrue(cache.Upsert("LOAD_2D_THERMAL", 4, "LOAD_2D_THERMAL.2\tGeneral\tG7\t3\tDZ\t239\t509", "abcdefgh", "Slab4", GwaSetCommandType.Set));
      Assert.AreEqual(2, cache.LookupIndices("LOAD_2D_THERMAL").Where(i => i.HasValue).Select(i => i.Value).Count());

      //Now convert a provisional to latest and check that the number of records hasn't increased
      Assert.IsTrue(cache.Upsert("LOAD_2D_THERMAL", 3, "LOAD_2D_THERMAL.2\tGeneral\tG6 G7 G8 G9 G10\t3\tDZ\t239\t509", "abcdefgh", "Slab1", GwaSetCommandType.Set));
      Assert.AreEqual(3, cache.LookupIndices("LOAD_2D_THERMAL").Where(i => i.HasValue).Select(i => i.Value).Count());

      //Check that the next index recognises (and doesn't re-use) the current provisional indices
      Assert.AreEqual(5, cache.ResolveIndex("LOAD_2D_THERMAL"));
    }

    [Test]
    public void GenerateDesignCache()
    {
      GSA.GsaApp = new GsaAppResources();
      GSA.GsaApp.gsaSettings.TargetLayer = GSATargetLayer.Design;
      GSA.GsaApp.gsaSettings.SeparateStreams = true;
      GSA.SenderInfo = new Dictionary<string, SidSpeckleRecord>() { { "testStream", new SidSpeckleRecord("testStreamId", "testStream", "testClientId") } };

      //This runs SpeckleInitializer.Initialize() and fills WriteTypePrereqs and ReadTypePrereqs
      GSA.Init("");

      //Status.MessageAdded += (s, e) => Debug.WriteLine("Message: " + e.Message);
      //Status.ErrorAdded += (s, e) => Debug.WriteLine("Error: " + e.Message);
      Status.StatusChanged += (s, e) => Debug.WriteLine("Status: " + e.Name);

      var filePath = @"C:\Users\Nic.Burgers\OneDrive - Arup\Issues\Nguyen Le\2D result\shear wall system-seismic v10.1.gwb";

      GSA.GsaApp.gsaProxy.OpenFile(Path.Combine(TestDataDirectory, filePath), false);

      var senderCoordinator = new SenderCoordinator();
      bool failed = false;
      try
      {

        //This will load data from all streams into the cache
        senderCoordinator.Initialize("", "", (restApi, apiToken) => new TestSpeckleGSASender(), new Progress<MessageEventArgs>(), new Progress<string>(), new Progress<double>());

        senderCoordinator.Trigger();

        //Each kit stores their own objects to be sent
        var speckleObjects = GSA.GetSpeckleObjectsFromSenderDictionaries();
        var response = new ResponseObject() { Resources = speckleObjects };
        var jsonToWrite = JsonConvert.SerializeObject(response, Formatting.Indented);

        Helper.WriteFile(jsonToWrite, designLayerExpectedFile, TestDataDirectory);
      }
      catch (Exception ex)
      {
        failed = true;
      }
      finally
      {
        GSA.GsaApp.gsaProxy.Close();
      }
      Assert.IsFalse(failed);
    }

    [Test]
    public void SendAnalysisThenDesign()
    {
      GSA.GsaApp = new GsaAppResources();

      //This runs SpeckleInitializer.Initialize() and fills WriteTypePrereqs and ReadTypePrereqs
      GSA.Init("");
      GSA.SenderInfo = new Dictionary<string, SidSpeckleRecord>() { { "testStream", new SidSpeckleRecord("testStreamId", "testStream", "testClientId") } };

      var json = Helper.ReadFile(designLayerExpectedFile, TestDataDirectory);

      var response = ResponseObject.FromJson(json);
      var expectedDesignLayerSpeckleObjects = response.Resources;

      //Status.MessageAdded += (s, e) => Debug.WriteLine("Message: " + e.Message);
      //Status.ErrorAdded += (s, e) => Debug.WriteLine("Error: " + e.Message);
      Status.StatusChanged += (s, e) => Debug.WriteLine("Status: " + e.Name);

      var filePath = @"C:\Users\Nic.Burgers\OneDrive - Arup\Issues\Nguyen Le\2D result\shear wall system-seismic v10.1.gwb";

      GSA.GsaApp.gsaProxy.OpenFile(Path.Combine(TestDataDirectory, filePath), false);

      bool failed = false;
      try
      {
        //RECEIVE EVENT #1: Analyis layer
        var sender = new SenderCoordinator();
        GSA.GsaApp.gsaSettings = new Settings() { TargetLayer = GSATargetLayer.Analysis, SeparateStreams = true };
        sender.Initialize("", "", (restApi, apiToken) => new TestSpeckleGSASender(), new Progress<MessageEventArgs>(), new Progress<string>(), new Progress<double>());
        sender.Trigger();

        //Each kit stores their own objects to be sent
        var speckleObjects = GSA.GetSpeckleObjectsFromSenderDictionaries();

        //RECEIVE EVENT #2: Design layer
        sender = new SenderCoordinator();
        GSA.GsaApp.gsaSettings.TargetLayer = GSATargetLayer.Design;
        sender.Initialize("", "", (restApi, apiToken) => new TestSpeckleGSASender(), new Progress<MessageEventArgs>(), new Progress<string>(), new Progress<double>());
        sender.Trigger();

        //Each kit stores their own objects to be sent
        speckleObjects = GSA.GetSpeckleObjectsFromSenderDictionaries();
      }
      catch
      {
        failed = true;
      }
      finally
      {
        GSA.GsaApp.gsaProxy.Close();
      }
      Assert.IsFalse(failed);
    }

  }
}
