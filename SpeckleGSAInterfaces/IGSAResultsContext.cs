using System.Collections.Generic;

namespace SpeckleGSAInterfaces
{
  public interface IGSAResultsContext
  {
    bool ImportResultsFromFileDir(string dir, List<string> tableNames = null);

    object[,] Query(string tableName, string loadCase, List<string> columns);
  }
}
