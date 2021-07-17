using SpeckleGSAInterfaces;
using SpeckleGSAProxy.Results;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SpeckleGSAProxy.Test.ResultsTest
{
  public class ResultsNodeProcessor : ResultsProcessorBase
  {
    public override ResultCsvGroup Group => ResultCsvGroup.Node;

    public ResultsNodeProcessor(string filePath, Dictionary<ResultUnitType, double> unitData, List<string> cases = null, List<int> elemIds = null) : base(filePath, unitData, cases, elemIds)
    {
      this.resultTypes = new List<ResultType>()
      {
        ResultType.NodalDisplacements,
        ResultType.NodalVelocity,
        ResultType.NodalAcceleration,
        ResultType.NodalReaction,
        ResultType.ConstraintForces
      };

      ColumnValuesFns = new Dictionary<ResultType, Func<List<int>, Dictionary<string, List<object>>>>()
      {
        { ResultType.NodalDisplacements, ResultTypeColumnValues_NodalDisplacements },
        { ResultType.NodalVelocity, ResultTypeColumnValues_NodalVelocity },
        { ResultType.NodalAcceleration, ResultTypeColumnValues_NodalAcceleration },
        { ResultType.NodalReaction, ResultTypeColumnValues_NodalReaction },
        { ResultType.ConstraintForces, ResultTypeColumnValues_ConstraintForces }
      };
    }

    public override bool LoadFromFile(bool parallel = true) =>base.LoadFromFile<CsvNode>(parallel);

    #region column_values_fns
    protected Dictionary<string, List<object>> ResultTypeColumnValues_NodalDisplacements(List<int> indices)
    {
      if (Records == null || Records.Count() == 0)
      {
        return new Dictionary<string, List<object>>();
      }
      var factors = GetFactors(ResultUnitType.Length);
      var factorsRotation = GetFactors(ResultUnitType.Angle);

      var retDict = new Dictionary<string, List<object>>
      {
        { "ux", indices.Select(i => ApplyFactors(((CsvNode) Records[i]).Ux, factors)).Cast<object>().ToList() },
        { "uy", indices.Select(i => ApplyFactors(((CsvNode) Records[i]).Uy, factors)).Cast<object>().ToList() },
        { "uz", indices.Select(i => ApplyFactors(((CsvNode) Records[i]).Uz, factors)).Cast<object>().ToList() },
        { "|u|", indices.Select(i => ApplyFactors(((CsvNode) Records[i]).U.Value, factors)).Cast<object>().ToList() },
        { "rxx", indices.Select(i => ApplyFactors(((CsvNode) Records[i]).Rxx, factors)).Cast<object>().ToList() },
        { "ryy", indices.Select(i => ApplyFactors(((CsvNode) Records[i]).Ryy, factors)).Cast<object>().ToList() },
        { "rzz", indices.Select(i => ApplyFactors(((CsvNode) Records[i]).Rzz, factors)).Cast<object>().ToList() },
        { "|r|", indices.Select(i => ApplyFactors(((CsvNode) Records[i]).R_Disp.Value, factorsRotation)).Cast<object>().ToList() },
        { "uxy", indices.Select(i => ApplyFactors(((CsvNode) Records[i]).Uxy.Value, factors)).Cast<object>().ToList() }
      };
      return retDict;
    }

    protected Dictionary<string, List<object>> ResultTypeColumnValues_NodalVelocity(List<int> indices)
    {
      var factors = GetFactors(ResultUnitType.Length, ResultUnitType.Time);
      var retDict = new Dictionary<string, List<object>>
      {
        { "vx", indices.Select(i => ApplyFactors(((CsvNode) Records[i]).Vx, factors)).Cast<object>().ToList() },
        { "vy", indices.Select(i => ApplyFactors(((CsvNode) Records[i]).Vy, factors)).Cast<object>().ToList() },
        { "vz", indices.Select(i => ApplyFactors(((CsvNode) Records[i]).Vz, factors)).Cast<object>().ToList() },
        { "|v|", indices.Select(i => ApplyFactors(((CsvNode) Records[i]).V.Value, factors)).Cast<object>().ToList() },
        { "vxx", indices.Select(i => ApplyFactors(((CsvNode) Records[i]).Vxx, factors)).Cast<object>().ToList() },
        { "vyy", indices.Select(i => ApplyFactors(((CsvNode) Records[i]).Vyy, factors)).Cast<object>().ToList() },
        { "vzz", indices.Select(i => ApplyFactors(((CsvNode) Records[i]).Vzz, factors)).Cast<object>().ToList() },
        { "|r|", indices.Select(i => ApplyFactors(((CsvNode) Records[i]).R_Vel.Value, factors)).Cast<object>().ToList() }
      };
      return retDict;
    }

    protected Dictionary<string, List<object>> ResultTypeColumnValues_NodalAcceleration(List<int> indices)
    {
      var factors = GetFactors(ResultUnitType.Accel);
      var retDict = new Dictionary<string, List<object>>
      {
        { "ax", indices.Select(i => ApplyFactors(((CsvNode) Records[i]).Ax, factors)).Cast<object>().ToList() },
        { "ay", indices.Select(i => ApplyFactors(((CsvNode) Records[i]).Ay, factors)).Cast<object>().ToList() },
        { "az", indices.Select(i => ApplyFactors(((CsvNode) Records[i]).Az, factors)).Cast<object>().ToList() },
        { "|a|", indices.Select(i => ApplyFactors(((CsvNode) Records[i]).A.Value, factors)).Cast<object>().ToList() },
        { "axx", indices.Select(i => ApplyFactors(((CsvNode) Records[i]).Axx, factors)).Cast<object>().ToList() },
        { "ayy", indices.Select(i => ApplyFactors(((CsvNode) Records[i]).Ayy, factors)).Cast<object>().ToList() },
        { "azz", indices.Select(i => ApplyFactors(((CsvNode) Records[i]).Azz, factors)).Cast<object>().ToList() },
        { "|r|", indices.Select(i => ApplyFactors(((CsvNode) Records[i]).R_Acc.Value, factors)).Cast<object>().ToList() }
      };
      return retDict;
    }

    protected Dictionary<string, List<object>> ResultTypeColumnValues_NodalReaction(List<int> indices)
    {
      var factorsForce = GetFactors(ResultUnitType.Force);
      var factorsMoment = GetFactors(ResultUnitType.Force, ResultUnitType.Length);
      var retDict = new Dictionary<string, List<object>>
      {
        { "fx", indices.Select(i => ApplyFactors(((CsvNode) Records[i]).Fx_Reac, factorsForce)).Cast<object>().ToList() },
        { "fy", indices.Select(i => ApplyFactors(((CsvNode) Records[i]).Fy_Reac, factorsForce)).Cast<object>().ToList() },
        { "fz", indices.Select(i => ApplyFactors(((CsvNode) Records[i]).Fz_Reac, factorsForce)).Cast<object>().ToList() },
        { "|f|", indices.Select(i => ApplyFactors(((CsvNode) Records[i]).F_Reac.Value, factorsForce)).Cast<object>().ToList() },
        { "mxx", indices.Select(i => ApplyFactors(((CsvNode) Records[i]).Mxx_Reac, factorsMoment)).Cast<object>().ToList() },
        { "myy", indices.Select(i => ApplyFactors(((CsvNode) Records[i]).Myy_Reac, factorsMoment)).Cast<object>().ToList() },
        { "mzz", indices.Select(i => ApplyFactors(((CsvNode) Records[i]).Mzz_Reac, factorsMoment)).Cast<object>().ToList() },
        { "|m|", indices.Select(i => ApplyFactors(((CsvNode) Records[i]).M_Reac.Value, factorsMoment)).Cast<object>().ToList() }
      };
      return retDict;
    }

    protected Dictionary<string, List<object>> ResultTypeColumnValues_ConstraintForces(List<int> indices)
    {
      var factorsForce = GetFactors(ResultUnitType.Force);
      var factorsMoment = GetFactors(ResultUnitType.Force, ResultUnitType.Length);
      var retDict = new Dictionary<string, List<object>>
      {
        { "fx", indices.Select(i => ApplyFactors(((CsvNode) Records[i]).Fx_Cons, factorsForce)).Cast<object>().ToList() },
        { "fy", indices.Select(i => ApplyFactors(((CsvNode) Records[i]).Fy_Cons, factorsForce)).Cast<object>().ToList() },
        { "fz", indices.Select(i => ApplyFactors(((CsvNode) Records[i]).Fz_Cons, factorsForce)).Cast<object>().ToList() },
        { "|f|", indices.Select(i => ApplyFactors(((CsvNode) Records[i]).F_Cons.Value, factorsForce)).Cast<object>().ToList() },
        { "mxx", indices.Select(i => ApplyFactors(((CsvNode) Records[i]).Mxx_Cons, factorsMoment)).Cast<object>().ToList() },
        { "myy", indices.Select(i => ApplyFactors(((CsvNode) Records[i]).Myy_Cons, factorsMoment)).Cast<object>().ToList() },
        { "mzz", indices.Select(i => ApplyFactors(((CsvNode) Records[i]).Mzz_Cons, factorsMoment)).Cast<object>().ToList() },
        { "|m|", indices.Select(i => ApplyFactors(((CsvNode) Records[i]).M_Cons.Value, factorsMoment)).Cast<object>().ToList() }
      };
      return retDict;
    }
    #endregion
  }
}
