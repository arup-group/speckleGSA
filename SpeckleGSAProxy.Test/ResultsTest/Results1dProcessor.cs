using SpeckleGSAInterfaces;
using SpeckleGSAProxy.Results;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SpeckleGSAProxy.Test.ResultsTest
{
  public class Results1dProcessor : ResultsProcessorBase
  {
    public override ResultCsvGroup Group => ResultCsvGroup.Element1d;

    public Results1dProcessor(string filePath, Dictionary<ResultUnitType, double> unitData, List<string> cases = null, List<int> elemIds = null) 
      : base(filePath, unitData, cases, elemIds)
    {
      this.resultTypes = new List<ResultType>()
      {
        ResultType.Element1dDisplacement,
        ResultType.Element1dForce
      };

      ColumnValuesFns = new Dictionary<ResultType, Func<List<int>, Dictionary<string, List<object>>>>()
      {
        { ResultType.Element1dDisplacement, ResultTypeColumnValues_Element1dDisplacement },
        { ResultType.Element1dForce, ResultTypeColumnValues_Element1dForce }
      };
    }

    public override bool LoadFromFile(bool parallel = true) => base.LoadFromFile<CsvElem1d>(parallel);


    #region column_values_fns
    protected Dictionary<string, List<object>> ResultTypeColumnValues_Element1dDisplacement(List<int> indices)
    {
      var factors = GetFactors(ResultUnitType.Length);
      var retDict = new Dictionary<string, List<object>>
      {
        { "ux", indices.Select(i => ApplyFactors(((CsvElem1d)Records[i]).Ux, factors)).Cast<object>().ToList() },
        { "uy", indices.Select(i => ApplyFactors(((CsvElem1d)Records[i]).Uy, factors)).Cast<object>().ToList() },
        { "uz", indices.Select(i => ApplyFactors(((CsvElem1d)Records[i]).Uz, factors)).Cast<object>().ToList() },
        { "|u|", indices.Select(i => ApplyFactors(((CsvElem1d)Records[i]).U.Value, factors)).Cast<object>().ToList() }
      };
      return retDict;
    }


    protected Dictionary<string, List<object>> ResultTypeColumnValues_Element1dForce(List<int> indices)
    {
      var factorsForce = GetFactors(ResultUnitType.Force);
      var factorsMoment = GetFactors(ResultUnitType.Force, ResultUnitType.Length);
      var retDict = new Dictionary<string, List<object>>
      {
        { "fx", indices.Select(i => ApplyFactors(((CsvElem1d)Records[i]).Fx, factorsForce)).Cast<object>().ToList() },
        { "fy", indices.Select(i => ApplyFactors(((CsvElem1d)Records[i]).Fy, factorsForce)).Cast<object>().ToList() },
        { "fz", indices.Select(i => ApplyFactors(((CsvElem1d)Records[i]).Fz, factorsForce)).Cast<object>().ToList() },
        { "|f|", indices.Select(i => ApplyFactors(((CsvElem1d)Records[i]).F.Value, factorsForce)).Cast<object>().ToList() },
        { "mxx", indices.Select(i => ApplyFactors(((CsvElem1d)Records[i]).Mxx, factorsMoment)).Cast<object>().ToList() },
        { "myy", indices.Select(i => ApplyFactors(((CsvElem1d)Records[i]).Myy, factorsMoment)).Cast<object>().ToList() },
        { "mzz", indices.Select(i => ApplyFactors(((CsvElem1d)Records[i]).Mzz, factorsMoment)).Cast<object>().ToList() },
        { "|m|", indices.Select(i => ApplyFactors(((CsvElem1d)Records[i]).M.Value, factorsMoment)).Cast<object>().ToList() },
        { "fyz", indices.Select(i => ApplyFactors(((CsvElem1d)Records[i]).Fyz.Value, factorsForce)).Cast<object>().ToList() },
        { "myz", indices.Select(i => ApplyFactors(((CsvElem1d)Records[i]).Myz.Value, factorsMoment)).Cast<object>().ToList() }
      };
      return retDict;
    }
    #endregion
  }
}
