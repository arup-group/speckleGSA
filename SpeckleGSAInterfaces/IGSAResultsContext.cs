using System.Collections.Generic;

namespace SpeckleGSAInterfaces
{
  public interface IGSAResultsContext
  {
    string ResultsDir { get; }
    bool ImportResultsFromFile(string filePath, string loadCaseField, string elemIdField = null);

    bool Query(string tableName, IEnumerable<string> columns, IEnumerable<string> loadCases, out object[,] results, IEnumerable<int> elemIds = null);
  }
}
