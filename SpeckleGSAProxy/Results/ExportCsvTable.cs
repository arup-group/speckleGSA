using CsvHelper;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SpeckleGSAProxy.Results
{
  public class ExportCsvTable
  {
    public string TableName;
    public Dictionary<string, int> Headers;
    public int NumRows;
    public ConcurrentDictionary<int, object[]> Values;
    public List<int> ErrRowIndices;

    protected List<string> fields;

    //private object valuesLock = new object();
    protected int numHeaders = 0;

    public ExportCsvTable(string tableName)
    {
      this.TableName = tableName;
      Values = new ConcurrentDictionary<int, object[]>();
      ErrRowIndices = new List<int>();
    }

    public bool LoadFromFile(string filePath)
    {
      var delimiters = new HashSet<char> { ',' };

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
          var vals = new object[fields.Count()];
          for (int c = 0; c < fields.Count(); c++)
          {
            vals[c] = csv.GetField(fields[c]);
          }
          if (!AddRow(rowIndex, vals.ToList()))
          {
            ErrRowIndices.Add(rowIndex);
          }
          rowIndex++;
        }
      }

      NumRows = Values.Keys.Count();

      reader.Close();
      return true;
    }

    protected virtual bool AddRow(int RowIndex, List<object> rowData)
    {
      if (numHeaders > 0)
      {
        Values[RowIndex] = rowData.ToArray();
        return true;
      }

      return false;
    }
  }
}
