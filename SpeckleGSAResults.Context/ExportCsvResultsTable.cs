using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SpeckleGSAResults
{
  internal class ExportCsvResultsTable : ExportCsvTable
  {
    private readonly ConcurrentDictionary<string, List<int>> loadCaseIndices = new ConcurrentDictionary<string, List<int>>();
    private int? loadCaseFieldIndex = null;

    //"A1", "C2", etc - empty value here will cause the entire file to be loaded
    public List<string> LoadCases;
    public string LoadCaseField;

    public ExportCsvResultsTable(string tableName, string loadCaseField)
      : base(tableName)
    {
      this.LoadCaseField = loadCaseField;
    }

    protected override bool AddRow(int rowIndex, List<string> rowData)
    {
      base.AddRow(rowIndex, rowData);

      if (!loadCaseFieldIndex.HasValue && !string.IsNullOrEmpty(LoadCaseField))
      {
        for (var i = 0; i < Headers.Count(); i++)
        {
          if (Headers[i].Equals(LoadCaseField, StringComparison.InvariantCultureIgnoreCase))
          {
            this.loadCaseFieldIndex = i;
            break;
          }
        }
      }

      if (loadCaseFieldIndex.HasValue)
      {
        var loadCase = "A" + rowData[loadCaseFieldIndex.Value];
        if (!loadCaseIndices.ContainsKey(loadCase))
        {
          loadCaseIndices.TryAdd(loadCase, new List<int>());
        }
        loadCaseIndices[loadCase].Add(rowIndex);
      }

      return true;
    }
  }
}
