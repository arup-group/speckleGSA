using SpeckleCore;
using SpeckleGSAInterfaces;
using System;
using System.Collections.Generic;

namespace SpeckleGSAProxy
{
  public interface IGSACache
  {
    bool Upsert(string keyword, int index, string gwa, string applicationId = "", SpeckleObject speckleObject = null, bool currentSession = true, GwaSetCommandType gwaSetCommandType = GwaSetCommandType.Set);

    bool AssignSpeckleObject(string keyword, string applicationId, SpeckleObject so);

    void Snapshot();

    bool Exists(string keyword, string applicationId, bool prev = false, bool latest = true);

    bool ContainsType(string speckleTypeName);

    List<SpeckleObject> GetSpeckleObjects(string speckleTypeName, string applicationId);

    List<string> GetCurrentSessionGwa();

    void Clear();

    List<string> GetNewlyAddedGwa();

    List<Tuple<string, int, string, GwaSetCommandType>> GetToBeDeletedGwa();

  }
}
