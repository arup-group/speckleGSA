using System.Collections.Generic;

namespace SpeckleGSAProxy
{
  public interface IGSACacheForKit
  {
    List<string> GetNewlyAddedGwa();

    List<string> GetToBeDeletedGwa();

    string GetApplicationId(string keyword, int index);

    int ResolveIndex(string keyword, string type, string applicationId = "");

    int? LookupIndex(string keyword, string type, string applicationId);
    List<int?> LookupIndices(string keyword, string type, IEnumerable<string> applicationIds);
  }
}
