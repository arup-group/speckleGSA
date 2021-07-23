using CsvHelper;
using NUnit.Framework;
using SpeckleGSA;
using SpeckleGSAInterfaces;
using SpeckleGSAProxy.Results;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
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
      
      var cases = new List<string> { "A1", "C3", "C10" };
      var allResultTypes = new List<ResultType> { ResultType.NodalDisplacements, ResultType.NodalVelocity, ResultType.Element1dDisplacement };
      var path = Path.Combine(TestDataDirectory, "200602_timberPlint 3d model.gwb");
      Assert.IsTrue(proxy.OpenFile(path, true));
      Assert.IsTrue(proxy.PrepareResults(allResultTypes, 3));

      Assert.IsTrue(proxy.LoadResults(ResultGroup.Node, cases, new List<int> { 13 }));
      Assert.IsTrue(proxy.LoadResults(ResultGroup.Element1d, cases, new List<int> { 1 }));

      //Assert.IsTrue(proxy.GetResults("NODE", 13, out var nodeResults));
      Assert.IsTrue(proxy.GetResultHierarchy(ResultGroup.Node, 13, out var nodeResults));
      Assert.IsNotNull(nodeResults);
      Assert.IsTrue(nodeResults.Keys.Count > 0);
      //Assert.IsTrue(proxy.GetResults("EL", 1, out var elem1dResults));
      Assert.IsTrue(proxy.GetResultHierarchy(ResultGroup.Element1d, 1, out var elem1dResults));
      Assert.IsNotNull(elem1dResults);
      Assert.IsTrue(elem1dResults.Keys.Count > 0);

      proxy.Close();

      //var v = GetSpeckleResultHierarchy(nodeResults);
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

    [TestCase(@"C:\Temp", true)]
    public void CsvHelpersTestNullValues(string dir, bool parallel)
    {
      var startTime = DateTime.Now;

      var cases = new List<string> { "A1" };

      var unitData = new Dictionary<SpeckleGSAResultsHelper.ResultUnitType, double>() { { SpeckleGSAResultsHelper.ResultUnitType.Length, 1 }, { SpeckleGSAResultsHelper.ResultUnitType.Force, 1 } };

      var context = new List<ResultsProcessorBase>()
      {
        new ResultsAssemblyProcessor(Path.Combine(dir, @"result_assembly\result_assembly.csv"), unitData, cases: cases)
      };

      var hierarchiesByGroup = new Dictionary<ResultGroup, Dictionary<int, object>>();
      var hierarchiesLock = new object();
      int numAdded = 0;

      foreach (var processor in context)
      {
        hierarchiesByGroup.Add(processor.Group, new Dictionary<int, object>());

        processor.LoadFromFile(true);
      }
        
      foreach (var processor in context)
      {
          var elems = processor.ElementIds;

        foreach (var e in elems.Take(10))
        {
          var h = processor.GetResultHierarchy(e);
          lock (hierarchiesLock)
          {
            hierarchiesByGroup[processor.Group][e] = h;
            numAdded++;
          }
        }
      }
      
      TimeSpan duration = DateTime.Now - startTime;
      var durationString = duration.ToString(@"hh\:mm\:ss");
      Console.WriteLine("Duration of test: " + durationString);
    }

    [TestCase(@"C:\Nicolaas\Repo\speckleGSA-github\SpeckleGSAUI\bin\Debug\GSAExport", true)]
    //[TestCase(@"C:\Temp\result_elem_2d.csv", true)]
    public void CsvHelpersTest3(string dir, bool parallel)
    {
      var startTime = DateTime.Now;

      var cases = new List<string> { "A1" };

      var unitData = new Dictionary<SpeckleGSAResultsHelper.ResultUnitType, double>() { { SpeckleGSAResultsHelper.ResultUnitType.Length, 1 }, { SpeckleGSAResultsHelper.ResultUnitType.Force, 1 } };

      var context = new List<ResultsProcessorBase>()
      {
        new ResultsNodeProcessor(Path.Combine(dir, @"result_node\result_node.csv"), unitData, cases: cases),
        new Results1dProcessor(Path.Combine(dir, @"result_elem_1d\result_elem_1d.csv"), unitData, cases: cases),
        new Results2dProcessor(Path.Combine(dir, @"result_elem_2d\result_elem_2d.csv"), unitData, cases: cases),
        new ResultsAssemblyProcessor(Path.Combine(dir, @"result_assembly\result_assembly.csv"), unitData, cases: cases)
      };

      var hierarchiesByGroup = new Dictionary<ResultGroup, Dictionary<int, object>>();
      var hierarchiesLock = new object();
      int numAdded = 0;

      foreach (var processor in context)
      {
        hierarchiesByGroup.Add(processor.Group, new Dictionary<int, object>());

        processor.LoadFromFile(true);
      }

      foreach (var processor in context)
      {
        var elems = processor.ElementIds;

        foreach (var e in elems.Take(10))
        {
          var h = processor.GetResultHierarchy(e);
          lock (hierarchiesLock)
          {
            hierarchiesByGroup[processor.Group][e] = h;
            numAdded++;
          }
        }
      }

      TimeSpan duration = DateTime.Now - startTime;
      var durationString = duration.ToString(@"hh\:mm\:ss");
      Console.WriteLine("Duration of test: " + durationString);
    }
  }
}
