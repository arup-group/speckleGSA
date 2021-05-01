using SpeckleCore;
using SpeckleGSAInterfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSAProxy
{
  internal interface IGSACacheRecordCollection
  {
    Dictionary<int, object> GetSpeckleObjectsByTypeName(string speckleTypeName);
    List<SpeckleObject> GetSpeckleObjects(string speckleTypeName, string applicationId, bool? latest, string streamId);
    bool ContainsKeyword(string keyword, string applicationId);
    bool ContainsSpeckleType(string speckleTypeName);
    Dictionary<int, string> GetIndexedGwa(string kw);
    Dictionary<string, List<GSACacheRecord>> GetLatestRecordsByKeyword();
    List<string> GetRecord(string keyword, int index);
    List<string> GetAllRecordsByKeyword(string kw);
    List<string> GetLatestGwa();
    void Clear();
    bool GetRecordSummaries(string kw, out List<string> gwa, out List<int> indices, out List<string> applicationIds);
    List<string> GetGwaCommandsWithSet();
    void MarkPrevious(string kw, string applicationId);
    List<GSACacheRecord> GetAllRecords(string kw, int index);
    void Upsert(string kw, int index, string gwa, string streamId, string applicationId, bool v, SpeckleObject so, GwaSetCommandType gwaSetCommandType);
    bool AssignSpeckleObject(string kw, string applicationId, SpeckleObject so, string streamId);
    void Snapshot(string streamId);
    List<GSACacheRecord> GetAllRecords();
    bool AssignApplicationId(string kw, int index, string applicationId);
    string GetApplicationId(string kw, int index);
    HashSet<int> GetRecordIndexHashSet(string kw);
    SortedSet<int> GetRecordIndices(string kw);
    List<Tuple<string, int, string, GwaSetCommandType>> GetDeletableData();
    List<Tuple<string, int, string, GwaSetCommandType>> GetExpiredData();
    List<int?> GetRecordIndices(string kw, IEnumerable<string> applicationIds);
    int? GetRecordIndex(string kw, string applicationId);
    List<GSACacheRecord> GetAllRecords(string kw, string applicationId);
  }
}
