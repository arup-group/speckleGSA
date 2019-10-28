using System;
using System.Collections.Generic;

namespace SpeckleGSAInterfaces
{
	public interface IGSAIndexer
	{

    int ResolveIndex(string keyword, string type, string applicationId = "");
    
    int? LookupIndex(string keyword, string type, string applicationId);
    List<int?> LookupIndices(string keyword, string type, IEnumerable<string> applicationIds);
    
    List<int> ResolveIndices(string keyword, string type, IEnumerable<string> applicationIds = null);

    void ReserveIndices(string keyword, IEnumerable<int> indices);
    void ReserveIndicesAndMap(string keyword, string typeName, IList<int> indices, IList<string> applicationIds);

    void SetBaseline();
    void ResetToBaseline();
    bool InBaseline(string keywordGSA, int index);
    void Reset();

    //New methods

  }
}
