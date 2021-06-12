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
  public class SpeckleGSAResultsContext : IGSAResultsContext
  {
    private readonly ConcurrentBag<ExportCsvResultsTable> resultsTables = new ConcurrentBag<ExportCsvResultsTable>();
    private readonly ConcurrentBag<ExportCsvTable> dataTables = new ConcurrentBag<ExportCsvTable>();
    public string ResultsDir { get; private set; }
    public List<string> ResultTables { get => resultsTables.Select(dt => dt.TableName).ToList(); }

    public SpeckleGSAResultsContext(string resultsDir)
    {
      this.ResultsDir = resultsDir;
    }

    public bool Query(string tableName, IEnumerable<string> columns, string loadCase, out object[,] results, int? elemId = null)
    {
      results = null; //default

      var t = resultsTables.FirstOrDefault(rt => rt.TableName.Equals(tableName, StringComparison.InvariantCultureIgnoreCase));
      if (t == null)
      {
        return false;
      }

      return t.Query(columns, new[] { loadCase }, out results, elemId.HasValue ? new[] { elemId.Value }: null);
    }

    public bool Query(string tableName, IEnumerable<string> columns, IEnumerable<string> loadCases, out object[,] results, IEnumerable<int> elemIds = null)
    {
      results = null; //default

      var t = resultsTables.FirstOrDefault(rt => rt.TableName.Equals(tableName, StringComparison.InvariantCultureIgnoreCase));
      if (t == null)
      {
        return false;
      }

      return t.Query(columns, loadCases, out results, elemIds);
    }

    public bool ImportResultsFromFile(string fileName, string caseIdField, string elemIdField)
    {
      var fn = Path.HasExtension(fileName) ? fileName : fileName + ".csv";
      var filePath = fn.Contains(":") ? fn : Path.Combine(ResultsDir, fn);

      if (!File.Exists(filePath))
      {
        return false;
      }

      var tableName = Path.GetFileNameWithoutExtension(filePath);
      var isResultsTable = tableName.StartsWith("result_", StringComparison.InvariantCultureIgnoreCase);

      ExportCsvTable t = isResultsTable ? new ExportCsvResultsTable(tableName, caseIdField, elemIdField) : new ExportCsvTable(tableName);
      if (t.LoadFromFile(filePath))
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
      return true;
    }

    /*
    public bool ImportResultsFromFileDir(string dir, IEnumerable<string> tableNames = null)
    {
      //Names of files that will be needed regardless of the later queries
      var otherTableNamesToImport = new List<string>() { "analysis_case.csv" };  //All IDS here are combined with "A" to form the load case string

      var allResultFiles = Directory.GetFiles(dir, "result*.csv").ToList();

      var filesToImport = new List<string>();
      filesToImport.AddRange(otherTableNamesToImport.Select(otn => Path.Combine(dir, otn)));
      filesToImport.AddRange((tableNames == null) ? allResultFiles : allResultFiles.Where(f => tableNames.Any(tn => f.Contains(tn))));

      foreach (var f in filesToImport)
      {
        var tableName = Path.GetFileNameWithoutExtension(f);
        var isResultsTable = tableName.StartsWith("result_", StringComparison.InvariantCultureIgnoreCase);

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
    */

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
