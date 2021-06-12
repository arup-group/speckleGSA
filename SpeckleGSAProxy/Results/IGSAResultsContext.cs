using System.Collections.Generic;

namespace SpeckleGSAProxy
{
  public interface IGSAResultsContext
  {
    string ResultsDir { get; }
    bool ImportResultsFromFile(string filePath, string loadCaseField, string elemIdField = null);
    //Tables currently read and loaded into memory
    List<string> ResultTables { get; }

    bool Query(string tableName, IEnumerable<string> columns, IEnumerable<string> loadCases, out object[,] results, IEnumerable<int> elemIds = null);
  }
}
