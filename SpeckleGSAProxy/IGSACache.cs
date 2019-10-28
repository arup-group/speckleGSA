using SpeckleCore;
using SpeckleGSAInterfaces;
using System;
using System.Collections.Generic;

namespace SpeckleGSAProxy
{
  public interface IGSACache
  {
    bool Upsert(string keyword, int index, string gwa, string applicationId = "", SpeckleObject speckleObject = null, bool currentSession = true, GwaSetCommandType gwaSetCommandType = GwaSetCommandType.Set);

    bool AssignSpeckleObject(Type type, string ApplicationId, SpeckleObject so);

    void Snapshot();

    bool Exists(string applicationId);

    bool ContainsType(Type t);

    List<SpeckleObject> GetSpeckleObjects(Type t, string applicationId);

    List<string> GetCurrentSessionGwa();

    void Clear();

    //Indexing
    /*
    int ResolveIndex(string keyword, string type, string applicationId = "");
    //List<int> ResolveIndices(string keyword, string type, IEnumerable<string> applicationIds = null);
    int? LookupIndex(string keyword, string type, string applicationId);
    List<int?> LookupIndices(string keyword, string type, IEnumerable<string> applicationIds);
    */
  }
}
