using CsvHelper;
using SpeckleGSAProxy.Results;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SpeckleGSAProxy.Test.ResultsTest
{
  public class Results2dProcessor
  {
    protected FileToResultTableSpec spec;
    protected string filePath;
    protected HashSet<string> cases;
    protected HashSet<int> elemIds;
    protected Dictionary<string, List<int>> HeaderIndicesByResultType = new Dictionary<string, List<int>>();

    public string TableName;
    public Dictionary<string, int> Headers = new Dictionary<string, int>();
    public List<int> ErrRowIndices;

   protected struct HierarchyInfo
    {
      public int ElemId;
      public string CaseId;
      public Dictionary<string, Dictionary<string, object>> Hierarchy;
    }

    //protected List<HierarchyInfo> Hierarchies = new List<HierarchyInfo>();
    protected Dictionary<int, HierarchyInfo> Hierarchies = new Dictionary<int, HierarchyInfo>();
    protected int lastHierarchyIndex = -1;
    protected Dictionary<int, HashSet<int>> HierarchyInfoByElemId = new Dictionary<int, HashSet<int>>();
    protected Dictionary<string, HashSet<int>> HierarchyInfoByCaseId = new Dictionary<string, HashSet<int>>();
    protected object hierarchyUpsertLock = new object();

    public Results2dProcessor(FileToResultTableSpec spec, string filePath, List<string> cases = null, List<int> elemIds = null)
    {
      this.spec = spec;
      this.filePath = filePath;
      if (cases != null)
      {
        this.cases = new HashSet<string>(cases);
      }
      if (elemIds != null)
      {
        this.elemIds = new HashSet<int>(elemIds);
      }
    }

    //Assume order is always correct
    //the hierarchy to compile, to be converted, is
    public bool LoadFromFile(bool parallel = true)
    {
      var reader = new StreamReader(filePath);

      int currElemId = 0;
      string currCaseId = null;
      string prevCaseId = null;
      int? prevElemId = null;
      Dictionary<string, List<object[]>> currVertexElemRowsByResultType = null;

      var tasks = new List<Task>();

      // [ result_type, [ [ headers ], [ row, column ] ] ]

      using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
      {       
        var records = new List<object>();
        csv.Read();
        csv.ReadHeader();

        var fileHeaders = csv.HeaderRecord.ToList();

        PopulateHeaderIndices(fileHeaders);

        var headersByResultType = HeaderIndicesByResultType.Keys.ToDictionary(rt => rt, rt => HeaderIndicesByResultType[rt].Select(i => fileHeaders[i]).ToList());
        var numHeadersByResultType = HeaderIndicesByResultType.Keys.ToDictionary(rt => rt, rt => HeaderIndicesByResultType[rt].Count());
        var caseHeaderIndex = fileHeaders.IndexOf(spec.CaseIdCol);

        int rowIndex = 0;
        int currNumVertexRows = 0;
        while (csv.Read())
        {
          currCaseId = csv.GetField<string>(spec.CaseIdCol);
          currElemId = csv.GetField<int>(spec.ElementIdCol);
          var posR = Math.Round(csv.GetField<double>("position_r"), 1);
          var isFace = !(posR == 0 || posR == 1);

          //TestContext.Progress.WriteLine("currElemId=" + currElemId + " prevElemId=" + (prevElemId ?? -1) + " currCaseId=" + currCaseId + " prevCaseId=" + prevCaseId + "currNumVertexRows=" + currNumVertexRows);

          //Wrap up vertex set and initialise the next set
          if (currElemId != prevElemId)
          {
            if (prevElemId.HasValue && (elemIds == null || elemIds.Contains(prevElemId.Value)) && currNumVertexRows > 0)
            {
              if (parallel)
              {
                var tempElemId = prevElemId.Value;
                var tempCaseId = prevCaseId;
                var copyVertexElemRowsByResultType = CopyVertexRowDictionary(currVertexElemRowsByResultType);
                //TestContext.Progress.WriteLine("COPIED VERTEX DICTIONARY currElemId=" + currElemId + " prevElemId=" + tempElemId + " prevCaseId=" + tempCaseId + " currNumVertexRows=" + currNumVertexRows);
                var t = new Task(() => AddVertexRowsToHierarchies(tempElemId, tempCaseId, copyVertexElemRowsByResultType, headersByResultType));
                tasks.Add(t);
                t.Start();
                //TestContext.Progress.WriteLine("TRIGGERED VERTEX ADD prevElemId=" + tempElemId + " prevCaseId=" + tempCaseId + "currNumVertexRows=" + currNumVertexRows);
              }
              else
              {
                AddVertexRowsToHierarchies(prevElemId.Value, prevCaseId, currVertexElemRowsByResultType, headersByResultType);
              }
            }
            //Start new set of data
            currVertexElemRowsByResultType = new Dictionary<string, List<object[]>>();
            currNumVertexRows = 0;

            if ((elemIds == null || elemIds.Contains(currElemId)) && (cases == null || cases.Contains(currCaseId)))
            {
              foreach (var rt in HeaderIndicesByResultType.Keys)
              {
                currVertexElemRowsByResultType.Add(rt, new List<object[]>());
              }
            }
          }

          if ((elemIds == null || elemIds.Contains(currElemId)) && (cases == null || cases.Contains(currCaseId)))
          {
            Dictionary<string, object[]> currFaceElemRowByResultType = null;
            if (isFace)
            {
              currFaceElemRowByResultType = new Dictionary<string, object[]>();
            }

            foreach (var rt in HeaderIndicesByResultType.Keys)
            {
              var vals = new object[numHeadersByResultType[rt]];
              for (int c = 0; c < numHeadersByResultType[rt]; c++)
              {
                var fieldIndex = HeaderIndicesByResultType[rt][c];
                if (fieldIndex == caseHeaderIndex)
                {
                  vals[c] = csv.GetField<string>(spec.CaseIdCol);
                }
                else
                {
                  vals[c] = csv.GetField<float?>(fileHeaders[fieldIndex]);
                }
              }
              if (isFace)
              {
                currFaceElemRowByResultType[rt] = vals;
              }
              else
              {
                currVertexElemRowsByResultType[rt].Add(vals);
              }
            }

            if (isFace)
            {
              if (parallel)
              {
                //Shallow copy of the dictionary
                var copyFaceElemRowByResultType = new Dictionary<string, object[]>(currFaceElemRowByResultType);
                //TestContext.Progress.WriteLine("COPIED FACE DICTIONARY currElemId=" + currElemId + " prevElemId=" + (prevElemId ?? -1) + " currCaseId=" + currCaseId + " prevCaseId=" + prevCaseId + "currNumVertexRows=" + currNumVertexRows);
                var tempElemId = currElemId;
                var tempCaseId = currCaseId;
                var t = new Task(() => AddFaceRowToHierarchies(tempElemId, tempCaseId, copyFaceElemRowByResultType, headersByResultType));
                
                tasks.Add(t);
                t.Start();
                //TestContext.Progress.WriteLine("TRIGGERED FACE ADD currElemId=" + tempElemId + " prevElemId=" + (prevElemId ?? -1) + " currCaseId=" + tempCaseId + " prevCaseId=" + prevCaseId + "currNumVertexRows=" + currNumVertexRows);
              }
              else
              {
                AddFaceRowToHierarchies(currElemId, currCaseId, currFaceElemRowByResultType, headersByResultType);
              }
            }
            else
            {
              currNumVertexRows++;
            }
          }

          prevCaseId = currCaseId;
          prevElemId = currElemId;
          if (!isFace)
          {
            //Only need to do this for the vertex lines - the face lines are just processed immediately
            //prevElemId = currElemId;
          }
          rowIndex++;
        }

        if (currVertexElemRowsByResultType != null && currNumVertexRows > 0 && currElemId > 0 && !string.IsNullOrEmpty(currCaseId))
        {
          if (parallel)
          {
            var copyVertexElemRowsByResultType = CopyVertexRowDictionary(currVertexElemRowsByResultType);
            var t = new Task(() => AddVertexRowsToHierarchies(currElemId, currCaseId, copyVertexElemRowsByResultType, headersByResultType));
            tasks.Add(t);
            t.Start();
          }
          else
          {
            AddVertexRowsToHierarchies(currElemId, currCaseId, currVertexElemRowsByResultType, headersByResultType);
          }
        }
      }

      if (parallel)
      {
        Task.WaitAll(tasks.ToArray());
      }
      reader.Close();
      return true;
    }

    private Dictionary<string, List<object[]>> CopyVertexRowDictionary(Dictionary<string, List<object[]>> orig)
    {
      //Ensure the dictionary given to the task can't be overwritten by another thread - by duplicating it
      var copyVertexElemRowsByResultType = new Dictionary<string, List<object[]>>();
      foreach (var rt in orig.Keys)
      {
        copyVertexElemRowsByResultType.Add(rt, new List<object[]>(orig[rt].Count));
        for (int r = 0; r < orig[rt].Count; r++)
        {
          var numVals = orig[rt][r].Length;
          var vals = new object[numVals];
          for (int v = 0; v < numVals; v++)
          {
            vals[v] = orig[rt][r][v];
          }
          copyVertexElemRowsByResultType[rt].Add(vals);
        }
      }
      return copyVertexElemRowsByResultType;
    }

    public Dictionary<string, Dictionary<string, object>> GetHierarchy(int elemId, string caseId)
    {
      if (HierarchyInfoByElemId.ContainsKey(elemId) && HierarchyInfoByCaseId.ContainsKey(caseId))
      {
        var indices = HierarchyInfoByElemId[elemId].Intersect(HierarchyInfoByCaseId[caseId]);
        if (indices != null && indices.Any())
        {
          return Hierarchies[indices.First()].Hierarchy;
        }
      }
      return null;
    }

    private void PopulateHeaderIndices(List<string> fileCols)
    {
      var caseIndex = fileCols.IndexOf(spec.CaseIdCol);
      foreach (var rt in spec.ResultTypeCsvColumnMap.Keys)
      {
        HeaderIndicesByResultType.Add(rt, new List<int>() { caseIndex });
        foreach (var of in spec.ResultTypeCsvColumnMap[rt].OrderedColumns)
        {
          if (spec.ResultTypeCsvColumnMap[rt].FileCols.ContainsKey(of))
          {
            var fileCol = spec.ResultTypeCsvColumnMap[rt].FileCols[of].FileCol;
            var fileColIndex = fileCols.IndexOf(fileCol);
            //It's an imported field
            HeaderIndicesByResultType[rt].Add(fileColIndex);
            if (!Headers.ContainsKey(fileCol))
            {
              Headers.Add(fileCol, fileColIndex);
            }
          }
        }
      }
    }

    private void CreateAndUpsertHierarchy(int elemId, string caseId, Dictionary<string, Tuple<List<string>, List<object[]>>> data)
    {
      //var hierarchy = Helper.GetSpeckleResultHierarchy(data, false, spec.CaseIdCol);
      var hierarchy = GetSpeckleResultHierarchy(data, false, spec.CaseIdCol);
      var loadCaseHierarchy = hierarchy.First().Value;

      //TestContext.Progress.WriteLine("About to add for " + elemId + " case: " + caseId);

      lock (hierarchyUpsertLock)
      {
        if (!HierarchyInfoByElemId.ContainsKey(elemId))
        {
          HierarchyInfoByElemId.Add(elemId, new HashSet<int>());
        }
        if (!HierarchyInfoByCaseId.ContainsKey(caseId))
        {
          HierarchyInfoByCaseId.Add(caseId, new HashSet<int>());
        }

        var indices = HierarchyInfoByElemId[elemId].Intersect(HierarchyInfoByCaseId[caseId]);
        if (indices == null || !indices.Any())
        {
          var hierarchyInfo = new HierarchyInfo { Hierarchy = hierarchy, CaseId = caseId, ElemId = elemId };
          lastHierarchyIndex++;
          Hierarchies.Add(lastHierarchyIndex, hierarchyInfo);
          HierarchyInfoByElemId[elemId].Add(lastHierarchyIndex);
          HierarchyInfoByCaseId[caseId].Add(lastHierarchyIndex);
          //TestContext.Progress.WriteLine("New for " + elemId + " case: " + caseId);
        }
        else
        {
          //Merge hierarchies - note: they'd differ on result type
          var hierarchyIndex = indices.First();
          var hierarchyInfo = Hierarchies[hierarchyIndex];
          foreach (var rt in loadCaseHierarchy.Keys)
          {
            hierarchyInfo.Hierarchy.First().Value.Add(rt, (Dictionary<string, object>)loadCaseHierarchy[rt]);
          }
          if (HierarchyInfoByElemId[elemId].Contains(hierarchyIndex))
          {
            HierarchyInfoByElemId[elemId].Add(hierarchyIndex);
          }
          if (HierarchyInfoByCaseId[caseId].Contains(hierarchyIndex))
          {
            HierarchyInfoByCaseId[caseId].Add(hierarchyIndex);
          }
          //TestContext.Progress.WriteLine("Added to " + elemId + " case: " + caseId);
        }
      }
    }

    private void AddFaceRowToHierarchies(int currElemId, string caseId, Dictionary<string, object[]> currFaceElemRowByResultType, Dictionary<string, List<string>> headersByResultType)
    {
      //Wrap up the last set of data before starting anew
      var data = new Dictionary<string, Tuple<List<string>, List<object[]>>>();
      foreach (var rt in HeaderIndicesByResultType.Keys)
      {
        data.Add(rt + "_face", new Tuple<List<string>, List<object[]>>(headersByResultType[rt], new List<object[]> { currFaceElemRowByResultType[rt] }));
      }

      CreateAndUpsertHierarchy(currElemId, caseId, data);
    }
    
    private void AddVertexRowsToHierarchies(int currElemId, string caseId, Dictionary<string, List<object[]>> currVertexElemRowsByResultType, Dictionary<string, List<string>> headersByResultType)
    {
      //Wrap up the last set of data before starting anew
      var data = new Dictionary<string, Tuple<List<string>, List<object[]>>>();
      foreach (var rt in HeaderIndicesByResultType.Keys)
      {
        data.Add(rt + "_vertex", new Tuple<List<string>, List<object[]>>(headersByResultType[rt], currVertexElemRowsByResultType[rt]));
      }

      CreateAndUpsertHierarchy(currElemId, caseId, data);
    }

    private Dictionary<string, Dictionary<string, object>> GetSpeckleResultHierarchy(Dictionary<string, Tuple<List<string>, List<object[]>>> data,
      bool simplifySingleItemLists = true, string caseCol = "case_id")
    {
      //This stores ALL the data in this one pass
      var value = new Dictionary<string, Dictionary<string, object>>();
      //This stores where there is at least one non-zero/null/"null" value in the whole result type, across all columns
      var sendableValues = new Dictionary<string, Dictionary<string, bool>>();
      //This stores the number of values in each column: [ load case [ result type [ col, num values ] ] ]
      var numColValues = new Dictionary<string, Dictionary<string, Dictionary<string, int>>>();

      //This loop has been designed with the intention that the data is traversed *once*

      //Each result type (e.g. "Nodal Velocity")
      foreach (var rt in data.Keys)
      {
        int caseColIndex = data[rt].Item1.IndexOf(caseCol);
        for (var r = 0; r < data[rt].Item2.Count(); r++)
        {
          var loadCase = data[rt].Item2[r][caseColIndex].ToString();
          if (!value.Keys.Contains(loadCase))
          {
            value.Add(loadCase, new Dictionary<string, object>());
          }
          if (!value[loadCase].ContainsKey(rt))
          {
            value[loadCase].Add(rt, new Dictionary<string, object>());
          }
          foreach (var c in Enumerable.Range(0, data[rt].Item1.Count()).Except(new[] { caseColIndex }))
          {
            var col = data[rt].Item1[c];
            var val = data[rt].Item2[r][c];
            if (!((Dictionary<string, object>)value[loadCase][rt]).ContainsKey(col))
            {
              ((Dictionary<string, object>)value[loadCase][rt]).Add(col, new List<object>());
            }
            ((List<object>)((Dictionary<string, object>)value[loadCase][rt])[col]).Add(val);
            if (!sendableValues.ContainsKey(loadCase))
            {
              sendableValues.Add(loadCase, new Dictionary<string, bool>());
            }
            var sendable = SendableValue(val);
            if (!sendableValues[loadCase].ContainsKey(rt))
            {
              sendableValues[loadCase].Add(rt, sendable);
            }
            else if (!sendableValues[loadCase][rt])
            {
              sendableValues[loadCase][rt] = sendable;
            }
            if (!numColValues.ContainsKey(loadCase))
            {
              numColValues.Add(loadCase, new Dictionary<string, Dictionary<string, int>>());
            }
            if (!numColValues[loadCase].ContainsKey(rt))
            {
              numColValues[loadCase].Add(rt, new Dictionary<string, int>());
            }
            if (!numColValues[loadCase][rt].ContainsKey(col))
            {
              numColValues[loadCase][rt].Add(col, 1);
            }
            else
            {
              numColValues[loadCase][rt][col]++;
            }
          }
        }
      }

      var retValue = new Dictionary<string, Dictionary<string, object>>();
      foreach (var loadCase in sendableValues.Keys)
      {
        foreach (var rt in sendableValues[loadCase].Keys.Where(k => sendableValues[loadCase][k]))
        {
          if (!retValue.ContainsKey(loadCase))
          {
            retValue.Add(loadCase, new Dictionary<string, object>());
          }
          foreach (var col in ((Dictionary<string, object>)value[loadCase][rt]).Keys)
          {
            var colValues = ((List<object>)((Dictionary<string, object>)value[loadCase][rt])[col]);
          }
          retValue[loadCase].Add(rt, value[loadCase][rt]);
        }
      }

      if (simplifySingleItemLists)
      {
        foreach (var loadCase in retValue.Keys)
        {
          foreach (var rt in retValue[loadCase].Keys)
          {
            var singleValueCols = ((Dictionary<string, object>)retValue[loadCase][rt]).Keys.Where(k => numColValues[loadCase][rt][k] == 1).ToList();
            foreach (var col in singleValueCols)
            {
              ((Dictionary<string, object>)retValue[loadCase][rt])[col] = ((List<object>)((Dictionary<string, object>)value[loadCase][rt])[col]).First();
            }
          }
        }
      }

      return retValue;
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
