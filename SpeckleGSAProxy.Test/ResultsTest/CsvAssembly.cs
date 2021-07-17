using CsvHelper.Configuration.Attributes;
using System;

namespace SpeckleGSAProxy.Test.ResultsTest
{
  public class CsvAssembly : CsvRecord
  {
    [Name("id")]
    public int ElemId { get; set; }

    [Name("case_id")]
    public string CaseId { get; set; }

    [Name("force_x")]
    public float Fx { get; set; }

    [Name("force_y")]
    public float Fy { get; set; }

    [Name("force_z")]
    public float Fz { get; set; }

    public float? Frc { get => Magnitude(Fx, Fy, Fz); }

    [Name("moment_x")]
    public float Mxx { get; set; }

    [Name("moment_y")]
    public float Myy { get; set; }

    [Name("moment_z")]
    public float Mzz { get; set; }

    public float? Mom { get => Magnitude(Mxx, Myy, Mzz); }
  }
}
