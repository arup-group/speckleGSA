using Microsoft.VisualBasic.FileIO;
using SpeckleGSAInterfaces;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSAResults
{
  public class SpeckleGSAResultsContext : IGSAResultsContext
  {
    private readonly List<ExportCsvResultsTable> resultsTables = new List<ExportCsvResultsTable>();
    private readonly List<ExportCsvTable> dataTables = new List<ExportCsvTable>();

    public object[,] Query(string tableName, string loadCase, List<string> columns)
    {

      return new object[0, 0];
    }

    public bool ImportResultsFromFileDir(string dir, List<string> tableNames = null)
    {
      //Names of files that will be needed regardless of the later queries
      var otherTableNamesToImport = new List<string>() { "analysis_case.csv" };  //All IDS here are combined with "A" to form the load case string

      var allResultFiles = Directory.GetFiles(dir, "result*.csv").ToList();

      var filesToImport = new List<string>();
      filesToImport.AddRange(otherTableNamesToImport.Select(otn => Path.Combine(dir, otn)));
      filesToImport.AddRange((tableNames == null) ? allResultFiles : allResultFiles.Where(f => tableNames.Any(tn => f.Contains(tn))));

      foreach (var f in filesToImport)
      {
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(f);
        var isResultsTable = fileNameWithoutExtension.StartsWith("result_", StringComparison.InvariantCultureIgnoreCase);
        var tableName = isResultsTable ? fileNameWithoutExtension.Substring(("result_").Length) : fileNameWithoutExtension;
        ExportCsvTable t = isResultsTable ? new ExportCsvResultsTable(tableName, "case_id") : new ExportCsvTable(tableName);
        if (t.LoadFromFile(f))
        {
          if (isResultsTable)
          {
            resultsTables.Add((ExportCsvResultsTable)t);
          }
          else
          {
            dataTables.Add(t);
          }
        }
        else
        {
          return false;
        }
      }

      return true;
    }

    /*
    private bool LoadTable(string filePath, List<string> loadCases)
    {
      var t = new ResultsTable() { TableName = Path.GetFileNameWithoutExtension(filePath) };
      using (TextFieldParser parser = new TextFieldParser(filePath))
      {
        parser.Delimiters = new string[] { "," };
        parser.HasFieldsEnclosedInQuotes = true;
        //Read column headers
        string[] parts = parser.ReadFields();

        while (true)
        {
          string[] parts = parser.re();
          if (parts == null)
          {
            break;
          }
          Console.WriteLine("{0} field(s)", parts.Length);
        }
      }
      return true;
    }
    */
  }
}
