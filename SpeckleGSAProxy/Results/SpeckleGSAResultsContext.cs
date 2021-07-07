using SpeckleGSAInterfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSAProxy.Results
{
  internal class SpeckleGSAResultsContext : IGSAResultsContext
  {
    private readonly ConcurrentDictionary<ResultCsvGroup, ExportCsvResultsTable> resultsTables = new ConcurrentDictionary<ResultCsvGroup, ExportCsvResultsTable>();
    private readonly ConcurrentBag<ExportCsvTable> dataTables = new ConcurrentBag<ExportCsvTable>();
    public string ResultsDir { get; private set; }
    public List<string> ResultTableNames { get => resultsTables.Keys.Select(rtk => resultsTables[rtk].TableName).ToList(); }
    public List<ResultCsvGroup> ResultTableGroups { get => resultsTables.Keys.ToList(); }
    public SpeckleGSAResultsContext(string resultsDir)
    {
      this.ResultsDir = resultsDir;
    }

    public bool Query(ResultCsvGroup group, IEnumerable<string> columns, string loadCase, out object[,] results, int? elemId = null)
    {
      results = null; //default

      if (!resultsTables.ContainsKey(group) || resultsTables[group] == null)
      {
        return false;
      }

      return resultsTables[group].Query(columns, new[] { loadCase }, out results, elemId.HasValue ? new[] { elemId.Value }: null);
    }

    public bool Query(ResultCsvGroup group, IEnumerable<string> columns, IEnumerable<string> loadCases, out object[,] results, IEnumerable<int> elemIds = null)
    {
      results = null; //default

      if (!resultsTables.ContainsKey(group) || resultsTables[group] == null)
      {
        return false;
      }

      return resultsTables[group].Query(columns, loadCases, out results, elemIds);
    }

    public bool Clear(ResultCsvGroup group = ResultCsvGroup.Unknown)
    {
      if (group == ResultCsvGroup.Unknown)
      {
        foreach (var g in resultsTables.Keys)
        {
          resultsTables[g].Clear();
        }
      }
      else if (resultsTables.ContainsKey(group))
      {
        resultsTables[group].Clear();
      }
      else
      {
        return false;
      }

      return true;
    }

    public bool ImportResultsFromFile(string fileName, ResultCsvGroup group, string caseIdField, string elemIdField, List<string> otherFields, 
      List<string> cases, List<int> elemIds)
    {
      var fn = Path.HasExtension(fileName) ? fileName : fileName + ".csv";
      var filePath = fn.Contains(":") ? fn : Path.Combine(ResultsDir, fn);

      if (!File.Exists(filePath))
      {
        return false;
      }

      var tableName = Path.GetFileNameWithoutExtension(filePath);
      var isResultsTable = tableName.StartsWith("result_", StringComparison.InvariantCultureIgnoreCase);

      if (isResultsTable)
      {
        var t = new ExportCsvResultsTable(tableName, group, caseIdField, elemIdField, otherFields);
        if (t.LoadFromFile(filePath, cases, elemIds))
        {
          resultsTables.TryAdd(group, (ExportCsvResultsTable)t);
        }
      }
      else
      {
        var t = new ExportCsvTable(tableName);
        t.LoadFromFile(filePath);
        //TO DO: add to local data tables
        return false;
      }
      return true;
    }
  }
}
