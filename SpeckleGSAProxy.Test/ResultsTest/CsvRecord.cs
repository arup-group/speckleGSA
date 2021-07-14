using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSAProxy.Test.ResultsTest
{
  public class CsvRecord
  {
    protected float? Magnitude(params float[] dims)
    {
      if (dims.Length < 2)
      {
        return null;
      }
      var vals = dims.Cast<float>().ToArray();
      return (float?)Math.Sqrt(vals.Select(d => Math.Pow((float)d, 2)).Sum());
    }

    protected float? MomentResult(params float[] dims)
    {
      if (dims.Length < 2)
      {
        return null;
      }
      var first = (float)dims.First();
      var last = (float)dims.Last();
      var magnitude = Math.Abs(first) + Math.Abs(last);
      return (first < 0) ? (-1) * magnitude : magnitude;
    }
  }
}
