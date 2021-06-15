using NUnit.Framework;
using SpeckleGSA;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSAProxy.Test
{
  [TestFixture]
  public class ResultsTests
  {
    private string TestDataDirectory { get => AppDomain.CurrentDomain.BaseDirectory.TrimEnd(new[] { '\\' }) + @"\..\..\TestData\"; }

    [Test]
    public void ResultsTest()
    {
      GSA.Reset();
      var proxy = new GSAProxy();
      GSA.App = new TestAppResources(proxy, new Settings() { Units = "m", TargetLayer = SpeckleGSAInterfaces.GSATargetLayer.Analysis });
      GSA.Init("");

      GSA.App.Settings.NodalResults = new Dictionary<string, SpeckleGSAInterfaces.IGSAResultParams>() { { "Nodal Displacements", null }, { "Nodal Velocity", null } };
      GSA.App.Settings.Element1DResults = new Dictionary<string, SpeckleGSAInterfaces.IGSAResultParams>() { { "1D Element Displacement", null } };

      var allResultTypes = new List<string>();
      allResultTypes.AddRange(GSA.App.Settings.NodalResults.Keys);
      allResultTypes.AddRange(GSA.App.Settings.Element1DResults.Keys);

      var path = Path.Combine(TestDataDirectory, "200602_timberPlint 3d model.gwb");
      proxy.OpenFile(path, false);
      Assert.IsTrue(proxy.PrepareResults(3, allResultTypes, new List<string> { "A1", "C3", "C10" }));
      Assert.IsTrue(proxy.GetResults("NODE", 13, out var nodeResults));
      Assert.IsNotNull(nodeResults);
      Assert.IsTrue(nodeResults.Keys.Count > 0);
      Assert.IsTrue(proxy.GetResults("EL", 1, out var elem1dResults));
      Assert.IsNotNull(elem1dResults);
      Assert.IsTrue(elem1dResults.Keys.Count > 0);

      proxy.Close();
    }
  }
}
