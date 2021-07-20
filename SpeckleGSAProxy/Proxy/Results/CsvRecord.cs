using CsvHelper.Configuration.Attributes;
using System;
using System.Linq;

namespace SpeckleGSAProxy
{
  public class CsvRecord
  {
    [Name("id")]
    public int ElemId { get; set; }

    [Name("case_id")]
    public string CaseId { get; set; }

    protected float? Magnitude(params float?[] dims)
    { 
      if (dims.Any(v => !v.HasValue))
      {
        return null;
      }
      return Magnitude(dims.Select(v => v.Value).ToArray());
    }

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
