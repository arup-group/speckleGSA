using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Messaging;
using System.Threading.Tasks;

namespace SpeckleGSAProxy.Results
{
  public class ExportCsvResultsTable : ExportCsvTable
  {
    private enum KnownResultCsvHeaders
    {
      ElementId = 1,
      LoadCase = 2
    }
    //Optimisations designed specifically for results tables
    private readonly ConcurrentDictionary<string, ConcurrentBag<int>> rowIndicesByLoadCase = new ConcurrentDictionary<string, ConcurrentBag<int>>();
    private readonly ConcurrentDictionary<int, ConcurrentBag<int>> rowIndicesByElemId = new ConcurrentDictionary<int, ConcurrentBag<int>>();
    private readonly ConcurrentDictionary<KnownResultCsvHeaders, int> colIndicesByHeader = new ConcurrentDictionary<KnownResultCsvHeaders, int>();

    //"A1", "C2", etc - empty value here will cause the entire file to be loaded
    //public List<string> LoadCases;

    public string LoadCaseField;
    public string ElemIdField;

    public ExportCsvResultsTable(string tableName, string loadCaseField, string elemIdField)
      : base(tableName)
    {
      this.LoadCaseField = loadCaseField;
      this.ElemIdField = elemIdField;
    }

    public ExportCsvResultsTable(string tableName, string loadCaseField)
      : base(tableName)
    {
      this.LoadCaseField = loadCaseField;
    }

    
    public bool Query(IEnumerable<string> columns, IEnumerable<string> loadCases, out object[,] results, IEnumerable<int> elemIds = null)
    {
      results = null; //default
      ConcurrentDictionary<int, object[]> tempValues = new ConcurrentDictionary<int, object[]>();

      if (!GetQueryHeaderIndices(columns, out var queryHeaderIndices)
        || !GetQueryRowIndices(loadCases, out var queryRowIndices, elemIds))
      {
        return false;
      }

      //foreach (var currRowIndex in queryRowIndices)
      Parallel.ForEach(queryRowIndices, currRowIndex =>
      {
        var rowData = Values[currRowIndex];

        var tempRow = new object[queryHeaderIndices.Count()];
        var i = 0;
        foreach (var hi in queryHeaderIndices)
        {
          tempRow[i++] = rowData[hi];
        }

        tempValues.TryAdd(currRowIndex, tempRow);
      }
      );

      results = new object[tempValues.Count(), queryHeaderIndices.Count()];

      var r = 0;
      var sortedRowIndices = tempValues.Keys.OrderBy(ri => ri).ToList();
      foreach (var ri in sortedRowIndices)
      {
        for (int j = 0; j < queryHeaderIndices.Count(); j++)
        {
          results[r, j] = tempValues[ri][j];
        }
        r++;
      }

      return true;
    }

    private bool GetQueryHeaderIndices(IEnumerable<string> columns, out List<int> queryHeaderIndices)
    {
      queryHeaderIndices = new List<int>();
      foreach (var c in columns)
      {
        if (Headers.ContainsKey(c))
        {
          queryHeaderIndices.Add(Headers[c]);
        }
        /*
        for (int i = 0; i < Headers.Length; i++)
        {
          if (Headers[i].Equals(c, StringComparison.InvariantCultureIgnoreCase))
          {
            queryHeaderIndices.Add(i);
          }
        }
        */
      }
      return (queryHeaderIndices.Count() > 0);
    }

