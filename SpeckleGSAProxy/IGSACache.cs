﻿using SpeckleCore;
using SpeckleGSAInterfaces;
using System;
using System.Collections.Generic;

namespace SpeckleGSAProxy
{
  public interface IGSACache
  {
    bool Upsert(string keyword, int index, string gwa, string applicationId = "", SpeckleObject so = null, GwaSetCommandType gwaSetCommandType = GwaSetCommandType.Set, bool? latest = true, string streamId = null);

    bool AssignSpeckleObject(string keyword, string sid, SpeckleObject so, string streamId = null);

    void Snapshot();

    bool ApplicationIdExists(string keyword, string applicationId);

    bool ContainsType(string speckleTypeName);

    List<SpeckleObject> GetSpeckleObjects(string speckleTypeName, string applicationId, bool? latest = true, string streamId = null);

    List<string> GetCurrentGwa();

    void Clear();

    List<string> GetNewlyAddedGwa();

    List<Tuple<string, int, string, GwaSetCommandType>> GetExpiredData();

    List<Tuple<string, int, string, GwaSetCommandType>> GetDeletableData();

    void MarkAsPrevious(string keyword, string sid);
  }
}
