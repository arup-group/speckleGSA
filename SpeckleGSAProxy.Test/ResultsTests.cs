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

      GSA.App.Settings.NodalResults = new Dictionary<string, IGSAResultParams>() { { "Nodal Displacements", null }, { "Nodal Velocity", null } };
      GSA.App.Settings.Element1DResults = new Dictionary<string, IGSAResultParams>() { { "1D Element Displacement", null } };

      var allResultTypes = new List<string>();
      allResultTypes.AddRange(GSA.App.Settings.NodalResults.Keys);
      allResultTypes.AddRange(GSA.App.Settings.Element1DResults.Keys);
      var cases = new List<string> { "A1", "C3", "C10" };

      var path = Path.Combine(TestDataDirectory, "200602_timberPlint 3d model.gwb");
      Assert.IsTrue(proxy.OpenFile(path, true));
      Assert.IsTrue(proxy.PrepareResults(3));

      Assert.IsTrue(proxy.LoadResults(GSA.App.Settings.NodalResults.Keys.ToList(), cases, new List<int> { 13 }));
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

    [TestCase(@"C:\Nicolaas\Repo\speckleGSA-github\SpeckleGSAUI\bin\Debug\GSAExport\result_elem_2d\result_elem_2d.csv")]
    public void LoadFileTest(string filePath)
    {
      var startTime = DateTime.Now;

      var context = new ExportCsvResultsTable("", ResultCsvGroup.Element2d, "case_id", "id", new List<string>());
      context.LoadFromFile(filePath);

      TimeSpan duration = DateTime.Now - startTime;
      var durationString = duration.ToString(@"hh\:mm\:ss");

    }

    [TestCase(@"C:\Nicolaas\Repo\speckleGSA-github\SpeckleGSAUI\bin\Debug\GSAExport\result_elem_1d\result_elem_1d.csv")]
    public void LoadFileTestControl(string filePath)
    {
      var startTime = DateTime.Now;

      var lines = new List<string>();

      var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
      var sr = new StreamReader(fs);

      string line;
      while ((line = sr.ReadLine()) != null)
      {
        lines.Add(line);
      }

      TimeSpan duration = DateTime.Now - startTime;
      var durationString = duration.ToString(@"hh\:mm\:ss");

    }

    [TestCase(@"C:\Nicolaas\Repo\speckleGSA-github\SpeckleGSAUI\bin\Debug\GSAExport\result_elem_1d\result_elem_1d.csv")]
    public void CsvHelpersTest(string filePath)
    {
      var startTime = DateTime.Now;

      var allFields = new List<string>();

      foreach (var rt in GSAProxy.resultTypeSpecs[ResultCsvGroup.Element1d].ResultTypeCsvColumnMap.Keys)
      {
        foreach (var importedField in GSAProxy.resultTypeSpecs[ResultCsvGroup.Element1d].ResultTypeCsvColumnMap[rt].FileCols)
        {
          if (!allFields.Contains(importedField.Value.FileCol))
          {
            allFields.Add(importedField.Value.FileCol);
          }
        }
      }


      var context = new ExportCsvResultsTable("", ResultCsvGroup.Element1d, "case_id", "id", allFields);
      context.LoadFromFile(filePath);

      TimeSpan duration = DateTime.Now - startTime;
      var durationString = duration.ToString(@"hh\:mm\:ss");

    }

    [TestCase(@"C:\Nicolaas\Repo\speckleGSA-github\SpeckleGSAUI\bin\Debug\GSAExport\result_elem_2d\result_elem_2d.csv", true)]
    //[TestCase(@"C:\Temp\result_elem_2d.csv")]
    public void CsvHelpersTest2(string filePath, bool parallel)
    {
      var startTime = DateTime.Now;

      var cases = new List<string> { "A1", "A2", "A3" };

      //var context = new ResultsTest.Results2dProcessor(GSAProxy.resultTypeSpecs[ResultCsvGroup.Element2d], filePath, new List<string>() { "A1", "A2" }, new List<int>() { 1, 2, 3 });
      var context = new ResultsTest.Results2dProcessor(GSAProxy.resultTypeSpecs[ResultCsvGroup.Element2d], filePath, cases);
      context.LoadFromFile(true);

      var hierarchies1 = context.GetHierarchy(1, "A1");
      var hierarchies2 = context.GetHierarchy(2, "A1");
      var hierarchies3 = context.GetHierarchy(3, "A1");
      var hierarchies4 = context.GetHierarchy(1, "A2");
      var hierarchies5 = context.GetHierarchy(2, "A2");
      var hierarchies6 = context.GetHierarchy(3, "A2");

      TimeSpan duration = DateTime.Now - startTime;
      var durationString = duration.ToString(@"hh\:mm\:ss");
      Console.WriteLine("Duration of test: " + durationString);
    }

    [TestCase(@"C:\Nicolaas\Repo\speckleGSA-github\SpeckleGSAUI\bin\Debug\GSAExport\result_elem_2d\result_elem_2d.csv", true)]
    //[TestCase(@"C:\Temp\result_elem_2d.csv", true)]
    public void CsvHelpersTest3(string filePath, bool parallel)
    {
      var startTime = DateTime.Now;

      var cases = new List<string> { "A1", "A2", "A3" };

      var context = new ResultsTest.Results2dProcessor2(GSAProxy.resultTypeSpecs[ResultCsvGroup.Element2d], filePath);
      context.LoadFromFile(true);

      var hierarchies1 = context.GetHierarchy(1, "A1");
      
      TimeSpan duration = DateTime.Now - startTime;
      var durationString = duration.ToString(@"hh\:mm\:ss");
      Console.WriteLine("Duration of test: " + durationString);
    }
  }
}