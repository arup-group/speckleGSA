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
    [Test]
    public void ReserveMultipleIndicesSet()
    {
      var cache = new GSACache();

      cache.Upsert("MEMB.7", 1, "MEMB.7:{speckle_app_id:Slab0}\t1\tSlab 0\tNO_RGB\tSLAB\t1\t6\t36 37 38 39 40 41 42\t0\t0\t5\tMESH\tLINEAR\t0\t0\t0\t0\t0\tACTIVE\tNO\t0\tALL", "abcdefgh", "Slab0", GwaSetCommandType.Set);

      var newIndices = new List<int>
      {
        cache.ResolveIndex("MEMB.7", "Slab1"),
        cache.ResolveIndex("MEMB.7", "Slab2"),
        cache.ResolveIndex("MEMB.7", "Slab3")
      };

      Assert.AreEqual(1, cache.LookupIndices("MEMB.7").Where(i => i.HasValue).Select(i => i.Value).Count());
      Assert.AreEqual(1, cache.LookupIndices("MEMB.7", new[] { "Slab0", "Slab1", "Slab2", "Slab3", "Slab4" }).Where(i => i.HasValue).Select(i => i.Value).Count());

      //Try upserting a latest record before converting a provisional to latest
      Assert.IsTrue(cache.Upsert("MEMB.7", 5, "MEMB.7:{speckle_app_id:Slab4}\t5\tSlab 4\tNO_RGB\tSLAB\t1\t6\t36 37 38 39 40 41 42\t0\t0\t5\tMESH\tLINEAR\t0\t0\t0\t0\t0\tACTIVE\tNO\t0\tALL", "abcdefgh", "Slab4", GwaSetCommandType.Set));
      Assert.AreEqual(2, cache.LookupIndices("MEMB.7").Where(i => i.HasValue).Select(i => i.Value).Count());
      Assert.AreEqual(2, cache.LookupIndices("MEMB.7", new[] { "Slab0", "Slab1", "Slab2", "Slab3", "Slab4" }).Where(i => i.HasValue).Select(i => i.Value).Count());

      //Now convert a provisional to latest and check that the number of records hasn't increased
      Assert.IsTrue(cache.Upsert("MEMB.7", 2, "MEMB.7:{speckle_app_id:Slab1}\t2\tSlab 1\tNO_RGB\tSLAB\t1\t6\t36 37 38 39 40 41 42\t0\t0\t5\tMESH\tLINEAR\t0\t0\t0\t0\t0\tACTIVE\tNO\t0\tALL", "abcdefgh", "Slab1", GwaSetCommandType.Set));
      Assert.AreEqual(3, cache.LookupIndices("MEMB.7").Where(i => i.HasValue).Select(i => i.Value).Count());
      Assert.AreEqual(3, cache.LookupIndices("MEMB.7", new[] { "Slab0", "Slab1", "Slab2", "Slab3", "Slab4" }).Where(i => i.HasValue).Select(i => i.Value).Count());

      //Check that asking to resolve a previously-created provisional index returns that same one
      Assert.AreEqual(3, cache.ResolveIndex("MEMB.7", "Slab2"));

      //Check that the next index recognises (and doesn't re-use) the current provisional indices
      Assert.AreEqual(6, cache.ResolveIndex("MEMB.7"));
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
  }
}
