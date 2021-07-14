using CsvHelper;
using SpeckleGSAInterfaces;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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

    //These dictionaries don't need locks as they're (at least at time of writing this comment) being written-to serially during file load, and during
    //querying, there is no writing going on, only reading
    private readonly Dictionary<int, List<int>> rowIndicesByElemId = new Dictionary<int, List<int>>();
    private readonly Dictionary<string, List<int>> rowIndicesByTrimmedLoadCase = new Dictionary<string, List<int>>();
    private readonly Dictionary<string, List<int>> rowIndicesByFullLoadCase = new Dictionary<string, List<int>>();

    private readonly Dictionary<KnownResultCsvHeaders, int> colIndicesByHeader = new Dictionary<KnownResultCsvHeaders, int>();

    //"A1", "C2", etc - empty value here will cause the entire file to be loaded
    //public List<string> LoadCases;

    public string LoadCaseField;
    public string ElemIdField;
    public List<string> OtherFields;
    public ResultCsvGroup Group;

    public ExportCsvResultsTable(string tableName, ResultCsvGroup group, string loadCaseField, string elemIdField, List<string> otherFields) 
      : base(tableName)
    {
      this.fields = (new List<string>() { loadCaseField, elemIdField }).Concat(otherFields).ToList();

      this.LoadCaseField = loadCaseField;
      this.ElemIdField = elemIdField;
      this.OtherFields = otherFields;
      this.colIndicesByHeader.Add(KnownResultCsvHeaders.LoadCase, 0);
      this.colIndicesByHeader.Add(KnownResultCsvHeaders.ElementId, 1);
      this.Group = group;
    }

    public ExportCsvResultsTable(string tableName, ResultCsvGroup group, string loadCaseField, List<string> otherFields)
      : base(tableName)
    {
      this.LoadCaseField = loadCaseField;
      this.OtherFields = otherFields;
      this.fields = (new List<string>() { loadCaseField }).Concat(otherFields).ToList();
      this.colIndicesByHeader.Add(KnownResultCsvHeaders.LoadCase, 0);
      this.Group = group;
    }

    public bool LoadFromFile(string filePath, List<string> cases, List<int> elemIds)
    {
      var delimiters = new HashSet<char> { ',' };

      var elemIdHashes = new HashSet<int>(elemIds);
      var loadCaseHashes = new HashSet<string>(cases);

      var reader = new StreamReader(filePath);

      using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
      {
        var records = new List<object>();
        csv.Read();
        csv.ReadHeader();

        var fileHeaders = csv.HeaderRecord.ToList();
        Headers = new Dictionary<string, int>();
        for (int f = 0; f < fields.Count; f++)
        {
          if (fileHeaders.Contains(fields[f]))
          {
            Headers.Add(fields[f], f);
          }
        }
        numHeaders = Headers.Count();

        int rowIndex = 0;
        while (csv.Read())
        {
          var caseId = csv.GetField<string>("case_id");
          var elemId = csv.GetField<int>("id");

          if (elemIdHashes.Contains(elemId) && loadCaseHashes.Contains(caseId))
          {
            var vals = new object[fields.Count()];
            for (int c = 0; c < fields.Count(); c++)
            {
              if (fields[c].Equals("case_id"))
              {
                vals[c] = caseId;
              }
              else if (fields[c].Equals("id"))
              {
                vals[c] = elemId;
              }
              else
              {
                vals[c] = csv.GetField<double?>(fields[c]);
              }
            }
            if (!AddRow(rowIndex, vals.ToList()))
            {
              ErrRowIndices.Add(rowIndex);
            }
          }
          rowIndex++;
        }
      }

      lock (valuesLock)
      {
        NumRows = Values.Keys.Count();
      }

      reader.Close();
      return true;
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
        object[] rowData;
        lock (valuesLock)
        {
          rowData = Values[currRowIndex];
        }

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
          List<int> loadCaseIndices = null;
          if (rowIndicesByFullLoadCase.ContainsKey(lc))
          {
            loadCaseIndices = rowIndicesByFullLoadCase[lc];
          }
          else if (rowIndicesByTrimmedLoadCase.ContainsKey(lc))
          {
            loadCaseIndices = rowIndicesByTrimmedLoadCase[lc];
          }

          if (loadCaseIndices != null)
          {
            foreach (var ilc in loadCaseIndices)
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

        if (relevantByElemId.Count() == 0)
        {
          return false;
        }

        var tempIndices = new List<int>();
        foreach (var lc in loadCases)
        {
          List<int> matching = null;
          
          if (rowIndicesByFullLoadCase.ContainsKey(lc))
          {
            matching = rowIndicesByFullLoadCase[lc];
          }
          else if (char.IsDigit(lc.Last()))
          {
            var loadCaseTrimmed = lc;  //Default, could be overridden below
            var dividingIndex = lc.IndexOf('m');
            if (dividingIndex >= 0)
            {
              loadCaseTrimmed = lc.Substring(0, dividingIndex);
            }
            if (rowIndicesByTrimmedLoadCase.ContainsKey(loadCaseTrimmed))
            {
              matching = rowIndicesByTrimmedLoadCase[loadCaseTrimmed];
            }
          }

          if (matching != null && matching.Count() > 0)
          {
            foreach (var ilc in matching)
            {
              if (relevantByElemId.Contains(ilc))
              {
                tempIndices.Add(ilc);
              }
            }
          }
        }

        queryRowIndices = tempIndices.OrderBy(i => i).ToList();
      }
      return (queryRowIndices.Count() > 0);
    }

    private int? HeaderIndexOf(string headerToFind)
    {
      return (Headers.ContainsKey(headerToFind) ? (int?)Headers[headerToFind] : null);
    }

    protected override bool AddRow(int rowIndex, List<object> rowData)
    {
      base.AddRow(rowIndex, rowData);

      if (colIndicesByHeader.ContainsKey(KnownResultCsvHeaders.LoadCase))
      {
        var loadCase = rowData[colIndicesByHeader[KnownResultCsvHeaders.LoadCase]].ToString();

        if (!rowIndicesByFullLoadCase.ContainsKey(loadCase))
        {
          rowIndicesByFullLoadCase.Add(loadCase, new List<int>());
        }
        rowIndicesByFullLoadCase[loadCase].Add(rowIndex);

        var loadCaseTrimmed = loadCase;  //Default, could be overridden below
        if (!char.IsDigit(loadCase.Last()))
        {
          var dividingIndex = loadCase.IndexOf('m');
          if (dividingIndex >= 0)
          {
            loadCaseTrimmed = loadCase.Substring(0, dividingIndex);
          }

          if (!rowIndicesByTrimmedLoadCase.ContainsKey(loadCaseTrimmed))
          {
            rowIndicesByTrimmedLoadCase.Add(loadCaseTrimmed, new List<int>());
          }
          rowIndicesByTrimmedLoadCase[loadCaseTrimmed].Add(rowIndex);
        }
      }

      if (colIndicesByHeader.ContainsKey(KnownResultCsvHeaders.ElementId))
      {
        int elemId = (int)rowData[colIndicesByHeader[KnownResultCsvHeaders.ElementId]];
        if (!rowIndicesByElemId.ContainsKey(elemId))
        {
          rowIndicesByElemId.Add(elemId, new List<int>());
        }
        rowIndicesByElemId[elemId].Add(rowIndex);
      }

      return true;
    }

    public bool ClearForElementId(int id)
    {
      lock (valuesLock)
      {
        if (Values.ContainsKey(id) && Values[id] != null)
        {
          Values[id] = null;
        }
      }
      if (rowIndicesByElemId.ContainsKey(id))
      {
        rowIndicesByElemId.Remove(id);
      }
      return true;
    }

    public bool Clear()
    {
      Headers.Clear();
      NumRows = 0;
      lock (valuesLock)
      {
        Values.Clear();
      }
      ErrRowIndices.Clear();
      rowIndicesByElemId.Clear();
      rowIndicesByTrimmedLoadCase.Clear();
      rowIndicesByFullLoadCase.Clear();
      colIndicesByHeader.Clear();
      fields.Clear();
      OtherFields.Clear();
      return true;
    }
  }
}
