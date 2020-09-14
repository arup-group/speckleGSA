using Microsoft.VisualBasic.FileIO;
using SpeckleGSAInterfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSAResults
{
  internal class ResultsTable
  {
    public string TableName;
    public List<string> Columns;
    public object[,] Values;
  }

  public class SpeckleGSAResultsContext : IGSAResultsContext
  {
    private List<ResultsTable> resultsTables = new List<ResultsTable>();
    private List<string> loadCases;

    public object[,] Query(string tableName, string loadCase, List<string> columns)
    {

      return new object[0, 0];
    }

    public bool ImportResultsFromFileDir(string dir, List<string> loadCases, List<string> tableNames = null)
    {
      //initialised with files that will be needed regardless of the later queries
      var filesToImport = new List<string>() { "analysis_case.csv" };

      var allResultFiles = Directory.GetFiles(dir, "result*.csv");
      if (tableNames == null)
      {
        filesToImport.AddRange(allResultFiles);
      }
      else
      {
        filesToImport.AddRange(allResultFiles.Where(f => tableNames.Any(tn => f.Contains(tn))));
      }

      this.loadCases = loadCases;
      
      foreach (var f in filesToImport)
      {
        LoadTable(f, loadCases);
      }

      return true;
    }

    private bool LoadTable(string filePath, List<string> loadCases)
    {
      var t = new ResultsTable() { TableName = Path.GetFileNameWithoutExtension(filePath) };
      using (TextFieldParser parser = new TextFieldParser(filePath))
      {
        parser.Delimiters = new string[] { "," };
        parser.HasFieldsEnclosedInQuotes = true;
        //Read column headers
        while (true)
        {
          string[] parts = parser.ReadFields();
          if (parts == null)
          {
            break;
          }
          Console.WriteLine("{0} field(s)", parts.Length);
        }
      }
      return true;
    }
  }
}
