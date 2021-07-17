using SpeckleGSAInterfaces;
using SpeckleGSAProxy.Results;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SpeckleGSAProxy.Test.ResultsTest
{
  public class ResultsAssemblyProcessor : ResultsProcessorBase
  {
    public override ResultCsvGroup Group => ResultCsvGroup.Assembly;

    public ResultsAssemblyProcessor(string filePath, Dictionary<ResultUnitType, double> unitData, List<string> cases = null, List<int> elemIds = null)
      : base(filePath, unitData, cases, elemIds)
    {
      this.resultTypes = new List<ResultType>()
      {
        ResultType.AssemblyForcesAndMoments
      };

      ColumnValuesFns = new Dictionary<ResultType, Func<List<int>, Dictionary<string, List<object>>>>()
      {
        { ResultType.AssemblyForcesAndMoments, ResultTypeColumnValues_AssemblyForcesAndMoments }
      };
    }
    public override bool LoadFromFile(bool parallel = true) => base.LoadFromFile<CsvAssembly>(parallel);

    #region column_values_fns

    protected Dictionary<string, List<object>> ResultTypeColumnValues_AssemblyForcesAndMoments(List<int> indices)
    {
      var factors = GetFactors(ResultUnitType.Length);
      var retDict = new Dictionary<string, List<object>>
      {
        { "fx", indices.Select(i => ApplyFactors(((CsvAssembly)Records[i]).Fx, factors)).Cast<object>().ToList() },
        { "fy", indices.Select(i => ApplyFactors(((CsvAssembly)Records[i]).Fy, factors)).Cast<object>().ToList() },
        { "fz", indices.Select(i => ApplyFactors(((CsvAssembly)Records[i]).Fz, factors)).Cast<object>().ToList() },
        { "frc", indices.Select(i => ApplyFactors(((CsvAssembly)Records[i]).Frc.Value, factors)).Cast<object>().ToList() },
        { "mxx", indices.Select(i => ApplyFactors(((CsvAssembly)Records[i]).Mxx, factors)).Cast<object>().ToList() },
        { "myy", indices.Select(i => ApplyFactors(((CsvAssembly)Records[i]).Myy, factors)).Cast<object>().ToList() },
        { "mzz", indices.Select(i => ApplyFactors(((CsvAssembly)Records[i]).Mzz, factors)).Cast<object>().ToList() },
        { "mom", indices.Select(i => ApplyFactors(((CsvAssembly)Records[i]).Mom.Value, factors)).Cast<object>().ToList() },
      };
      return retDict;
    }
    #endregion
  }
}
