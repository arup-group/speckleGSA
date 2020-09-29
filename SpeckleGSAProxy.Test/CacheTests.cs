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
namespace SpeckleGSAProxy.Test
{
  [TestFixture]
  public class CacheTests
  {
    private string TestDataDirectory { get => AppDomain.CurrentDomain.BaseDirectory.TrimEnd(new[] { '\\' }) + @"\..\..\TestData\"; }

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

      cache.Upsert("MEMB.8", 1, "MEMB.8:{speckle_app_id:Slab0}\t1\tSlab 0\tNO_RGB\tSLAB\t1\t6\t36 37 38 39 40 41 42\t0\t0\t5\tMESH\tLINEAR\t0\t0\t0\t0\t0\tACTIVE\tNO\t0\tALL", "abcdefgh", "Slab0", GwaSetCommandType.Set);

      var newIndices = new List<int>
      {
        cache.ResolveIndex("MEMB.8", "Slab1"),
        cache.ResolveIndex("MEMB.8", "Slab2"),
        cache.ResolveIndex("MEMB.8", "Slab3")
      };

      Assert.AreEqual(1, cache.LookupIndices("MEMB.8").Where(i => i.HasValue).Select(i => i.Value).Count());
      Assert.AreEqual(1, cache.LookupIndices("MEMB.8", new[] { "Slab0", "Slab1", "Slab2", "Slab3", "Slab4" }).Where(i => i.HasValue).Select(i => i.Value).Count());

      //Try upserting a latest record before converting a provisional to latest
      Assert.IsTrue(cache.Upsert("MEMB.8", 5, "MEMB.8:{speckle_app_id:Slab4}\t5\tSlab 4\tNO_RGB\tSLAB\t1\t6\t36 37 38 39 40 41 42\t0\t0\t5\tMESH\tLINEAR\t0\t0\t0\t0\t0\tACTIVE\tNO\t0\tALL", "abcdefgh", "Slab4", GwaSetCommandType.Set));
      Assert.AreEqual(2, cache.LookupIndices("MEMB.8").Where(i => i.HasValue).Select(i => i.Value).Count());
      Assert.AreEqual(2, cache.LookupIndices("MEMB.8", new[] { "Slab0", "Slab1", "Slab2", "Slab3", "Slab4" }).Where(i => i.HasValue).Select(i => i.Value).Count());

      //Now convert a provisional to latest and check that the number of records hasn't increased
      Assert.IsTrue(cache.Upsert("MEMB.8", 2, "MEMB.8:{speckle_app_id:Slab1}\t2\tSlab 1\tNO_RGB\tSLAB\t1\t6\t36 37 38 39 40 41 42\t0\t0\t5\tMESH\tLINEAR\t0\t0\t0\t0\t0\tACTIVE\tNO\t0\tALL", "abcdefgh", "Slab1", GwaSetCommandType.Set));
      Assert.AreEqual(3, cache.LookupIndices("MEMB.8").Where(i => i.HasValue).Select(i => i.Value).Count());
      Assert.AreEqual(3, cache.LookupIndices("MEMB.8", new[] { "Slab0", "Slab1", "Slab2", "Slab3", "Slab4" }).Where(i => i.HasValue).Select(i => i.Value).Count());

      //Check that asking to resolve a previously-created provisional index returns that same one
      Assert.AreEqual(3, cache.ResolveIndex("MEMB.8", "Slab2"));

      //Check that the next index recognises (and doesn't re-use) the current provisional indices
      Assert.AreEqual(6, cache.ResolveIndex("MEMB.8"));
    }

    [Test]
    public void ReserveMultipleIndicesSetAt()
    {
      var cache = new GSACache();

      cache.Upsert("LOAD_2D_THERMAL.2", 1, "LOAD_2D_THERMAL.2\tGeneral\tG6\t3\tDZ\t239\t509", "abcdefgh", "", GwaSetCommandType.SetAt);

      cache.ResolveIndex("LOAD_2D_THERMAL.2");
      cache.ResolveIndex("LOAD_2D_THERMAL.2");

      //Try upserting a latest record before converting a provisional to latest
      Assert.IsTrue(cache.Upsert("LOAD_2D_THERMAL.2", 4, "LOAD_2D_THERMAL.2\tGeneral\tG7\t3\tDZ\t239\t509", "abcdefgh", "Slab4", GwaSetCommandType.Set));
      Assert.AreEqual(2, cache.LookupIndices("LOAD_2D_THERMAL.2").Where(i => i.HasValue).Select(i => i.Value).Count());

      //Now convert a provisional to latest and check that the number of records hasn't increased
      Assert.IsTrue(cache.Upsert("LOAD_2D_THERMAL.2", 3, "LOAD_2D_THERMAL.2\tGeneral\tG6 G7 G8 G9 G10\t3\tDZ\t239\t509", "abcdefgh", "Slab1", GwaSetCommandType.Set));
      Assert.AreEqual(3, cache.LookupIndices("LOAD_2D_THERMAL.2").Where(i => i.HasValue).Select(i => i.Value).Count());

      //Check that the next index recognises (and doesn't re-use) the current provisional indices
      Assert.AreEqual(5, cache.ResolveIndex("LOAD_2D_THERMAL.2"));
    }

    [Test]
    public void ExpandTestCasesTest()
    {
      var cache = new GSACache();

      var gwa = new List<string>
      {
        "ANAL.1\t1\tSW\t1\tL1", 
        "ANAL.1\t3\tLL\t1\tL3", 
        "ANAL.1\t4\tDynamic : Mode 1\t2\tM1", 
        "ANAL.1\t5\tDynamic: Mode 2\t2\tM2", 
        "ANAL.1\t6\tDynamic: Mode 3\t2\tM3", 
        "ANAL.1\t7\tDynamic: Mode 4\t2\tM4", 
        "ANAL.1\t8\tDynamic: Mode 5\t2\tM5", 
        "ANAL.1\t9\tDynamic: Mode 6\t2\tM6", 
        "ANAL.1\t10\tDynamic: Mode 7\t2\tM7", 
        "ANAL.1\t11\tDynamic: Mode 8\t2\tM8", 
        "ANAL.1\t12\tDynamic: Mode 9\t2\tM9", 
        "ANAL.1\t13\tDynamic: Mode 10\t2\tM10", 
        "COMBINATION.1\t1\tULS\t1.35A1 + 1.35A2 + 1.5A3\t\t", 
        "COMBINATION.1\t2\tSLS\tA1 + A2 + 0.7A3\t\t"
      };

      foreach (var g in gwa)
      {
        var gSplit = g.Split(new string[] { "\t" }, StringSplitOptions.None);
        var keyword = gSplit.First();
        int.TryParse(gSplit[1], out int index);
        cache.Upsert(keyword, index, g);
      }

      var cases = cache.ExpandLoadCasesAndCombinations("all");

      GSA.gsaProxy.Close();

      Assert.IsNotNull(cases);
      Assert.IsTrue(cases.Count() > 0);
    }
  }
}
