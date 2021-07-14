using SpeckleCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSAProxy.Test.ResultsTest
{
  public static class Helper
  {
    //For insertion into the Result.Value property
    // takes in   [ result_type, [ [ headers ], [ row, column ] ] ]
    // returns    [ load case [ result type [ column [ values ] ] ] ]
    public static Dictionary<string, Dictionary<string, object>> GetSpeckleResultHierarchy(Dictionary<string, Tuple<List<string>, List<object[]>>> data,
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

    private static bool SendableValue(object v)
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
