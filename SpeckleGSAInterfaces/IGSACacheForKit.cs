using System.Collections.Generic;

namespace SpeckleGSAInterfaces
{
  public interface IGSACacheForKit
  {
    List<string> GetGwa(string keyword, int index);

    Dictionary<int, object> GetIndicesSpeckleObjects(string SpeckleType);

    List<string> GetGwa(string keyword);

    Dictionary<int, string> GetGwaToSerialise(string keyword);

    string GetApplicationId(string keyword, int index);

    int ResolveIndex(string keyword, string applicationId = "");

    int? LookupIndex(string keyword, string applicationId);

    List<int?> LookupIndices(string keyword, IEnumerable<string> applicationIds);

    List<int?> LookupIndices(string keyword);

    //Used to update the cache with nodes created using NodeAt
    bool Upsert(string keyword, int index, string gwaWithoutSet, string streamId, string applicationId, GwaSetCommandType gwaSetCommandType);
  }
}