    private bool GetQueryRowIndices(IEnumerable<string> loadCases, out List<int> queryRowIndices, IEnumerable<int> elemIds = null)
    {
      queryRowIndices = new List<int>();
      if (elemIds == null)
      {
        foreach (var lc in loadCases)
        {
          var lcBase = lc.Split('m').First();
          if (rowIndicesByLoadCase.Keys.Any(k => k.Split('m').First().Equals(lcBase)))
          //if (rowIndicesByLoadCase.ContainsKey(lc))
          {
            foreach (var ilc in rowIndicesByLoadCase[lc])
            {
              if (!queryRowIndices.Contains(ilc))
              {
                queryRowIndices.Add(ilc);
              }
            }
          }
        }
      }
      else
      {
        var relevantByElemId = new HashSet<int>();
        if (elemIds != null)
        {
          foreach (var ei in elemIds)
          {
            if (rowIndicesByElemId.ContainsKey(ei))
            {
              foreach (var iei in rowIndicesByElemId[ei])
              {
                relevantByElemId.Add(iei);
              }
            }
          }
        }


        //var relevantByLoadCase = new SortedSet<int>();
        var tempIndices = new List<int>();
        foreach (var lc in loadCases)
        {
          var lcBase = lc.Split('m').First();
          var matching = rowIndicesByLoadCase.Keys.Where(k => k.Split('m').First().Equals(lcBase)).SelectMany(k => rowIndicesByLoadCase[k]);
          if (matching.Count() > 0)
          //if (rowIndicesByLoadCase.ContainsKey(lc))
          {
            foreach (var ilc in matching)
            //foreach (var ilc in rowIndicesByLoadCase[lc])
            {
              //relevantByLoadCase.Add(ilc);
              if (relevantByElemId.Contains(ilc))
              {
                tempIndices.Add(ilc);
              }
            }
          }
        }

        queryRowIndices = tempIndices.OrderBy(i => i).ToList();
        /*
        var tempIndices = (from i in relevantByLoadCase.Intersect(relevantByElemId) select i);
        foreach (var ti in tempIndices)
        {
          queryRowIndices.Add(ti);
        }
        */
      }
      return (queryRowIndices.Count() > 0);
    }

    private int? HeaderIndexOf(string headerToFind)
    {
      return (Headers.ContainsKey(headerToFind) ? (int?) Headers[headerToFind] : null);
      /*
      for (var i = 0; i < Headers.Count(); i++)
      {
        if (Headers[i].Equals(headerToFind, StringComparison.InvariantCultureIgnoreCase))
        {
          return i;
        }
      }
      return null;
      */
    }

    private void SetSpecifiedColumnIndices()
    {
      if (!colIndicesByHeader.ContainsKey(KnownResultCsvHeaders.LoadCase) && !string.IsNullOrEmpty(LoadCaseField))
      {
        var index = HeaderIndexOf(LoadCaseField);
        if (index.HasValue)
        {
          colIndicesByHeader.TryAdd(KnownResultCsvHeaders.LoadCase, index.Value);
        }
      }
      if (!colIndicesByHeader.ContainsKey(KnownResultCsvHeaders.ElementId) && !string.IsNullOrEmpty(ElemIdField))
      {
        var index = HeaderIndexOf(ElemIdField);
        if (index.HasValue)
        {
          colIndicesByHeader.TryAdd(KnownResultCsvHeaders.ElementId, index.Value);
        }
      }
    }

    protected override bool AddRow(int rowIndex, List<string> rowData)
    {
      //Do this on first row only - it's assumed the first row will always contain headers (column names)
      if (rowIndex == 0)
      {
        SetSpecifiedColumnIndices();
      }

      base.AddRow(rowIndex, rowData);

      if (colIndicesByHeader.ContainsKey(KnownResultCsvHeaders.LoadCase))
      {
        var loadCase = rowData[colIndicesByHeader[KnownResultCsvHeaders.LoadCase]];
        if (!rowIndicesByLoadCase.ContainsKey(loadCase))
        {
          rowIndicesByLoadCase.TryAdd(loadCase, new ConcurrentBag<int>());
        }
        rowIndicesByLoadCase[loadCase].Add(rowIndex);
      }

      if (colIndicesByHeader.ContainsKey(KnownResultCsvHeaders.ElementId))
      {
        if (int.TryParse(rowData[colIndicesByHeader[KnownResultCsvHeaders.ElementId]], out int elemId))
        {
          if (!rowIndicesByElemId.ContainsKey(elemId))
          {
            rowIndicesByElemId.TryAdd(elemId, new ConcurrentBag<int>());
          }
          rowIndicesByElemId[elemId].Add(rowIndex);
        }
      }

      return true;
    }
  }
}
