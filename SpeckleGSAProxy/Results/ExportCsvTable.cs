using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SpeckleGSAProxy.Results
{
  internal class ExportCsvTable
  {
    public string TableName;
    public string[] Headers;
    public int NumRows;
    public ConcurrentDictionary<int, string[]> Values;
    public List<int> ErrRowIndices;

    public ExportCsvTable(string tableName)
    {
      this.TableName = tableName;
      Values = new ConcurrentDictionary<int, string[]>();
      ErrRowIndices = new List<int>();
    }

    public bool LoadFromFile(string filePath)
    {
      var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
      var sr = new StreamReader(fs);
      var delimiters = new[] { ',' };

      //Headers
      var line = sr.ReadLine();
      Headers = line.Split(new[] { ',' }, StringSplitOptions.None);
      ErrRowIndices.Clear();
      Values.Clear();
      NumRows = 0;

      //Rest of file
      while ((line = sr.ReadLine()) != null)
      {
        try
        {
          var rowValues = ParseLine(line, delimiters);
          for (var i = 0; i < rowValues.Count(); i++)
          {
            //This is a recognised value
            if (rowValues[i].Equals("null"))
            {
              rowValues[i] = "";
            }
          }
          if (!AddRow(NumRows, rowValues))
          {
            ErrRowIndices.Add(NumRows);
          }
        }
        catch
        {
          ErrRowIndices.Add(NumRows);
        }
        finally
        {
          NumRows++;
        }
      }
      sr.Close();
      return true;
    }

    protected virtual bool AddRow(int RowIndex, List<string> rowData)
    {
      return (Headers != null && Headers.Length > 0 && Values.TryAdd(RowIndex, rowData.ToArray()));
    }

    protected List<string> ParseLine(string line, char[] delimiters)
    {
      var lineTrimmed = line.Trim();
      if (lineTrimmed.StartsWith("//") || lineTrimmed.Length == 0)
      {
        return null;
      }

      //Check if there are any unclosed quotes on the line
      var numQuotes = line.Count(c => c.Equals('"'));
      if (numQuotes % 2 == 1)
      {
        line += "\"";
      }

      var pieces = new List<string>();
      string currWord = "";

      var inQuote = false;
      for (int i = 0; i < line.Length; i++)
      {
        if (delimiters.Any(d => line[i] == d))
        {
          if (inQuote)
          {
            currWord += line[i];
          }
          else
          {
            pieces.Add(currWord);
            currWord = "";
            /*
            if (!string.IsNullOrEmpty(currWord))
            {
              pieces.Add(currWord);
              currWord = "";
            }
            */
          }
        }
        else if (line[i] == '\"')
        {
          if (inQuote)
          {
            if (!string.IsNullOrEmpty(currWord))
            {
              pieces.Add(currWord);
            }
            currWord = "";
            inQuote = false;
          }
          else
          {
            inQuote = true;
          }
        }
        else
        {
          currWord += line[i];
        }
      }
      pieces.Add(currWord);
      /*
      if (!string.IsNullOrEmpty(currWord))
      {
        pieces.Add(currWord);
      }
      */
      return pieces;
    }
  }
}
