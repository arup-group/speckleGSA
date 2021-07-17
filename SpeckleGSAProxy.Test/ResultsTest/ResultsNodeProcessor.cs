using SpeckleGSAInterfaces;
using SpeckleGSAProxy.Results;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SpeckleGSAProxy.Test.ResultsTest
{
  public class ResultsNodeProcessor : ResultsProcessorBase
  {
    protected Dictionary<int, CsvNode> RecordsNode = new Dictionary<int, CsvNode>();
    protected Dictionary<int, Dictionary<string, List<int>>> NodeRecordIndices = new Dictionary<int, Dictionary<string, List<int>>>();

    protected override Dictionary<int, Dictionary<string, List<int>>> RecordIndices => NodeRecordIndices;

    public ResultsNodeProcessor(string filePath, Dictionary<ResultUnitType, double> unitData, List<string> cases = null, List<int> elemIds = null) : base(filePath, unitData, cases, elemIds)
    {
      this.resultTypes.AddRange(new[]
          {
            ResultType.NodalDisplacements,
            ResultType.NodalVelocity,
            ResultType.NodalAcceleration,
            ResultType.NodalReaction,
            ResultType.ConstraintForces
          });

      ColumnValuesFns = new Dictionary<ResultType, Func<List<int>, Dictionary<string, List<object>>>>()
      {
        { ResultType.NodalDisplacements, ResultTypeColumnValues_NodalDisplacements },
        { ResultType.NodalVelocity, ResultTypeColumnValues_NodalVelocity },
        { ResultType.NodalAcceleration, ResultTypeColumnValues_NodalAcceleration },
        { ResultType.NodalReaction, ResultTypeColumnValues_NodalReaction },
        { ResultType.ConstraintForces, ResultTypeColumnValues_ConstraintForces }
      };
    }

    public bool LoadFromFile(bool parallel = true) => base.LoadFromFile<CsvNode>(parallel);

    #region column_values_fns
    protected Dictionary<string, List<object>> ResultTypeColumnValues_NodalDisplacements(List<int> indices)
    {
      var factors = GetFactors(ResultUnitType.Length);
      var factorsRotation = GetFactors(ResultUnitType.Angle);
      var retDict = new Dictionary<string, List<object>>
      {
        { "ux", indices.Select(i => ApplyFactors(RecordsNode[i].Ux, factors)).Cast<object>().ToList() },
        { "uy", indices.Select(i => ApplyFactors(RecordsNode[i].Uy, factors)).Cast<object>().ToList() },
        { "uz", indices.Select(i => ApplyFactors(RecordsNode[i].Uz, factors)).Cast<object>().ToList() },
        { "|u|", indices.Select(i => ApplyFactors(RecordsNode[i].U.Value, factors)).Cast<object>().ToList() },
        { "rxx", indices.Select(i => ApplyFactors(RecordsNode[i].Rxx, factors)).Cast<object>().ToList() },
        { "ryy", indices.Select(i => ApplyFactors(RecordsNode[i].Ryy, factors)).Cast<object>().ToList() },
        { "rzz", indices.Select(i => ApplyFactors(RecordsNode[i].Rzz, factors)).Cast<object>().ToList() },
        { "|r|", indices.Select(i => ApplyFactors(RecordsNode[i].R_Disp.Value, factorsRotation)).Cast<object>().ToList() },
        { "uxy", indices.Select(i => ApplyFactors(RecordsNode[i].Uxy.Value, factors)).Cast<object>().ToList() }
      };
      return retDict;
    }

    protected Dictionary<string, List<object>> ResultTypeColumnValues_NodalVelocity(List<int> indices)
    {
      var factors = GetFactors(ResultUnitType.Length, ResultUnitType.Time);
      var retDict = new Dictionary<string, List<object>>
      {
        { "vx", indices.Select(i => ApplyFactors(RecordsNode[i].Vx, factors)).Cast<object>().ToList() },
        { "vy", indices.Select(i => ApplyFactors(RecordsNode[i].Vy, factors)).Cast<object>().ToList() },
        { "vz", indices.Select(i => ApplyFactors(RecordsNode[i].Vz, factors)).Cast<object>().ToList() },
        { "|v|", indices.Select(i => ApplyFactors(RecordsNode[i].V.Value, factors)).Cast<object>().ToList() },
        { "vxx", indices.Select(i => ApplyFactors(RecordsNode[i].Vxx, factors)).Cast<object>().ToList() },
        { "vyy", indices.Select(i => ApplyFactors(RecordsNode[i].Vyy, factors)).Cast<object>().ToList() },
        { "vzz", indices.Select(i => ApplyFactors(RecordsNode[i].Vzz, factors)).Cast<object>().ToList() },
        { "|r|", indices.Select(i => ApplyFactors(RecordsNode[i].R_Vel.Value, factors)).Cast<object>().ToList() }
      };
      return retDict;
    }

    protected Dictionary<string, List<object>> ResultTypeColumnValues_NodalAcceleration(List<int> indices)
    {
      var factors = GetFactors(ResultUnitType.Accel);
      var retDict = new Dictionary<string, List<object>>
      {
        { "ax", indices.Select(i => ApplyFactors(RecordsNode[i].Ax, factors)).Cast<object>().ToList() },
        { "ay", indices.Select(i => ApplyFactors(RecordsNode[i].Ay, factors)).Cast<object>().ToList() },
        { "az", indices.Select(i => ApplyFactors(RecordsNode[i].Az, factors)).Cast<object>().ToList() },
        { "|a|", indices.Select(i => ApplyFactors(RecordsNode[i].A.Value, factors)).Cast<object>().ToList() },
        { "axx", indices.Select(i => ApplyFactors(RecordsNode[i].Axx, factors)).Cast<object>().ToList() },
        { "ayy", indices.Select(i => ApplyFactors(RecordsNode[i].Ayy, factors)).Cast<object>().ToList() },
        { "azz", indices.Select(i => ApplyFactors(RecordsNode[i].Azz, factors)).Cast<object>().ToList() },
        { "|r|", indices.Select(i => ApplyFactors(RecordsNode[i].R_Acc.Value, factors)).Cast<object>().ToList() }
      };
      return retDict;
    }

    protected Dictionary<string, List<object>> ResultTypeColumnValues_NodalReaction(List<int> indices)
    {
      var factorsForce = GetFactors(ResultUnitType.Force);
      var factorsMoment = GetFactors(ResultUnitType.Force, ResultUnitType.Length);
      var retDict = new Dictionary<string, List<object>>
      {
        { "fx", indices.Select(i => ApplyFactors(RecordsNode[i].Fx_Reac, factorsForce)).Cast<object>().ToList() },
        { "fy", indices.Select(i => ApplyFactors(RecordsNode[i].Fy_Reac, factorsForce)).Cast<object>().ToList() },
        { "fz", indices.Select(i => ApplyFactors(RecordsNode[i].Fz_Reac, factorsForce)).Cast<object>().ToList() },
        { "|f|", indices.Select(i => ApplyFactors(RecordsNode[i].F_Reac.Value, factorsForce)).Cast<object>().ToList() },
        { "mxx", indices.Select(i => ApplyFactors(RecordsNode[i].Mxx_Reac, factorsMoment)).Cast<object>().ToList() },
        { "myy", indices.Select(i => ApplyFactors(RecordsNode[i].Myy_Reac, factorsMoment)).Cast<object>().ToList() },
        { "mzz", indices.Select(i => ApplyFactors(RecordsNode[i].Mzz_Reac, factorsMoment)).Cast<object>().ToList() },
        { "|m|", indices.Select(i => ApplyFactors(RecordsNode[i].M_Reac.Value, factorsMoment)).Cast<object>().ToList() }
      };
      return retDict;
    }

    protected Dictionary<string, List<object>> ResultTypeColumnValues_ConstraintForces(List<int> indices)
    {
      var factorsForce = GetFactors(ResultUnitType.Force);
      var factorsMoment = GetFactors(ResultUnitType.Force, ResultUnitType.Length);
      var retDict = new Dictionary<string, List<object>>
      {
        { "fx", indices.Select(i => ApplyFactors(RecordsNode[i].Fx_Cons, factorsForce)).Cast<object>().ToList() },
        { "fy", indices.Select(i => ApplyFactors(RecordsNode[i].Fy_Cons, factorsForce)).Cast<object>().ToList() },
        { "fz", indices.Select(i => ApplyFactors(RecordsNode[i].Fz_Cons, factorsForce)).Cast<object>().ToList() },
        { "|f|", indices.Select(i => ApplyFactors(RecordsNode[i].F_Cons.Value, factorsForce)).Cast<object>().ToList() },
        { "mxx", indices.Select(i => ApplyFactors(RecordsNode[i].Mxx_Cons, factorsMoment)).Cast<object>().ToList() },
        { "myy", indices.Select(i => ApplyFactors(RecordsNode[i].Myy_Cons, factorsMoment)).Cast<object>().ToList() },
        { "mzz", indices.Select(i => ApplyFactors(RecordsNode[i].Mzz_Cons, factorsMoment)).Cast<object>().ToList() },
        { "|m|", indices.Select(i => ApplyFactors(RecordsNode[i].M_Cons.Value, factorsMoment)).Cast<object>().ToList() }
      };
      return retDict;
    }
    #endregion
  }
}
