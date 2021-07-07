using SpeckleGSAInterfaces;
using System.Collections.Generic;

namespace SpeckleGSAProxy
{
  internal interface IGSAResultsContext
  {
    string ResultsDir { get; }
    bool ImportResultsFromFile(string fileName, ResultCsvGroup group, string caseIdField, string elemIdField, List<string> otherFields, List<string> cases, List<int> elemIds);
    //Tables currently read and loaded into memory
    List<string> ResultTableNames { get; }
    List<ResultCsvGroup> ResultTableGroups { get; }

    bool Query(ResultCsvGroup group, IEnumerable<string> columns, string loadCase, out object[,] results, int? elemId = null);
    bool Query(ResultCsvGroup group, IEnumerable<string> columns, IEnumerable<string> loadCases, out object[,] results, IEnumerable<int> elemIds = null);
    bool Clear(ResultCsvGroup group = ResultCsvGroup.Unknown);
  }
}
