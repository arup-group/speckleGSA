using SpeckleGSAInterfaces;
using SpeckleGSAProxy.Results;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SpeckleGSAProxy.Test.ResultsTest
{
  public class ResultsAssemblyProcessor : ResultsProcessorBase
  {
    protected Dictionary<int, CsvAssembly> RecordsAssembly = new Dictionary<int, CsvAssembly>();
    protected Dictionary<int, Dictionary<string, List<int>>> AssemblyRecordIndices = new Dictionary<int, Dictionary<string, List<int>>>();

    protected override Dictionary<int, Dictionary<string, List<int>>> RecordIndices => AssemblyRecordIndices;

    public ResultsAssemblyProcessor(string filePath, Dictionary<ResultUnitType, double> unitData, List<string> cases = null, List<int> elemIds = null)
      : base(filePath, unitData, cases, elemIds)
    {
      this.resultTypes.AddRange(new[]
          {
            ResultType.AssemblyForcesAndMoments
          });

      ColumnValuesFns = new Dictionary<ResultType, Func<List<int>, Dictionary<string, List<object>>>>()
      {
        { ResultType.AssemblyForcesAndMoments, ResultTypeColumnValues_AssemblyForcesAndMoments }
      };
    }
    public bool LoadFromFile(bool parallel = true) => base.LoadFromFile<CsvAssembly>(parallel);

    #region column_values_fns

    protected Dictionary<string, List<object>> ResultTypeColumnValues_AssemblyForcesAndMoments(List<int> indices)
    {
      var factors = GetFactors(ResultUnitType.Length);
      var retDict = new Dictionary<string, List<object>>
      {
        { "fx", indices.Select(i => ApplyFactors(RecordsAssembly[i].Fx, factors)).Cast<object>().ToList() },
        { "fy", indices.Select(i => ApplyFactors(RecordsAssembly[i].Fy, factors)).Cast<object>().ToList() },
        { "fz", indices.Select(i => ApplyFactors(RecordsAssembly[i].Fz, factors)).Cast<object>().ToList() },
        { "frc", indices.Select(i => ApplyFactors(RecordsAssembly[i].Frc.Value, factors)).Cast<object>().ToList() },
        { "mxx", indices.Select(i => ApplyFactors(RecordsAssembly[i].Mxx, factors)).Cast<object>().ToList() },
        { "myy", indices.Select(i => ApplyFactors(RecordsAssembly[i].Myy, factors)).Cast<object>().ToList() },
        { "mzz", indices.Select(i => ApplyFactors(RecordsAssembly[i].Mzz, factors)).Cast<object>().ToList() },
        { "mom", indices.Select(i => ApplyFactors(RecordsAssembly[i].Mom.Value, factors)).Cast<object>().ToList() },
      };
      return retDict;
    }
    #endregion
  }
}
