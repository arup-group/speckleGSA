using CsvHelper;
using SpeckleGSAInterfaces;
using SpeckleGSAProxy.Results;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SpeckleGSAProxy.Test.ResultsTest
{
  public abstract class ResultsProcessorBase
  {
    protected string filePath;
    protected HashSet<string> cases;
    protected HashSet<int> elemIds;
    protected Dictionary<int, CsvRecord> Records = new Dictionary<int, CsvRecord>();
    protected Dictionary<ResultType, Func<List<int>, Dictionary<string, List<object>>>> ColumnValuesFns;
    protected Dictionary<ResultUnitType, double> unitData;
    protected List<string> orderedCases = null; // will be updated in the first call to GetResultHierarchy
    protected List<ResultType> resultTypes;

    protected static Dictionary<ResultType, string> rtStrings = new Dictionary<ResultType, string>
    {
      { ResultType.NodalDisplacements, "Nodal Displacements" },
      { ResultType.NodalVelocity, "Nodal Velocity" },
      { ResultType.NodalAcceleration, "Nodal Acceleration" },
      { ResultType.NodalReaction, "Nodal Reaction" },
      { ResultType.ConstraintForces, "Constraint Forces" },
      { ResultType.Element1dDisplacement, "1D Element Displacement" },
      { ResultType.Element1dForce, "1D Element Force" },
      { ResultType.Element2dDisplacement, "2D Element Displacement" },
      { ResultType.Element2dProjectedMoment, "2D Element Projected Moment" },
      { ResultType.Element2dProjectedForce, "2D Element Projected Force" },
      { ResultType.Element2dProjectedStressBottom, "2D Element Projected Stress - Bottom" },
      { ResultType.Element2dProjectedStressMiddle, "2D Element Projected Stress - Middle" },
      { ResultType.Element2dProjectedStressTop, "2D Element Projected Stress - Top" },
      { ResultType.AssemblyForcesAndMoments, "Assembly Forces and Moments" }
    };

    protected Dictionary<int, Dictionary<string, List<int>>> RecordIndices = new Dictionary<int, Dictionary<string, List<int>>>();

    public string ResultTypeName(ResultType rt) => rtStrings[rt];
    public List<int> ElementIds => elemIds.OrderBy(i => i).ToList();
    public List<string> CaseIds => cases.OrderBy(c => c).ToList();
    public abstract ResultCsvGroup Group { get; }

    public ResultsProcessorBase(string filePath, Dictionary<ResultUnitType, double> unitData, List<string> cases = null, List<int> elemIds = null)
    {
      this.filePath = filePath;
      if (cases != null)
      {
        this.cases = new HashSet<string>(cases);
      }
      if (elemIds != null)
      {
        this.elemIds = new HashSet<int>(elemIds);
      }
      this.unitData = unitData;
    }

    public abstract bool LoadFromFile(bool parallel = true);

    protected bool LoadFromFile<T>(bool parallel = true) where T: CsvRecord
    {
      var reader = new StreamReader(filePath);

      var tasks = new List<Task>();

      int rowIndex = 0;

      var foundCases = new HashSet<string>();
      var foundElems = new HashSet<int>();

      // [ result_type, [ [ headers ], [ row, column ] ] ]

      using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
      {
        csv.Read();
        csv.ReadHeader();

        while (csv.Read())
        {
          var record = csv.GetRecord<T>();

          if (elemIds == null && !foundElems.Contains(record.ElemId))
          {
            foundElems.Add(record.ElemId);
          }
          if (cases == null && !foundCases.Contains(record.CaseId))
          {
            foundCases.Add(record.CaseId);
          }

          if ((elemIds == null || elemIds.Contains(record.ElemId)) && ((cases == null) || (cases.Contains(record.CaseId))))
          {
            Records.Add(rowIndex, record);
            if (!RecordIndices.ContainsKey(record.ElemId))
            {
              RecordIndices.Add(record.ElemId, new Dictionary<string, List<int>>());
            }
            if (!RecordIndices[record.ElemId].ContainsKey(record.CaseId))
            {
              RecordIndices[record.ElemId].Add(record.CaseId, new List<int>());
            }
            RecordIndices[record.ElemId][record.CaseId].Add(rowIndex);
          }

          rowIndex++;
        }
      }

      if (elemIds == null)
      {
        this.elemIds = foundElems;
      }
      if (cases == null)
      {
        this.cases = foundCases;
      }

      this.orderedCases = this.cases.OrderBy(c => c).ToList();

      reader.Close();
      return true;
    }

    // For both embedded and separate results, the format needs to be, per element:
    // [ load_case [ result_type [ column [ values ] ] ] ]
    public Dictionary<string, object> GetResultHierarchy(int elemId)
    {
      var retDict = new Dictionary<string, object>();

      if (!RecordIndices.ContainsKey(elemId))
      {
        return null;
      }

      foreach (var caseId in orderedCases)
      {
        var indices = (RecordIndices[elemId].ContainsKey(caseId)) ? RecordIndices[elemId][caseId] : null;

        if (indices != null && indices.Count > 0)
        {
          var rtDict = new Dictionary<string, Dictionary<string, List<object>>>(resultTypes.Count * 2);
          foreach (var rt in resultTypes)
          {
            var name = ResultTypeName(rt);
            if (!string.IsNullOrEmpty(name))
            {
              rtDict.Add(name, ColumnValuesFns[rt](indices));
            }
          }
          retDict.Add(caseId, rtDict);
        }
      }

      return retDict;
    }

    protected float ApplyFactors(float val, List<double> factors)
    {
      if (factors == null || factors.Count() == 0)
      {
        return val;
      }
      if ((float)val == 0)
      {
        return val;
      }
      foreach (var f in factors)
      {
        val = (float)((float)val * f);
      }
      return val;
    }

    protected List<double> GetFactors(params ResultUnitType[] ruts)
    {
      return ruts.Where(r => unitData.ContainsKey(r)).Select(r => unitData[r]).ToList();
    }

    protected bool SendableValue(object v)
    {
      if (v == null)
      {
        return false;
      }
      if (v is int)
      {
        return ((int)v != 0);
      }
      else if (v is float)
      {
        return ((float)v != 0);
      }
      else if (v is double)
      {
        return ((double)v != 0);
      }
      else if (v is string)
      {
        return (!string.IsNullOrEmpty((string)v) && !((string)v).Equals("null", StringComparison.InvariantCultureIgnoreCase));
      }
      return true;
    }
  }
}
