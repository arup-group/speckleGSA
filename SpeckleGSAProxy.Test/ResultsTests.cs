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
      Assert.IsTrue(proxy.OpenFile(path, true));
      Assert.IsTrue(proxy.PrepareResults(3, allResultTypes, new List<string> { "A1", "C3", "C10" }));
      Assert.IsTrue(proxy.GetResults("NODE", 13, out var nodeResults));
      Assert.IsNotNull(nodeResults);
      Assert.IsTrue(nodeResults.Keys.Count > 0);
      Assert.IsTrue(proxy.GetResults("EL", 1, out var elem1dResults));
      Assert.IsNotNull(elem1dResults);
      Assert.IsTrue(elem1dResults.Keys.Count > 0);

      proxy.Close();

      var v = GetSpeckleResultHierarchy(nodeResults);
    }

    //For insertion into the Result.Value property
    // [ load case [ result type [ column [ values ] ] ] ]
    private Dictionary<string, Dictionary<string, object>> GetSpeckleResultHierarchy(Dictionary<string, Tuple<List<string>, object[,]>> data, 
      int elementIdColIndex = 0, int caseColIndex = 1)
    {
      var value = new Dictionary<string, Dictionary<string, object>>();

      //Each result type (e.g. "Nodal Velocity")
      foreach (var rt in data.Keys)
      {
        var valueLevel2 = new Dictionary<string, object>();

        for (int r = 0; r < data[rt].Item2.GetLength(0); r++)
        {
          var loadCase = data[rt].Item2[r, caseColIndex].ToString();
          if (!value.Keys.Contains(loadCase))
          {
            value.Add(loadCase, new Dictionary<string, object>());
          }
          if (!value[loadCase].ContainsKey(rt))
          {
            value[loadCase].Add(rt, new Dictionary<string, object>());
          }
          foreach (var c in Enumerable.Range(0, data[rt].Item1.Count()).Except(new[] { elementIdColIndex, caseColIndex }))
          {
            var col = data[rt].Item1[c];
            var val = data[rt].Item2[r, c];
            if (!((Dictionary<string, object>)value[loadCase][rt]).ContainsKey(col))
            {
              ((Dictionary<string, object>)value[loadCase][rt]).Add(col, new List<object>());
            }
            ((List<object>)((Dictionary<string, object>)value[loadCase][rt])[col]).Add(val);
          }
        }
      }

      return value;
    }
  }
}
