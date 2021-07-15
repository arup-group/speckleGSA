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
  public class Results2dProcessor2 : ResultsProcessorBase
  {
    protected FileToResultTableSpec spec;
    protected string filePath;
    protected HashSet<string> cases;
    protected HashSet<int> elemIds;
    protected List<ResultType> resultTypes;

    protected Dictionary<int, CsvElem2d> Records = new Dictionary<int, CsvElem2d>();
    protected Dictionary<int, Dictionary<string, List<int>>> FaceRecordIndices = new Dictionary<int, Dictionary<string, List<int>>>();
    protected Dictionary<int, Dictionary<string, List<int>>> VertexRecordIndices = new Dictionary<int, Dictionary<string, List<int>>>();
    protected List<string> orderedCases = null; // will be updated in the first call to GetResultHierarchy

    protected static Dictionary<ResultType, Func<List<int>, Dictionary<string, List<object>>>> ColumnValuesFns;

    public List<int> ElementIds => elemIds.OrderBy(i => i).ToList();
    public List<string> CaseIds => cases.OrderBy(c => c).ToList();

    public Results2dProcessor2(FileToResultTableSpec spec, string filePath, List<ResultType> resultTypes = null, 
      List<string> cases = null, List<int> elemIds = null)
    {
      this.spec = spec;
      this.filePath = filePath;
      if (resultTypes == null)
      {
        this.resultTypes = new List<ResultType> 
        { 
          ResultType.Element2dDisplacement, 
          ResultType.Element2dProjectedForce, 
          ResultType.Element2dProjectedMoment, 
          ResultType.Element2dProjectedStressBottom, 
          ResultType.Element2dProjectedStressMiddle, 
          ResultType.Element2dProjectedStressTop 
        };
      }
      else
      {
        this.resultTypes = new List<ResultType>();
        //this ensures only the 2D result types are considered
        foreach (var rt in resultTypes)
        {
          if (rt == ResultType.Element2dDisplacement || rt == ResultType.Element2dProjectedForce || rt == ResultType.Element2dProjectedMoment
            || rt == ResultType.Element2dProjectedStressBottom || rt == ResultType.Element2dProjectedStressMiddle || rt == ResultType.Element2dProjectedStressTop)
          {
            this.resultTypes.Add(rt);
          }
        }
      }
      if (cases != null)
      {
        this.cases = new HashSet<string>(cases);
      }
      if (elemIds != null)
      {
        this.elemIds = new HashSet<int>(elemIds);
      }

      ColumnValuesFns = new Dictionary<ResultType, Func<List<int>, Dictionary<string, List<object>>>>()
      {
        { ResultType.Element2dDisplacement, ResultTypeColumnValues_Element2dDisplacement },
        { ResultType.Element2dProjectedForce, ResultTypeColumnValues_Element2dProjectedForce },
        { ResultType.Element2dProjectedMoment, ResultTypeColumnValues_Element2dProjectedMoment },
        { ResultType.Element2dProjectedStressBottom, ResultTypeColumnValues_Element2dProjectedStressBottom },
        { ResultType.Element2dProjectedStressMiddle, ResultTypeColumnValues_Element2dProjectedStressMiddle },
        { ResultType.Element2dProjectedStressTop, ResultTypeColumnValues_Element2dProjectedStressTop }
      };
    }

    //Assume order is always correct
    //the hierarchy to compile, to be converted, is
    public bool LoadFromFile(bool parallel = true)
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
          var record = csv.GetRecord<CsvElem2d>();

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
            if (record.IsVertex)
            {
              if (!VertexRecordIndices.ContainsKey(record.ElemId))
              {
                VertexRecordIndices.Add(record.ElemId, new Dictionary<string, List<int>>());
              }
              if (!VertexRecordIndices[record.ElemId].ContainsKey(record.CaseId))
              {
                VertexRecordIndices[record.ElemId].Add(record.CaseId, new List<int>());
              }
              VertexRecordIndices[record.ElemId][record.CaseId].Add(rowIndex);
            }
            else
            {
              if (!FaceRecordIndices.ContainsKey(record.ElemId))
              {
                FaceRecordIndices.Add(record.ElemId, new Dictionary<string, List<int>>());
              }
              if (!FaceRecordIndices[record.ElemId].ContainsKey(record.CaseId))
              {
                FaceRecordIndices[record.ElemId].Add(record.CaseId, new List<int>());
              }
              FaceRecordIndices[record.ElemId][record.CaseId].Add(rowIndex);
            }
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

      if (!VertexRecordIndices.ContainsKey(elemId) && !FaceRecordIndices.ContainsKey(elemId))
      {
        return null;
      }

      foreach (var caseId in orderedCases)
      {
        var indicesVertex = (VertexRecordIndices[elemId].ContainsKey(caseId)) ? VertexRecordIndices[elemId][caseId] : null;
        var indicesFace = (FaceRecordIndices[elemId].ContainsKey(caseId)) ? FaceRecordIndices[elemId][caseId] : null;

        if (indicesVertex != null && indicesVertex.Count > 0 && indicesFace != null && indicesFace.Count > 0)
        {
          var rtDict = new Dictionary<string, Dictionary<string, List<object>>>(resultTypes.Count * 2);
          foreach (var rt in resultTypes)
          {
            var name = ResultTypeName(rt);
            if (!string.IsNullOrEmpty(name))
            {
              rtDict.Add(name + "_face", ColumnValuesFns[rt](indicesFace));
              rtDict.Add(name + "_vertex", ColumnValuesFns[rt](indicesVertex));
            }
          }
          retDict.Add(caseId, rtDict);
        }
      }

      return retDict;
    }


    public Dictionary<string, List<object>> ResultTypeColumnValues_Element2dDisplacement(List<int> indices)
    {
      var retDict = new Dictionary<string, List<object>>
      {
        { "ux", indices.Select(i => Records[i].Ux).Cast<object>().ToList() },
        { "uy", indices.Select(i => Records[i].Uy).Cast<object>().ToList() },
        { "uz", indices.Select(i => Records[i].Uz).Cast<object>().ToList() },
        { "|u|", indices.Select(i => Records[i].U.Value).Cast<object>().ToList() }
      };
      return retDict;
    }

    protected Dictionary<string, List<object>> ResultTypeColumnValues_Element2dProjectedMoment(List<int> indices)
    {
      var retDict = new Dictionary<string, List<object>>
      {
        { "mx", indices.Select(i => Records[i].Mx).Cast<object>().ToList() },
        { "my", indices.Select(i => Records[i].My).Cast<object>().ToList() },
        { "mxy", indices.Select(i => Records[i].Mxy).Cast<object>().ToList() },
        { "mx+mxy", indices.Select(i => Records[i].Mx_Mxy.Value).Cast<object>().ToList() },
        { "my+myx", indices.Select(i => Records[i].My_Myx.Value).Cast<object>().ToList() }
      };
      return retDict;
    }

    protected Dictionary<string, List<object>> ResultTypeColumnValues_Element2dProjectedForce(List<int> indices)
    {
      var retDict = new Dictionary<string, List<object>>
      {
        { "nx", indices.Select(i => Records[i].Nx).Cast<object>().ToList() },
        { "ny", indices.Select(i => Records[i].Ny).Cast<object>().ToList() },
        { "nxy", indices.Select(i => Records[i].Nxy).Cast<object>().ToList() },
        { "qx", indices.Select(i => Records[i].Qx).Cast<object>().ToList() },
        { "qy", indices.Select(i => Records[i].Qy).Cast<object>().ToList() }
      };
      return retDict;
    }

    protected Dictionary<string, List<object>> ResultTypeColumnValues_Element2dProjectedStressBottom(List<int> indices)
    {
      var retDict = new Dictionary<string, List<object>>
      {
        { "xx", indices.Select(i => Records[i].Xx_b).Cast<object>().ToList() },
        { "yy", indices.Select(i => Records[i].Yy_b).Cast<object>().ToList() },
        { "zz", indices.Select(i => Records[i].Zz_b).Cast<object>().ToList() },
        { "xy", indices.Select(i => Records[i].Xy_b).Cast<object>().ToList() },
        { "yz", indices.Select(i => Records[i].Yz_b).Cast<object>().ToList() },
        { "zx", indices.Select(i => Records[i].Zx_b).Cast<object>().ToList() }
      };
      return retDict;
    }

    protected Dictionary<string, List<object>> ResultTypeColumnValues_Element2dProjectedStressMiddle(List<int> indices)
    {
      var retDict = new Dictionary<string, List<object>>
      {
        { "xx", indices.Select(i => Records[i].Xx_m).Cast<object>().ToList() },
        { "yy", indices.Select(i => Records[i].Yy_m).Cast<object>().ToList() },
        { "zz", indices.Select(i => Records[i].Zz_m).Cast<object>().ToList() },
        { "xy", indices.Select(i => Records[i].Xy_m).Cast<object>().ToList() },
        { "yz", indices.Select(i => Records[i].Yz_m).Cast<object>().ToList() },
        { "zx", indices.Select(i => Records[i].Zx_m).Cast<object>().ToList() }
      };
      return retDict;
    }

    protected Dictionary<string, List<object>> ResultTypeColumnValues_Element2dProjectedStressTop(List<int> indices)
    {
      var retDict = new Dictionary<string, List<object>>
      {
        { "xx", indices.Select(i => Records[i].Xx_t).Cast<object>().ToList() },
        { "yy", indices.Select(i => Records[i].Yy_t).Cast<object>().ToList() },
        { "zz", indices.Select(i => Records[i].Zz_t).Cast<object>().ToList() },
        { "xy", indices.Select(i => Records[i].Xy_t).Cast<object>().ToList() },
        { "yz", indices.Select(i => Records[i].Yz_t).Cast<object>().ToList() },
        { "zx", indices.Select(i => Records[i].Zx_t).Cast<object>().ToList() }
      };
      return retDict;
    }

    private bool SendableValue(object v)
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
