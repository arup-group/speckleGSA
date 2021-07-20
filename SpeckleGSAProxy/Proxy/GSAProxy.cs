using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Interop.Gsa_10_1;
using SpeckleGSAInterfaces;
using SpeckleGSAProxy.Results;

namespace SpeckleGSAProxy
{
  public class GSAProxy : IGSALocalProxy
  {
    //Hardwired values for interacting with the GSA instance
    //----
    private static readonly string SID_APPID_TAG = "speckle_app_id";
    private static readonly string SID_STRID_TAG = "speckle_stream_id";

    public static Dictionary<ResultType, string> rtStrings = new Dictionary<ResultType, string>
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

    public static readonly char GwaDelimiter = '\t';

    //These are the exceptions to the rule that, in GSA, all records that relate to each table (i.e. the set with mutually-exclusive indices) have the same keyword
    public static Dictionary<string, string[]> IrregularKeywordGroups = new Dictionary<string, string[]> {
      { "LOAD_BEAM", new string[] { "LOAD_BEAM_POINT", "LOAD_BEAM_UDL", "LOAD_BEAM_LINE", "LOAD_BEAM_PATCH", "LOAD_BEAM_TRILIN" } }
    };

    //Note that When a GET_ALL is called for LOAD_BEAM, it returns LOAD_BEAM_UDL, LOAD_BEAM_LINE, LOAD_BEAM_PATCH and LOAD_BEAM_TRILIN
    public static string[] SetAtKeywords = new string[] { "LOAD_NODE", "LOAD_BEAM", "LOAD_GRID_POINT", "LOAD_GRID_LINE", "LOAD_2D_FACE",
      "LOAD_GRID_AREA", "LOAD_2D_THERMAL", "LOAD_GRAVITY", "INF_BEAM", "INF_NODE", "RIGID", "GEN_REST" };
    //----

    //These are accessed via a lock
    private IComAuto GSAObject;
    private readonly List<string> batchSetGwa = new List<string>();
    private readonly List<string> batchBlankGwa = new List<string>();

    public string FilePath { get; set; }

    char IGSAProxy.GwaDelimiter => GSAProxy.GwaDelimiter;

    //Results-related
    private string resultDir = null;
    private Dictionary<ResultGroup, ResultsProcessorBase> resultProcessors = new Dictionary<ResultGroup, ResultsProcessorBase>();
    private List<ResultType> allResultTypes;

    //private IGSAResultsContext resultsContext = null;
    //private List<string> resultTypes = null;
    private List<string> cases = null;
    //This is the factor relative to the SI units (N, m, etc) that the model is currently set to - this is relevant for results as they're always
    //exported to CSV in SI units
    private Dictionary<ResultUnitType, double> unitData = new Dictionary<ResultUnitType, double>();

    /*
    private static Dictionary<ResultGroup, string> relativePathsToLoad = new Dictionary<ResultGroup, string>
      {
        {  ResultGroup.Node, @".\result_node\result_node.csv" },
        {  ResultGroup.Element1d, @".\result_elem_1d\result_elem_1d.csv" },
        {  ResultGroup.Element2d, @".\result_elem_2d\result_elem_2d.csv" },
        {  ResultGroup.Assembly, @".\result_assembly\result_assembly.csv" }
      };

    public static Dictionary<ResultGroup, FileToResultTableSpec> resultTypeSpecs = new Dictionary<ResultGroup, FileToResultTableSpec>()
    {
      {
        ResultGroup.Node, new FileToResultTableSpec("id", "case_id")
        {
          ResultTypeCsvColumnMap = new Dictionary<string, ColMap>()
          {
            {
              //Note: the element ID and case ID columns are default columns (not need to be specified here) which will be automatically added to the output
              "Nodal Displacements", new ColMap(
                new Dictionary<string, ImportedField>()
                {
                  { "ux", new ImportedField("disp_x", typeof(float), ResultUnitType.Length ) },
                  { "uy", new ImportedField("disp_y", typeof(float), ResultUnitType.Length) },
                  { "uz", new ImportedField("disp_z", typeof(float), ResultUnitType.Length) },
                  { "rxx", new ImportedField("disp_xx", typeof(float), ResultUnitType.Length) },
                  { "ryy", new ImportedField("disp_yy", typeof(float), ResultUnitType.Length) },
                  { "rzz", new ImportedField("disp_zz", typeof(float), ResultUnitType.Length) }
                },
                new Dictionary<string, CalculatedField>()
                {
                  //Note: the calculated field indices are the zero-based column numbers based on the spec above, *before* the default columns are added
                  { "|u|", new CalculatedField((v) => Magnitude(v), ResultUnitType.Length, 0, 1, 2) },
                  { "|r|", new CalculatedField((v) => Magnitude(v), ResultUnitType.Angle, 3, 4, 5) },
                  { "uxy", new CalculatedField((v) => Magnitude(v), ResultUnitType.Length, 0, 1) },
                },
                //The default columns will be added to the output too
                new List<string>() {  "ux", "uy", "uz", "|u|", "rxx", "ryy", "rzz", "|r|", "uxy" })
            },
            {
              "Nodal Velocity", new ColMap(
                new Dictionary<string, ImportedField>()
                {
                  { "vx", new ImportedField("vel_x",  typeof(float), new [] { ResultUnitType.Length, ResultUnitType.Time }) },
                  { "vy", new ImportedField("vel_y", typeof(float), new [] { ResultUnitType.Length, ResultUnitType.Time }) },
                  { "vz", new ImportedField("vel_z", typeof(float), new [] { ResultUnitType.Length, ResultUnitType.Time }) },
                  { "vxx", new ImportedField("vel_xx", typeof(float), new [] { ResultUnitType.Length, ResultUnitType.Time }) },
                  { "vyy", new ImportedField("vel_yy", typeof(float), new [] { ResultUnitType.Length, ResultUnitType.Time }) },
                  { "vzz", new ImportedField("vel_zz", typeof(float), new [] { ResultUnitType.Length, ResultUnitType.Time }) }
                },
                new Dictionary<string, CalculatedField>()
                {
                  { "|v|", new CalculatedField((v) => Magnitude(v), new [] { ResultUnitType.Length, ResultUnitType.Time }, 0, 1, 2) },
                  { "|r|", new CalculatedField((v) => Magnitude(v), new [] { ResultUnitType.Length, ResultUnitType.Time }, 3, 4, 5) }
                 },
                new List<string>() { "vx", "vy", "vz", "|v|", "vxx", "vyy", "vzz", "|r|" })
            },
            {
              "Nodal Acceleration", new ColMap(
                new Dictionary<string, ImportedField>()
                {
                  { "ax", new ImportedField("acc_x", typeof(float), ResultUnitType.Accel) },
                  { "ay", new ImportedField("acc_y", typeof(float), ResultUnitType.Accel) },
                  { "az", new ImportedField("acc_z", typeof(float), ResultUnitType.Accel) },
                  { "axx", new ImportedField("acc_xx", typeof(float), ResultUnitType.Accel) },
                  { "ayy", new ImportedField("acc_yy", typeof(float), ResultUnitType.Accel) },
                  { "azz", new ImportedField("acc_zz", typeof(float), ResultUnitType.Accel) }
                },
                new Dictionary<string, CalculatedField>()
                {
                  { "|a|", new CalculatedField((v) => Magnitude(v), ResultUnitType.Accel, 0, 1, 2) },
                  { "|r|", new CalculatedField((v) => Magnitude(v), ResultUnitType.Accel, 3, 4, 5) }
                 },
                new List<string>() { "ax", "ay", "az", "|a|", "axx", "ayy", "azz", "|r|" })
            },
            {
              "Nodal Reaction", new ColMap(
                new Dictionary<string, ImportedField>()
                {
                  { "fx", new ImportedField("reaction_x", typeof(float), ResultUnitType.Force) },
                  { "fy", new ImportedField("reaction_y", typeof(float), ResultUnitType.Force) },
                  { "fz", new ImportedField("reaction_z", typeof(float), ResultUnitType.Force) },
                  { "mxx", new ImportedField("reaction_xx", typeof(float), new [] { ResultUnitType.Force, ResultUnitType.Length }) },
                  { "myy", new ImportedField("reaction_yy", typeof(float), new [] { ResultUnitType.Force, ResultUnitType.Length }) },
                  { "mzz", new ImportedField("reaction_zz", typeof(float), new [] { ResultUnitType.Force, ResultUnitType.Length }) }
                },
                new Dictionary<string, CalculatedField>()
                {
                  { "|f|", new CalculatedField((v) => Magnitude(v), ResultUnitType.Force, 0, 1, 2) },
                  { "|m|", new CalculatedField((v) => Magnitude(v), new [] { ResultUnitType.Force, ResultUnitType.Length }, 3, 4, 5) }
                 },
                new List<string>() { "fx", "fy", "fz", "|f|", "mxx", "myy", "mzz", "|m|" })
            },
            {
              "Constraint Forces", new ColMap(
                new Dictionary<string, ImportedField>()
                {
                  { "fx", new ImportedField("constraint_x", typeof(float), ResultUnitType.Force) },
                  { "fy", new ImportedField("constraint_y", typeof(float), ResultUnitType.Force) },
                  { "fz", new ImportedField("constraint_z", typeof(float), ResultUnitType.Force) },
                  { "mxx", new ImportedField("constraint_xx", typeof(float), new [] { ResultUnitType.Force, ResultUnitType.Length }) },
                  { "myy", new ImportedField("constraint_yy", typeof(float), new [] { ResultUnitType.Force, ResultUnitType.Length }) },
                  { "mzz", new ImportedField("constraint_zz", typeof(float), new [] { ResultUnitType.Force, ResultUnitType.Length }) }
                },
                new Dictionary<string, CalculatedField>()
                {
                  { "|f|", new CalculatedField((v) => Magnitude(v), ResultUnitType.Force, 0, 1, 2) },
                  { "|m|", new CalculatedField((v) => Magnitude(v), new [] { ResultUnitType.Force, ResultUnitType.Length }, 3, 4, 5) }
                 },
                new List<string>() { "fx", "fy", "fz", "|f|", "mxx", "myy", "mzz", "|m|" })
            }
          }
        }
      },
      {
        ResultGroup.Element1d, new FileToResultTableSpec("id", "case_id")
        {
          ResultTypeCsvColumnMap = new Dictionary<string, ColMap>()
          {
            {
              "1D Element Displacement", new ColMap(
                new Dictionary<string, ImportedField>()
                {
                  { "ux", new ImportedField("disp_x", typeof(float), ResultUnitType.Length) },
                  { "uy", new ImportedField("disp_y",  typeof(float), ResultUnitType.Length) },
                  { "uz", new ImportedField("disp_z",  typeof(float), ResultUnitType.Length) }
                },
                new Dictionary<string, CalculatedField>()
                {
                  { "|u|", new CalculatedField((v) => Magnitude(v), ResultUnitType.Length, 0, 1, 2) }
                },
                new List<string>() { "ux", "uy", "uz", "|u|" })
            },
            {
              "1D Element Force", new ColMap(
                new Dictionary<string, ImportedField>()
                {
                  { "fx", new ImportedField("force_x", typeof(float), ResultUnitType.Force) },
                  { "fy", new ImportedField("force_y", typeof(float), ResultUnitType.Force) },
                  { "fz", new ImportedField("force_z", typeof(float), ResultUnitType.Force) },
                  { "mxx", new ImportedField("moment_x", typeof(float), new [] { ResultUnitType.Force, ResultUnitType.Length }) },
                  { "myy", new ImportedField("moment_y", typeof(float), new [] { ResultUnitType.Force, ResultUnitType.Length }) },
                  { "mzz", new ImportedField("moment_z", typeof(float), new [] { ResultUnitType.Force, ResultUnitType.Length }) }
                },
                new Dictionary<string, CalculatedField>()
                {
                  { "|f|", new CalculatedField((v) => Magnitude(v), ResultUnitType.Force, 0, 1, 2) },
                  { "|m|", new CalculatedField((v) => Magnitude(v), new [] { ResultUnitType.Force, ResultUnitType.Length }, 3, 4, 5) },
                  { "fyz", new CalculatedField((v) => Magnitude(v), ResultUnitType.Force, 1, 2) },
                  { "myz", new CalculatedField((v) => Magnitude(v), new [] { ResultUnitType.Force, ResultUnitType.Length }, 4, 5) }
                },
                new List<string>()  { "fx", "fy", "fz", "|f|", "mxx", "myy", "mzz", "|m|", "fyz", "myz" })
            }
          }
        }
      },
      {
        ResultGroup.Element2d, new FileToResultTableSpec("id", "case_id")
        {
          ResultTypeCsvColumnMap = new Dictionary<string, ColMap>()
          {
            {
              "2D Element Displacement", new ColMap(
                new Dictionary<string, ImportedField>()
                {
                  { "ux", new ImportedField("disp_x", typeof(float), ResultUnitType.Length) }, 
                  { "uy", new ImportedField("disp_y",  typeof(float), ResultUnitType.Length) }, 
                  { "uz", new ImportedField("disp_z",  typeof(float), ResultUnitType.Length) },
                  { "position_r", new ImportedField("position_r", typeof(float), ResultUnitType.None) },
                  { "position_s", new ImportedField("position_s", typeof(float), ResultUnitType.None) }
                },
                new Dictionary<string, CalculatedField>()
                {
                  { "|u|", new CalculatedField((v) => Magnitude(v), ResultUnitType.Length, 0, 1, 2) }
                },
                new List<string>() { "ux", "uy", "uz", "|u|", "position_r", "position_s" })
            },
            {
              "2D Element Projected Moment", new ColMap(
                new Dictionary<string, ImportedField>()
                {
                  { "mx", new ImportedField("moment_xx", typeof(float), new [] { ResultUnitType.Force, ResultUnitType.Length }) }, 
                  { "my", new ImportedField("moment_yy", typeof(float), new [] { ResultUnitType.Force, ResultUnitType.Length }) }, 
                  { "mxy", new ImportedField("moment_xy", typeof(float), new [] { ResultUnitType.Force, ResultUnitType.Length }) },
                  { "position_r", new ImportedField("position_r", typeof(float), ResultUnitType.None) },
                  { "position_s", new ImportedField("position_s", typeof(float), ResultUnitType.None) }
                },
                new Dictionary<string, CalculatedField>()
                {
                  { "mx+mxy", new CalculatedField((v) => MomentResult(v), new [] { ResultUnitType.Force, ResultUnitType.Length }, 0, 2) },
                  {  "my+myx", new CalculatedField((v) => MomentResult(v), new [] { ResultUnitType.Force, ResultUnitType.Length }, 1, 2) }
                },
                new List<string>()  { "mx", "my", "mxy", "mx+mxy", "my+myx", "position_r", "position_s" })
            },
            {
              "2D Element Projected Force", new ColMap(
                new Dictionary<string, ImportedField>()
                {
                  { "nx", new ImportedField("force_xx", typeof(float), new [] { ResultUnitType.Force, ResultUnitType.Length }) },
                  { "ny", new ImportedField("force_yy", typeof(float), new [] { ResultUnitType.Force, ResultUnitType.Length }) },
                  { "nxy", new ImportedField("force_xy", typeof(float), new [] { ResultUnitType.Force, ResultUnitType.Length }) },
                  { "qx", new ImportedField("shear_x", typeof(float), new [] { ResultUnitType.Force, ResultUnitType.Length }) },
                  { "qy", new ImportedField("shear_y", typeof(float), new [] { ResultUnitType.Force, ResultUnitType.Length }) },
                  { "position_r", new ImportedField("position_r", typeof(float), ResultUnitType.None) },
                  { "position_s", new ImportedField("position_s", typeof(float), ResultUnitType.None) }
                },
                null,
                new List<string>()  { "nx", "ny", "nxy", "qx", "qy", "position_r", "position_s"})
            },
            {
              "2D Element Projected Stress - Bottom", new ColMap(
                new Dictionary<string, ImportedField>()
                {
                  { "xx", new ImportedField("stress_bottom_xx", typeof(float), ResultUnitType.Stress) },
                  { "yy", new ImportedField("stress_bottom_yy", typeof(float), ResultUnitType.Stress) },
                  { "zz", new ImportedField("stress_bottom_zz", typeof(float), ResultUnitType.Stress) },
                  { "xy", new ImportedField("stress_bottom_xy", typeof(float), ResultUnitType.Stress) },
                  { "yz", new ImportedField("stress_bottom_yz", typeof(float), ResultUnitType.Stress) },
                  { "zx", new ImportedField("stress_bottom_zx", typeof(float), ResultUnitType.Stress) },
                  { "position_r", new ImportedField("position_r", typeof(float), ResultUnitType.None) },
                  { "position_s", new ImportedField("position_s", typeof(float), ResultUnitType.None) }
                },
                null,
                new List<string>()  { "xx", "yy", "zz", "xy", "yz", "zx", "position_r", "position_s"})
            },
            {
              "2D Element Projected Stress - Middle", new ColMap(
                new Dictionary<string, ImportedField>()
                {
                  { "xx", new ImportedField("stress_middle_xx", typeof(float), ResultUnitType.Stress) },
                  { "yy", new ImportedField("stress_middle_yy", typeof(float), ResultUnitType.Stress) },
                  { "zz", new ImportedField("stress_middle_zz", typeof(float), ResultUnitType.Stress) },
                  { "xy", new ImportedField("stress_middle_xy", typeof(float), ResultUnitType.Stress) },
                  { "yz", new ImportedField("stress_middle_yz", typeof(float), ResultUnitType.Stress) },
                  { "zx", new ImportedField("stress_middle_zx", typeof(float), ResultUnitType.Stress) },
                  { "position_r", new ImportedField("position_r", typeof(float), ResultUnitType.None) },
                  { "position_s", new ImportedField("position_s", typeof(float), ResultUnitType.None) }
                },
                null,
                new List<string>()  { "xx", "yy", "zz", "xy", "yz", "zx", "position_r", "position_s"})
            },
            {
              "2D Element Projected Stress - Top", new ColMap(
                new Dictionary<string, ImportedField>()
                {
                  { "xx", new ImportedField("stress_top_xx", typeof(float), ResultUnitType.Stress) },
                  { "yy", new ImportedField("stress_top_yy", typeof(float), ResultUnitType.Stress) },
                  { "zz", new ImportedField("stress_top_zz", typeof(float), ResultUnitType.Stress) },
                  { "xy", new ImportedField("stress_top_xy", typeof(float), ResultUnitType.Stress) },
                  { "yz", new ImportedField("stress_top_yz", typeof(float), ResultUnitType.Stress) },
                  { "zx", new ImportedField("stress_top_zx", typeof(float), ResultUnitType.Stress) },
                  { "position_r", new ImportedField("position_r", typeof(float), ResultUnitType.None) },
                  { "position_s", new ImportedField("position_s", typeof(float), ResultUnitType.None) }
                },
                null,
                new List<string>()  { "xx", "yy", "zz", "xy", "yz", "zx", "position_r", "position_s"})
            }
          }
        }
      },
      {
        ResultGroup.Assembly, new FileToResultTableSpec("id", "case_id")
        {
          ResultTypeCsvColumnMap = new Dictionary<string, ColMap>()
          {
            {
              //Note: the element ID and case ID columns are default columns (not need to be specified here) which will be automatically added to the output
              "Assembly Forces and Moments", new ColMap(
                new Dictionary<string, ImportedField>()
                {
                  { "fx", new ImportedField("force_x", typeof(float), ResultUnitType.Length ) },
                  { "fy", new ImportedField("force_y", typeof(float), ResultUnitType.Length) },
                  { "fz", new ImportedField("force_z", typeof(float), ResultUnitType.Length) },
                  { "mxx", new ImportedField("moment_x", typeof(float), ResultUnitType.Length) },
                  { "myy", new ImportedField("moment_y", typeof(float), ResultUnitType.Length) },
                  { "mzz", new ImportedField("moment_z", typeof(float), ResultUnitType.Length) }
                },
                new Dictionary<string, CalculatedField>()
                {
                  //Note: the calculated field indices are the zero-based column numbers based on the spec above, *before* the default columns are added
                  { "frc", new CalculatedField((v) => Magnitude(v), ResultUnitType.Length, 0, 1, 2) },
                  { "mom", new CalculatedField((v) => Magnitude(v), ResultUnitType.Length, 3, 4, 5) },
                },
                //The default columns will be added to the output too
                new List<string>() {  "fx", "fy", "fz", "frc", "mxx", "myy", "mzz", "mom" })
            }
          }
        }
      }
    };

    public static List<string> ResultTypes = resultTypeSpecs.SelectMany(rts => rts.Value.ResultTypeCsvColumnMap.Keys).ToList();
    public static List<string> ResultTypeFields(string rt)
    {
      foreach (var g in resultTypeSpecs.Keys)
      {
        foreach (var rtSpec in resultTypeSpecs[g].ResultTypeCsvColumnMap.Keys)
        {
          if (rtSpec.Equals(rt, StringComparison.InvariantCultureIgnoreCase))
          {
            return resultTypeSpecs[g].ResultTypeCsvColumnMap[rt].OrderedColumns;
          }
        }
      }
      return null;
    }
    */

    private static object Magnitude(params object[] dims)
    {
      if (!(dims.All(d => d is float)))
      {
        return null;
      }
      var vals = dims.Cast<float>().ToArray();
      return Math.Sqrt(vals.Select(d => Math.Pow((float)d, 2)).Sum());
    }

    private static object MomentResult(params object[] dims)
    {
      if (!(dims.All(d => d is float)))
      {
        return null;
      }
      var first = (float)dims.First();
      var last = (float)dims.Last();
      var magnitude = Math.Abs(first) + Math.Abs(last);
      return (first < 0) ? (-1) * magnitude : magnitude;
    }

    /*
    private static Dictionary<ResultCsvGroup, ColumnData> resultColData = new Dictionary<ResultCsvGroup, ColumnData>
    {
      {
        ResultCsvGroup.Node, new ColumnData()
        {
          CaseIdCol = "case_id", ElementIdCol = "id", ResultTypeCsvColumnMap = new Dictionary<string, Dictionary<string, string>>
          {
            { "Nodal Displacements", new Dictionary<string, string>()
              {
                { "disp_x", "ux" }, { "disp_y", "uy" } , { "disp_z", "uz" } , { "disp_xx", "rxx" } , { "disp_yy", "ryy" } , { "disp_zz", "rzz" }
              }
            },
            { "Nodal Velocity", new Dictionary<string, string>()
              {
                { "vel_x", "vx" }, { "vel_y", "vy" } , { "vel_z", "vz" } , { "vel_xx", "vxx" } , { "vel_yy", "vyy" } , { "vel_zz", "vzz" }
              }
            }
          }
        }
      },
      {
        ResultCsvGroup.Element1d, new ColumnData()
        {
          CaseIdCol = "case_id", ElementIdCol = "id", ResultTypeCsvColumnMap = new Dictionary<string, Dictionary<string, string>>
          {
            { "1D Element Displacement", new Dictionary<string, string>()
              {
                { "disp_x", "ux" }, { "disp_y", "uy" } , { "disp_z", "uz" }
              }
            },
            { "1D Element Force", new Dictionary<string, string>()
              {
                { "force_x", "fx" }, { "force_y", "fy" } , { "force_z", "fz" } , { "moment_x", "mxx" } , { "moment_y", "myy" } , { "moment_z", "mzz" }
              }
            }
          }
        }
      }
    };
    */

    private string SpeckleGsaVersion;
    private string units = "m";

    #region nodeAt_factors
    public static bool NodeAtCalibrated = false;
    //Set to defaults, which will be updated at calibration
    private static readonly Dictionary<string, float> UnitNodeAtFactors = new Dictionary<string, float>();

    public static void CalibrateNodeAt()
    {
      float coordValue = 1000;
      var unitCoincidentDict = new Dictionary<string, float>() { { "mm", 20 }, { "cm", 1 }, { "in", 1 }, { "m", 0.1f } };
      var units = new[] { "m", "cm", "mm", "in" };

      var proxy = new GSAProxy();
      proxy.NewFile(false);
      foreach (var u in units)
      {
        proxy.SetUnits(u);
        var nodeIndex = proxy.NodeAt(coordValue, coordValue, coordValue, unitCoincidentDict[u]);
        float factor = 1;
        var gwa = proxy.GetGwaForNode(nodeIndex);
        var pieces = gwa.Split(GSAProxy.GwaDelimiter);
        if (float.TryParse(pieces.Last(), out float z1))
        {
          if (z1 != coordValue)
          {
            var factorCandidate = coordValue / z1;

            nodeIndex = proxy.NodeAt(coordValue * factorCandidate, coordValue * factorCandidate, coordValue * factorCandidate, 1 * factorCandidate);

            gwa = proxy.GetGwaForNode(nodeIndex);
            pieces = gwa.Split(GSAProxy.GwaDelimiter);

            if (float.TryParse(pieces.Last(), out float z2) && z2 == 1000)
            {
              //it's confirmed
              factor = factorCandidate;
            }
          }
        }
        if (UnitNodeAtFactors.ContainsKey(u))
        {
          UnitNodeAtFactors[u] = factor;
        }
        else
        {
          UnitNodeAtFactors.Add(u, factor);
        }
      }

      proxy.Close();

      NodeAtCalibrated = true;
    }
    #endregion

    public void SetAppVersionForTelemetry(string speckleGsaAppVersion)
    {
      SpeckleGsaVersion = speckleGsaAppVersion;
    }

    #region telemetry
    public void SendTelemetry(params string[] messagePortions)
    {
      var finalMessagePortions = new List<string> { "SpeckleGSA", SpeckleGsaVersion, GSAObject.VersionString() };
      finalMessagePortions.AddRange(messagePortions);
      var message = string.Join("::", finalMessagePortions);
      GSAObject.LogFeatureUsage(message);
    }
    #endregion

    #region lock-related
    private readonly object syncLock = new object();
    protected T ExecuteWithLock<T>(Func<T> f)
    {
      lock (syncLock)
      {
        return f();
      }
    }

    protected void ExecuteWithLock(Action a)
    {
      lock (syncLock)
      {
        a();
      }
    }
    #endregion

    #region File Operations
    /// <summary>
    /// Creates a new GSA file. Email address and server address is needed for logging purposes.
    /// </summary>
    /// <param name="emailAddress">User email address</param>
    /// <param name="serverAddress">Speckle server address</param>
    public void NewFile(bool showWindow = true, object gsaInstance = null)
    {
      ExecuteWithLock(() =>
      {
        if (GSAObject != null)
        {
          try
          {
            GSAObject.Close();
          }
          catch { }
          GSAObject = null;
        }

        GSAObject = (IComAuto)gsaInstance ?? new ComAuto();

        GSAObject.LogFeatureUsage("api::specklegsa::" +
            FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location)
                .ProductVersion + "::GSA " + GSAObject.VersionString()
                .Split(new char[] { '\n' })[0]
                .Split(new char[] { GwaDelimiter }, StringSplitOptions.RemoveEmptyEntries)[1]);

        GSAObject.NewFile();
        GSAObject.SetLocale(Locale.LOC_EN_GB);
        if (showWindow)
        {
          GSAObject.DisplayGsaWindow(true);
        }
      });
    }

    /// <summary>
    /// Opens an existing GSA file. Email address and server address is needed for logging purposes.
    /// </summary>
    /// <param name="path">Absolute path to GSA file</param>
    /// <param name="emailAddress">User email address</param>
    /// <param name="serverAddress">Speckle server address</param>
    public bool OpenFile(string path, bool showWindow = true, object gsaInstance = null)
    {
      if (!File.Exists(path))
      {
        return false;
      }
      ExecuteWithLock(() =>
      {
        if (GSAObject != null)
        {
          try
          {
            GSAObject.Close();
          }
          catch { }
          GSAObject = null;
        }

        GSAObject = (IComAuto)gsaInstance ?? new ComAuto();

        GSAObject.LogFeatureUsage("api::specklegsa::" +
          FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location)
            .ProductVersion + "::GSA " + GSAObject.VersionString()
            .Split(new char[] { '\n' })[0]
            .Split(new char[] { GwaDelimiter }, StringSplitOptions.RemoveEmptyEntries)[1]);

        GSAObject.Open(path);
        FilePath = path;
        GSAObject.SetLocale(Locale.LOC_EN_GB);

        if (showWindow)
        {
          GSAObject.DisplayGsaWindow(true);
        }
      });
      return true;
    }

    public int SaveAs(string filePath) => ExecuteWithLock(() => GSAObject.SaveAs(filePath));

    /// <summary>
    /// Close GSA file.
    /// </summary>
    public void Close()
    {
      ExecuteWithLock(() =>
      {
        try
        {
          GSAObject.Close();
        }
        catch { }
      });
    }
    #endregion

    public string FormatApplicationIdSidTag(string value)
    {
      return (string.IsNullOrEmpty(value) ? "" : "{" + SID_APPID_TAG + ":" + value.Replace(" ","") + "}");
    }

    public string FormatStreamIdSidTag(string value)
    {
      return (string.IsNullOrEmpty(value) ? "" : "{" + SID_STRID_TAG + ":" + value.Replace(" ", "") + "}");
    }

    public string FormatSidTags(string streamId = "", string applicationId = "")
    {
      return FormatStreamIdSidTag(streamId) + FormatApplicationIdSidTag(applicationId);
    }

    public static void ParseGeneralGwa(string fullGwa, out string keyword, out int? index, out string streamId, out string applicationId, out string gwaWithoutSet, out GwaSetCommandType? gwaSetCommandType, bool includeKwVersion = false)
    {
      var pieces = fullGwa.ListSplit(GSAProxy.GwaDelimiter).ToList();
      keyword = "";
      streamId = "";
      applicationId = "";
      index = null;
      gwaWithoutSet = fullGwa;
      gwaSetCommandType = null;

      if (pieces.Count() < 2)
      {
        return;
      }

      //Remove the Set for the purpose of this method
      if (pieces[0].StartsWith("set", StringComparison.InvariantCultureIgnoreCase))
      {
        if (pieces[0].StartsWith("set_at", StringComparison.InvariantCultureIgnoreCase))
        {
          gwaSetCommandType = GwaSetCommandType.SetAt;

          if (int.TryParse(pieces[1], out int foundIndex))
          {
            index = foundIndex;
          }

          //For SET_ATs the format is SET_AT <index> <keyword> .., so remove the first two
          pieces.Remove(pieces[1]);
          pieces.Remove(pieces[0]);
        }
        else
        {
          gwaSetCommandType = GwaSetCommandType.Set;
          if (int.TryParse(pieces[2], out int foundIndex))
          {
            index = foundIndex;
          }

          pieces.Remove(pieces[0]);
        }
      }
      else
      {
        if (int.TryParse(pieces[1], out int foundIndex))
        {
          index = foundIndex;
        }
      }

      var delimIndex = pieces[0].IndexOf(':');
      if (delimIndex > 0)
      {
        //An SID has been found
        keyword = pieces[0].Substring(0, delimIndex);
        var sidTags = pieces[0].Substring(delimIndex);
        var match = Regex.Match(sidTags, "(?<={" + SID_STRID_TAG + ":).*?(?=})");
        streamId = (!string.IsNullOrEmpty(match.Value)) ? match.Value : "";
        match = Regex.Match(sidTags, "(?<={" + SID_APPID_TAG + ":).*?(?=})");
        applicationId = (!string.IsNullOrEmpty(match.Value)) ? match.Value : "";
      }
      else
      {
        keyword = pieces[0];
      }

      foreach (var groupKeyword in IrregularKeywordGroups.Keys)
      {
        if (IrregularKeywordGroups[groupKeyword].Contains(keyword))
        {
          keyword = groupKeyword;
          break;
        }
      }

      if (!includeKwVersion)
      {
        keyword = keyword.Split('.').First();
      }

      gwaWithoutSet = string.Join(GSAProxy.GwaDelimiter.ToString(), pieces);
      return;
    }

    //Tuple: keyword | index | Application ID | GWA command | Set or Set At
    public List<ProxyGwaLine> GetGwaData(IEnumerable<string> keywords, bool nodeApplicationIdFilter, IProgress<int> incrementProgress = null)
    {
      var dataLock = new object();
      var data = new List<ProxyGwaLine>();
      var setKeywords = new List<string>();
      var setAtKeywords = new List<string>();
      var tempKeywordIndexCache = new Dictionary<string, List<int>>();

      var versionRemovedKeywords = keywords.Select(kw => kw.Split('.').First()).Where(kw => !string.IsNullOrEmpty(kw)).ToList();

      foreach (var keyword in versionRemovedKeywords)
      {
        if (SetAtKeywords.Any(b => keyword.Equals(b, StringComparison.InvariantCultureIgnoreCase)))
        {
          setAtKeywords.Add(keyword);
        }
        else
        {
          setKeywords.Add(keyword);
        }
      }

      for (int i = 0; i < setKeywords.Count(); i++)
      {
        var newCommand = "GET_ALL" + GSAProxy.GwaDelimiter + setKeywords[i];
        var isNode = setKeywords[i].Contains("NODE");
        var isElement = setKeywords[i].StartsWith("EL");

        string[] gwaRecords;

        try
        {
          gwaRecords = ExecuteWithLock(() => ((string)GSAObject.GwaCommand(newCommand)).Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries));
        }
        catch
        {
          gwaRecords = new string[0];
        }

        if (setKeywords[i].Equals("UNIT_DATA", StringComparison.InvariantCultureIgnoreCase))
        {
          return gwaRecords.Select(r => new ProxyGwaLine() { GwaWithoutSet = r, Keyword = "UNIT_DATA" }).ToList();
        }

        Parallel.ForEach(gwaRecords, gwa =>
        {
          ParseGeneralGwa(gwa, out string keywordWithVersion, out int? foundIndex, out string foundStreamId, out string foundApplicationId, out string gwaWithoutSet, out GwaSetCommandType? gwaSetCommandType, true);
          var index = foundIndex ?? 0;
          var originalSid = "";
          var keyword = keywordWithVersion.Split('.').First();

          //For some GET_ALL calls, records with other keywords are returned, too.  Example: GET_ALL TASK returns TASK, TASK_TAG and ANAL records
          if (keyword.Equals(setKeywords[i], StringComparison.InvariantCultureIgnoreCase))
          {
            if (string.IsNullOrEmpty(foundStreamId))
            {
              //Slight hardcoding for optimisation here: the biggest source of GetSidTagValue calls would be from nodes, and knowing
              //(at least in GSA v10 build 63) that GET_ALL NODE does return SID tags, the call is avoided for NODE keyword
              if (!isNode && !isElement)
              {
                try
                {
                  foundStreamId = ExecuteWithLock(() => GSAObject.GetSidTagValue(keyword, index, SID_STRID_TAG));
                }
                catch { }
              }
            }
            else
            {
              originalSid += FormatStreamIdSidTag(foundStreamId);
            }

            if (string.IsNullOrEmpty(foundApplicationId))
            {
              //Again, the same optimisation as explained above
              if (!isNode && !isElement)
              {
                try
                {
                  foundApplicationId = ExecuteWithLock(() => GSAObject.GetSidTagValue(keyword, index, SID_APPID_TAG));
                }
                catch { }
              }
            }
            else
            {
              originalSid += FormatStreamIdSidTag(foundApplicationId);
            }

            var newSid = FormatStreamIdSidTag(foundStreamId) + FormatApplicationIdSidTag(foundApplicationId);
            if (!string.IsNullOrEmpty(newSid))
            {
              if (string.IsNullOrEmpty(originalSid))
              {
                gwaWithoutSet = gwaWithoutSet.Replace(keywordWithVersion, keywordWithVersion + ":" + newSid);
              }
              else
              {
                gwaWithoutSet = gwaWithoutSet.Replace(originalSid, newSid);
              }
            }

            if (!(nodeApplicationIdFilter == true && isNode && string.IsNullOrEmpty(foundApplicationId)))
            {
              var line = new ProxyGwaLine()
              {
                Keyword = keyword,
                Index = index,
                StreamId = foundStreamId,
                ApplicationId = foundApplicationId,
                GwaWithoutSet = gwaWithoutSet,
                GwaSetType = GwaSetCommandType.Set
              };

              lock (dataLock)
              {
                if (!tempKeywordIndexCache.ContainsKey(keyword))
                {
                  tempKeywordIndexCache.Add(keyword, new List<int>());
                }
                if (!tempKeywordIndexCache[keyword].Contains(index))
                {
                  data.Add(line);
                  tempKeywordIndexCache[keyword].Add(index);
                }
              }
            }
          }
        });

        if (incrementProgress != null)
        {
          incrementProgress.Report(1);
        }
      }

      for (int i = 0; i < setAtKeywords.Count(); i++)
      {
        var highestIndex = ExecuteWithLock(() => GSAObject.GwaCommand("HIGHEST" + GSAProxy.GwaDelimiter + setAtKeywords[i]));

        for (int j = 1; j <= highestIndex; j++)
        {
          var newCommand = string.Join(GwaDelimiter.ToString(), new[] { "GET", setAtKeywords[i], j.ToString() });

          var gwaRecord = "";
          try
          {
            gwaRecord = (string)ExecuteWithLock(() => GSAObject.GwaCommand(newCommand));
          }
          catch { }

          if (gwaRecord != "")
          {
            ParseGeneralGwa(gwaRecord, out string keyword, out int? foundIndex, out string foundStreamId, out string foundApplicationId, out string gwaWithoutSet, out GwaSetCommandType? gwaSetCommandType);

            if (keyword.ContainsCaseInsensitive(setAtKeywords[i]))
            {
              var originalSid = "";
              if (string.IsNullOrEmpty(foundStreamId))
              {
                try
                {
                  foundStreamId = ExecuteWithLock(() => GSAObject.GetSidTagValue(keyword, j, SID_STRID_TAG));
                }
                catch { }
              }
              else
              {
                originalSid += FormatStreamIdSidTag(foundStreamId);
              }
              if (string.IsNullOrEmpty(foundApplicationId))
              {
                foundApplicationId = ExecuteWithLock(() => GSAObject.GetSidTagValue(keyword, j, SID_APPID_TAG));
              }
              else
              {
                originalSid += FormatStreamIdSidTag(foundApplicationId);
              }

              var newSid = FormatStreamIdSidTag(foundStreamId) + FormatApplicationIdSidTag(foundApplicationId);
              if (!string.IsNullOrEmpty(originalSid) && !string.IsNullOrEmpty(newSid))
              {
                gwaWithoutSet.Replace(originalSid, newSid);
              }

              var line = new ProxyGwaLine()
              {
                Keyword = setAtKeywords[i],
                Index = j,
                StreamId = foundStreamId,
                ApplicationId = foundApplicationId,
                GwaWithoutSet = gwaWithoutSet,
                GwaSetType = GwaSetCommandType.SetAt
              };

              lock (dataLock)
              {
                if (!tempKeywordIndexCache.ContainsKey(setAtKeywords[i]))
                {
                  tempKeywordIndexCache.Add(setAtKeywords[i], new List<int>());
                }
                if (!tempKeywordIndexCache[setAtKeywords[i]].Contains(j))
                {
                  data.Add(line);
                  tempKeywordIndexCache[setAtKeywords[i]].Add(j);
                }
              }
            }
          }
        }
        if (incrementProgress != null)
        {
          incrementProgress.Report(1);
        }
      }

      return data;
    }

    private string FormatApplicationId(string keyword, int index, string applicationId)
    {
      //It has been observed that sometimes GET commands don't include the SID despite there being one.  For some (but not all)
      //of these instances, the SID is available through an explicit call for the SID, so try that next
      return (string.IsNullOrEmpty(applicationId)) ? ExecuteWithLock(() => GSAObject.GetSidTagValue(keyword, index, SID_APPID_TAG)) : applicationId;
    }

    private int ExtractGwaIndex(string gwaRecord)
    {
      var pieces = gwaRecord.Split(GwaDelimiter);
      return (int.TryParse(pieces[1], out int index)) ? index : 0;
    }


    //Assumed to be the full SET or SET_AT command
    public void SetGwa(string gwaCommand) => ExecuteWithLock(() => batchSetGwa.Add(gwaCommand));

    public void Sync()
    {
      if (batchBlankGwa.Count() > 0)
      {
        var batchBlankCommand = ExecuteWithLock(() => string.Join("\r\n", batchBlankGwa));
        var blankCommandResult = ExecuteWithLock(() => GSAObject.GwaCommand(batchBlankCommand));
        ExecuteWithLock(() => batchBlankGwa.Clear());
      }

      if (batchSetGwa.Count() > 0)
      {
        var batchSetCommand = ExecuteWithLock(() => string.Join("\r\n", batchSetGwa));
        var setCommandResult = ExecuteWithLock(() => GSAObject.GwaCommand(batchSetCommand));
        ExecuteWithLock(() => batchSetGwa.Clear());
      }
    }

    /*
    public void GetGSATotal2DElementOffset(int index, double insertionPointOffset, out double offset, out string offsetRec)
    {
      double materialInsertionPointOffset = 0;
      double zMaterialOffset = 0;

      object result = ExecuteWithLock(() => GSAObject.GwaCommand(string.Join(GSAProxy.GwaDelimiter.ToString(), new[] { "GET", "PROP_2D", index.ToString() })));
      string[] newPieces = ((string)result).Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries).Select((s, idx) => idx.ToString() + ":" + s).ToArray();

      string res = newPieces.FirstOrDefault();

      if (res == null || res == "")
      {
        offset = insertionPointOffset;
        offsetRec = res;
        return;
      }

      string[] pieces = res.ListSplit(GSAProxy.GwaDelimiter);

      offsetRec = res;

      if (pieces.Length >= 13)
      {
        zMaterialOffset = -Convert.ToDouble(pieces[12]);
        offset = insertionPointOffset + zMaterialOffset + materialInsertionPointOffset;
      }
      else
      {
        offset = 0;
      }
      return;
    }
    */

    public int NodeAt(double x, double y, double z, double coincidenceTol)
    {
      float factor = (UnitNodeAtFactors != null && UnitNodeAtFactors.ContainsKey(units)) ? UnitNodeAtFactors[units] : 1;
      //Note: the outcome of this might need to be added to the caches!
      var index = ExecuteWithLock(() => GSAObject.Gen_NodeAt(x * factor, y * factor, z * factor, coincidenceTol * factor));
      return index;
    }

    public string GetGwaForNode(int index)
    {
      var gwaCommand = string.Join(GwaDelimiter.ToString(), new[] { "GET", "NODE.3", index.ToString() });
      return (string)ExecuteWithLock(() => GSAObject.GwaCommand(gwaCommand));
    }

    public string SetSid(string gwa, string streamId, string applicationId)
    {
      ParseGeneralGwa(gwa, out string keyword, out int? foundIndex, out string foundStreamId, out string foundApplicationId, out string gwaWithoutSet, out GwaSetCommandType? gwaSetCommandType, true);

      var streamIdToWrite = (string.IsNullOrEmpty(streamId) ? foundStreamId : streamId);
      var applicationIdToWrite = (string.IsNullOrEmpty(applicationId) ? foundApplicationId : applicationId);

      if (!string.IsNullOrEmpty(streamIdToWrite))
      {
        ExecuteWithLock(() => GSAObject.WriteSidTagValue(keyword, foundIndex.Value, SID_STRID_TAG, streamIdToWrite));
      }
      if (!string.IsNullOrEmpty(applicationIdToWrite))
      {
        ExecuteWithLock(() => GSAObject.WriteSidTagValue(keyword, foundIndex.Value, SID_APPID_TAG, applicationIdToWrite));
      }

      var newSid = FormatStreamIdSidTag(streamIdToWrite) + FormatApplicationIdSidTag(applicationIdToWrite);
      if (!string.IsNullOrEmpty(foundStreamId) || !string.IsNullOrEmpty(foundApplicationId))
      {
        var originalSid = FormatStreamIdSidTag(foundStreamId) + FormatApplicationIdSidTag(foundApplicationId);
        gwa = gwa.Replace(originalSid, newSid);
      }
      else
      {
        gwa = gwa.Replace(keyword, keyword + ":" + newSid);
      }
      return gwa;
    }

    public int[] ConvertGSAList(string list, GSAEntity type)
    {
      if (list == null) return new int[0];

      string[] pieces = list.ListSplit(" ");
      pieces = pieces.Where(s => !string.IsNullOrEmpty(s)).ToArray();

      List<int> items = new List<int>();
      for (int i = 0; i < pieces.Length; i++)
      {
        if (pieces[i].IsDigits())
        {
          items.Add(Convert.ToInt32(pieces[i]));
        }
        else if (pieces[i].Contains('"'))
        {
          items.AddRange(ConvertNamedGSAList(pieces[i], type));
        }
        else if (pieces[i] == "to")
        {
          int lowerRange = Convert.ToInt32(pieces[i - 1]);
          int upperRange = Convert.ToInt32(pieces[i + 1]);

          for (int j = lowerRange + 1; j <= upperRange; j++)
            items.Add(j);

          i++;
        }
        else
        {
          try
          {
            var item = ExecuteWithLock(() =>
            {
              GSAObject.EntitiesInList(pieces[i], (GsaEntity)type, out int[] itemTemp);

              if (itemTemp == null)
              {
                GSAObject.EntitiesInList("\"" + list + "\"", (GsaEntity)type, out itemTemp);
              }
              return itemTemp;
            });

            if (item != null)
            {
              items.AddRange((int[])item);
            }
          }
          catch
          { }
        }
      }

      return items.ToArray();
    }

    /*
    public Dictionary<string, object> GetGSAResult(int id, int resHeader, int flags, List<string> keys, string loadCase, string axis = "local", int num1DPoints = 2)
    {
      var ret = new Dictionary<string, object>();
      GsaResults[] res = null;
      bool exists = false;

      int returnCode = -1;

      try
      {
        return ExecuteWithLock(() =>
        {
          int num;

          // The 2nd condition here is a special case for assemblies
          if (Enum.IsDefined(typeof(ResHeader), resHeader) || resHeader == 18002000)
          {
            returnCode = GSAObject.Output_Init_Arr(flags, axis, loadCase, (ResHeader)resHeader, num1DPoints);

            try
            {
              var existsResult = GSAObject.Output_DataExist(id);
              exists = (existsResult == 1);
            }
            catch (Exception e)
            {
              return null;
            }

            if (exists)
            {
              var extracted = false;
              try
              {
                returnCode = GSAObject.Output_Extract_Arr(id, out var outputExtractResults, out num);
                res = (GsaResults[])outputExtractResults;
                extracted = true;
              }
              catch { }

              if (!extracted)
              {
                // Try individual extract
                for (var i = 1; i <= keys.Count; i++)
                {
                  var indivResHeader = resHeader + i;

                  try
                  {
                    GSAObject.Output_Init(flags, axis, loadCase, indivResHeader, num1DPoints);
                  }
                  catch (Exception e)
                  {
                    return null;
                  }

                  var numPos = 1;

                  try
                  {
                    numPos = GSAObject.Output_NumElemPos(id);
                  }
                  catch { }

                  if (i == 1)
                  {
                    res = new GsaResults[numPos];
                    for (var j = 0; j < res.Length; j++)
                    {
                      res[j] = new GsaResults() { dynaResults = new double[keys.Count] };
                    }
                  }

                  for (var j = 0; j < numPos; j++)
                  {
                    res[j].dynaResults[i - 1] = (double)GSAObject.Output_Extract(id, j);
                  }
                }
              }

            }
            else
            {
              return null;
            }
          }
          else
          {
            returnCode = GSAObject.Output_Init(flags, axis, loadCase, resHeader, num1DPoints);

            try
            {
              var existsResult = GSAObject.Output_DataExist(id);
              exists = (existsResult == 1);
            }
            catch
            {
              return null;
            }

            if (exists)
            {
              var numPos = GSAObject.Output_NumElemPos(id);
              res = new GsaResults[numPos];

              try
              {
                for (var i = 0; i < numPos; i++)
                {
                  res[i] = new GsaResults() { dynaResults = new double[] { (double)GSAObject.Output_Extract(id, i) } };
                }
              }
              catch
              {
                return null;
              }
            }
            else
            {
              return null;
            }
          }

          var numColumns = res[0].dynaResults.Count();

          for (var i = 0; i < numColumns; i++)
          {
            ret[keys[i]] = res.Select(x => (double)x.dynaResults.GetValue(i)).ToList();
          }

          return ret;
        });
      }
      catch
      {
        return null;
      }
    }

    public bool CaseExist(string loadCase)
    {
      try
      {
        string[] pieces = loadCase.Split(new char[] { 'p' }, StringSplitOptions.RemoveEmptyEntries);

        if (pieces.Length == 1)
        {
          return ExecuteWithLock(() => GSAObject.CaseExist(loadCase[0].ToString(), Convert.ToInt32(loadCase.Substring(1))) == 1);
        }
        else if (pieces.Length == 2)
        {
          return ExecuteWithLock(() => GSAObject.CaseExist(loadCase[0].ToString(), Convert.ToInt32(pieces[0].Substring(1))) == 1);
        }
        else
        {
          return false;
        }
      }
      catch { return false; }
    }
    */

    #region private_methods
    private int[] ConvertNamedGSAList(string list, GSAEntity type)
    {
      list = list.Trim(new char[] { '"', ' ' });

      try
      {
        object result = ExecuteWithLock(() => GSAObject.GwaCommand(string.Join(GwaDelimiter.ToString(), new[] { "GET", "LIST", list })));
        string[] newPieces = ((string)result).Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries).Select((s, idx) => idx.ToString() + ":" + s).ToArray();

        string res = newPieces.FirstOrDefault();

        string[] pieces = res.Split(GSAProxy.GwaDelimiter);

        return ConvertGSAList(pieces[pieces.Length - 1], type);
      }
      catch
      {
        try
        {
          return ExecuteWithLock(() =>
          {
            GSAObject.EntitiesInList("\"" + list + "\"", (GsaEntity)type, out int[] itemTemp);
            return (itemTemp == null) ? new int[0] : (int[])itemTemp;
          });
        }
        catch { return new int[0]; }
      }
    }
    #endregion

    //
    public void DeleteGWA(string keyword, int index, GwaSetCommandType gwaSetCommandType)
    {
      var command = string.Join(GwaDelimiter.ToString(), new[] { (gwaSetCommandType == GwaSetCommandType.Set) ? "BLANK" : "DELETE", keyword, index.ToString() });
      ExecuteWithLock(() =>
      { 
        if (gwaSetCommandType == GwaSetCommandType.Set)
        {
          //For synchronising later
          batchBlankGwa.Add(command);
        }
        else
        {
          GSAObject.GwaCommand(command);
        }
      });
    }

    //----
    #region Speckle Client
    /// <summary>
    /// Writes sender and receiver streams associated with the account.
    /// </summary>
    /// <param name="emailAddress">User email address</param>
    /// <param name="serverAddress">Speckle server address</param>
    public bool SetTopLevelSid(string sidRecord)
    {
      try
      {
        ExecuteWithLock(() => GSAObject.GwaCommand(string.Join(GwaDelimiter.ToString(), new[] { "SET", "SID", sidRecord })));
        return true;
      }
      catch
      {
        return false;
      }
    }
    #endregion

    #region Document Properties
    /// <summary>
    /// Extract the title of the GSA model.
    /// </summary>
    /// <returns>GSA model title</returns>
    public string GetTitle()
    {
      string res = (string)ExecuteWithLock(() => GSAObject.GwaCommand("GET" + GSAProxy.GwaDelimiter + "TITLE"));

      string[] pieces = res.ListSplit(GSAProxy.GwaDelimiter);

      return pieces.Length > 1 ? pieces[1] : "My GSA Model";
    }

    public string[] GetTolerances()
    {
      return ((string)ExecuteWithLock(() => GSAObject.GwaCommand("GET" + GSAProxy.GwaDelimiter + "TOL"))).ListSplit(GSAProxy.GwaDelimiter);
    }

    /// <summary>
    /// Updates the GSA unit stored in SpeckleGSA.
    /// </summary>
    public string GetUnits()
    {
      var retrievedUnits = ((string)ExecuteWithLock(() => GSAObject.GwaCommand(string.Join(GwaDelimiter.ToString(), new[] { "GET", "UNIT_DATA.1", "LENGTH" })))).ListSplit(GwaDelimiter)[2];
      this.units = retrievedUnits;
      return retrievedUnits;
    }

    public bool SetUnits(string units)
    {
      this.units = units;
      var retCode = ExecuteWithLock(() => GSAObject.GwaCommand(string.Join(GwaDelimiter.ToString(), new[] { "SET", "UNIT_DATA", "LENGTH", units })));
      retCode = ExecuteWithLock(() => GSAObject.GwaCommand(string.Join(GwaDelimiter.ToString(), new[] { "SET", "UNIT_DATA", "DISP", units })));
      retCode = ExecuteWithLock(() => GSAObject.GwaCommand(string.Join(GwaDelimiter.ToString(), new[] { "SET", "UNIT_DATA", "SECTION", units })));
      //Apparently 1 seems to be the code for success, from observation
      return (retCode == 1);
    }
    #endregion

    #region Views
    /// <summary>
    /// Update GSA viewer. This should be called at the end of changes.
    /// </summary>
    public bool UpdateViews()
    {
      try
      {
        ExecuteWithLock(() => GSAObject.UpdateViews());
        return true;
      }
      catch
      {
        return false;
      }
    }

    /// <summary>
    /// Update GSA case and task links. This should be called at the end of changes.
    /// </summary>
    public bool UpdateCasesAndTasks()
    {
      try
      {
        ExecuteWithLock(() => GSAObject.ReindexCasesAndTasks());
        return true;
      }
      catch
      {
        return false;
      }
    }

    public string GetTopLevelSid()
    {
      string sid = "";
      try
      {
        sid = (string)ExecuteWithLock(() => GSAObject.GwaCommand("GET" + GwaDelimiter + "SID"));
      }
      catch
      {
        //File doesn't have SID
      }
      return sid;
    }

    //Created as part of functionality needed to convert a load case specification in the UI into an itemised list of load cases 
    //(including combinations)
    //Since EntitiesInList doesn't offer load cases/combinations as a GsaEntity type, a dummy GSA proxy (and therefore GSA instance) 
    //is created by the GSA cache and that calls the method below - even though it deals with nodes - as a roundabout way of
    //converting a list specification into valid load cases or combinations.   This method is called separately for load cases and combinations. 
    public List<int> GetNodeEntitiesInList(string spec)
    {
      var listType = GsaEntity.NODE;

      //Check that this indeed a list - the EntitiesInList call will function differently if given a single item
      var pieces = spec.Trim().Split(new[] { ' ' });
      if (pieces.Count() == 1)
      {
        spec = pieces[0] + " " + pieces[0];
      }

      var result = GSAObject.EntitiesInList(spec, ref listType, out int[] entities);
      return (entities != null && entities.Count() > 0)
        ? entities.ToList()
        : new List<int>();
    }

    private bool ClearResultsDirectory()
    {
      var di = new DirectoryInfo(resultDir);
      if (!di.Exists)
      {
        return true;
      }

      foreach (DirectoryInfo dir in di.GetDirectories())
      {
        foreach (FileInfo file in dir.GetFiles())
        {
          if (!file.Extension.Equals(".csv", StringComparison.InvariantCultureIgnoreCase))
          {
            return false;
          }
        }
      }

      foreach (FileInfo file in di.GetFiles())
      {
        file.Delete();
      }
      foreach (DirectoryInfo dir in di.GetDirectories())
      {
        dir.Delete(true);
      }
      return true;
    }

    private bool ProcessUnitGwaData()
    {
      var unitGwaLines = GetGwaData(new[] { "UNIT_DATA" }, false);
      if (unitGwaLines == null || unitGwaLines.Count() == 0)
      {
        return false;
      }
      unitData.Clear();

      foreach (var gwa in unitGwaLines.Select(l => l.GwaWithoutSet).ToList())
      {
        var pieces = gwa.Split(GwaDelimiter);

        if (Enum.TryParse(pieces[1], true, out ResultUnitType rut) && float.TryParse(pieces.Last(), out float factor))
        {
          unitData.Add(rut, factor);
        }
      }
      return true;
    }

    public bool PrepareResults(List<ResultType> resultTypes, int numBeamPoints = 3)
    {
      this.resultDir = Path.Combine(Environment.CurrentDirectory, "GSAExport");
      this.allResultTypes = resultTypes;

      ProcessUnitGwaData();

      //First delete all existing csv files in the results directory to avoid confusion
      if (!ClearResultsDirectory())
      {
        return false;
      }
      var retCode = GSAObject.ExportToCsv(resultDir, numBeamPoints, true, true, ",");
      if (retCode == 0)
      {
        //Assume that
        return true;
      }
      return false;
    }

    /*
    public bool LoadResults(List<string> resultTypes, List<string> cases = null, List<int> elemIds = null)
    {
      var fieldsPerGroup = new Dictionary<ResultGroup, List<string>>();

      if (this.resultTypes == null)
      {
        this.resultTypes = new List<string>();
      }
      foreach (var rt in resultTypes)
      {
        if (!this.resultTypes.Contains(rt))
        {
          this.resultTypes.Add(rt);
        }
      }
      this.cases = cases;

      foreach (var rt in resultTypes)
      {
        var groups = resultTypeSpecs.Keys.Where(k => resultTypeSpecs[k].ResultTypeCsvColumnMap.Keys.Any(r => r.Equals(rt, StringComparison.InvariantCultureIgnoreCase)));
        if (groups != null && groups.Count() > 0)
        {
          var g = groups.First();

          foreach (var f in resultTypeSpecs[g].ResultTypeCsvColumnMap[rt].FileCols.Values.Select(cm => cm.FileCol))
          {
            if (!fieldsPerGroup.ContainsKey(g))
            {
              fieldsPerGroup.Add(g, new List<string>());
            }
            if (!fieldsPerGroup[g].Contains(f))
            {
              fieldsPerGroup[g].Add(f);
            }
          }
        }
      }

      try
      {
        Parallel.ForEach(fieldsPerGroup.Keys, 
          g => resultsContext.ImportResultsFromFile(relativePathsToLoad[g], g, "case_id", "id", fieldsPerGroup[g], cases, elemIds));
      }
      catch (Exception ex)
      {
        return false;
      }

      return true;
    }

    public bool ClearResults(List<string> resultTypes)
    {
      foreach (var rt in resultTypes)
      {
        var groups = resultTypeSpecs.Keys.Where(k => resultTypeSpecs[k].ResultTypeCsvColumnMap.Keys.Any(r => r.Equals(rt, StringComparison.InvariantCultureIgnoreCase)));
        if (groups != null && groups.Count() > 0)
        {
          resultsContext.Clear(groups.First());
        }
      }

      GC.Collect();

      return true;
    }
    */

    /*
    public bool PrepareResults(int numBeamPoints, List<string> resultTypes, List<string> cases)
    {
      if (resultTypes == null || resultTypes.Count() == 0 || cases == null || cases.Count() == 0)
      {
        return false;
      }
      var allResultTypes = resultTypeSpecs.Keys.SelectMany(g => resultTypeSpecs[g].ResultTypeCsvColumnMap.Select(m => m.Key)).ToList();
      this.resultTypes = resultTypes.Where(rt => allResultTypes.Any(art => art.Equals(rt, StringComparison.InvariantCultureIgnoreCase))).ToList();
      if (this.resultTypes.Count() == 0)
      {
        return false;
      }
      this.resultDir = Path.Combine(Environment.CurrentDirectory, "GSAExport");
      this.cases = cases;

      ProcessUnitGwaData();

      //First delete all existing csv files in the results directory to avoid confusion
      if (!ClearResultsDirectory())
      {
        return false;
      }

      var fieldsPerGroup = new Dictionary<ResultCsvGroup, List<string>>();

      foreach (var rt in resultTypes)
      {
        var groups = resultTypeSpecs.Keys.Where(k => resultTypeSpecs[k].ResultTypeCsvColumnMap.Keys.Any(r => r.Equals(rt, StringComparison.InvariantCultureIgnoreCase)));
        if (groups != null && groups.Count() > 0)
        {
          var g = groups.First();

          foreach (var f in resultTypeSpecs[g].ResultTypeCsvColumnMap[rt].FileCols.Values.Select(cm => cm.FileCol))
          {
            if (!fieldsPerGroup.ContainsKey(g))
            {
              fieldsPerGroup.Add(g, new List<string>());
            }
            if (!fieldsPerGroup[g].Contains(f))
            {
              fieldsPerGroup[g].Add(f);
            }
          }
        }
      }

      //var progressPercentage = 5;  //TO DO: remove this hardcoded magic number - it's currently the assumed starting point after resolving the load cases
      var retCode = GSAObject.ExportToCsv(resultDir, numBeamPoints, true, true, ",");
      //progressPercentage += 10;
      //var progressPercentageLock = new object();

      if (retCode == 0)
      {
        //Assume that
        resultsContext = new SpeckleGSAResultsContext(resultDir);

        Parallel.ForEach(fieldsPerGroup.Keys, g =>
        //foreach (var g in relativePathsToLoad.Keys)
        {
          resultsContext.ImportResultsFromFile(relativePathsToLoad[g], g, "case_id", "id", fieldsPerGroup[g]);
          //lock (progressPercentageLock)
          //{
          //  progressPercentage += (40 / relativePathsToLoad.Count());
          //}
        }
        );
        return true;
      }
      return false;
    }

    // format for data is [ result_type, [ [ headers ], [ row, column ] ] ]
    public bool GetResults(string keyword, int index, out Dictionary<string, Tuple<List<string>, object[,]>> allData, int dimension = 1)
    {
      allData = new Dictionary<string, Tuple<List<string>, object[,]>>();

      var kw = keyword.Split('.').First();
      ResultGroup g = ResultGroup.Unknown;
      if (kw.Equals("NODE", StringComparison.InvariantCultureIgnoreCase))
      {
        g = ResultGroup.Node;
      }
      else if (kw.Equals("EL", StringComparison.InvariantCultureIgnoreCase))
      {
        g = dimension == 2 ? ResultGroup.Element2d : ResultGroup.Element1d;
      }
      else if (kw.Equals("ASSEMBLY", StringComparison.InvariantCultureIgnoreCase))
      {
        g = ResultGroup.Assembly;
      }

      if (g == ResultGroup.Unknown)
      {
        return false;
      }

      bool found = false;
      if (GetResults(g, index, out Dictionary<string, Tuple<List<string>, object[,]>> data) && data != null)
      {
        foreach (var k in data.Keys)
        {
          if (!allData.ContainsKey(k))
          {
            allData.Add(k, data[k]);
            found = true;
          }
        }
      }
      return found;
    }

    private bool GetResults(string tableName, ResultCsvGroup group, int elemId, out Dictionary<string, Tuple<List<string>, object[,]>> data)
    {
      data = new Dictionary<string, Tuple<List<string>, object[,]>>();
      if (ImportResultsFileIfNecessary(tableName) && resultColData.ContainsKey(group))
      {
        //For each result type applicable to the data offered in this table (read from file)
        foreach (var grt in resultColData[group].ResultTypeCsvColumnMap.Keys)
        {
          var cols = (new[] { resultColData[group].ElementIdCol, resultColData[group].CaseIdCol }).Concat(resultColData[group].ResultTypeCsvColumnMap[grt].Keys);
          if (resultsContext.Query(tableName, cols, cases, out var results, new int[] { elemId }))
          {
            data.Add(grt, new Tuple<List<string>, object[,]>(cols.ToList(), results));
          }
        }
      }
      return (data.Keys.Count() > 0);
    }

    private bool GetResults(ResultGroup group, int elemId, out Dictionary<string, Tuple<List<string>, object[,]>> data)
    {
      data = new Dictionary<string, Tuple<List<string>, object[,]>>();
      if (!resultTypeSpecs.ContainsKey(group) || !resultsContext.ResultTableGroups.Contains(group))
      {
        return false;
      }

      var spec = resultTypeSpecs[group];

      var defaultFileCols = new[] { spec.ElementIdCol, spec.CaseIdCol };
      var defaultFileColTypes = new[] { typeof(int), typeof(string) };
      var defaultColFactors = new List<double>[] { null, null };

      var indexOffsetForCalcs = defaultFileCols.Count();

      var relevantResultTypes = spec.ResultTypeCsvColumnMap.Keys.Intersect(this.resultTypes);
      if (relevantResultTypes == null || relevantResultTypes.Count() == 0)
      {
        //No results but successful
        return true;
      }

      foreach (var rt in relevantResultTypes)
      {
        var rtMap = spec.ResultTypeCsvColumnMap[rt];
        var specFileColFinalNames = rtMap.FileCols.Keys.ToList();
        var specFileColOriginalNames = specFileColFinalNames.Select(fn => rtMap.FileCols[fn].FileCol).ToList();
        var allFileCols = specFileColOriginalNames.Concat(defaultFileCols).ToList();
        var allFileIndices = allFileCols.Select((fc, i) => new { fc, i }).ToDictionary(x => x.fc, x => x.i);

        //The default columns are always tacked onto the end so there is no issue with the indices used in the calculated fields
        if (!resultsContext.Query(group, allFileCols, cases, out var rtResults, new int[] { elemId })
          || rtResults == null || rtResults.GetLength(0) == 0)
        {
          continue;
        }

        var numRows = rtResults.GetLength(0);
        var numSpecFileCols = specFileColFinalNames.Count();
        //Convert the newly-retrieved values from the CSV files into their correct destination type
        for (int r = 0; r < numRows; r++)
        {
          for (int c = 0; c < numSpecFileCols; c++)
          {
            var colName = specFileColFinalNames[c];
            if (rtMap.FileCols[specFileColFinalNames[c]] != null && rtResults[r, c] != null)
            {
              if (rtResults[r, c] is string && string.IsNullOrEmpty((string)rtResults[r, c]))
              {
                rtResults[r, c] = null;
              }
              else
              {
                rtResults[r, c] = Convert.ChangeType(rtResults[r, c], rtMap.FileCols[specFileColFinalNames[c]].DestType);
              }
            }
          }
          for (int cd = 0; cd < defaultFileCols.Count(); cd++)
          {
            rtResults[r, numSpecFileCols + cd] = Convert.ChangeType(rtResults[r, numSpecFileCols + cd], defaultFileColTypes[cd]);
          }
        }

        var numCols = rtMap.OrderedColumns.Count() + defaultFileCols.Count();
        var rtData = new object[numRows, numCols];

        if (rtMap.CalcFields != null && rtMap.CalcFields.Keys.Count() > 0 && rtMap.CalcFields.First().Value != null)
        {
          //Add in calculated fields based on the table returned from the query
          for (int r = 0; r < numRows; r++)
          {
            for (int c = 0; c < rtMap.OrderedColumns.Count(); c++)
            {
              var colFinalName = rtMap.OrderedColumns[c];
              if (rtMap.CalcFields.ContainsKey(colFinalName))
              {
                var indices = rtMap.CalcFields[colFinalName].FileColIndices;
                var values = indices.Select(i => rtResults[r, i]).ToArray();
                rtData[r, c] = rtMap.CalcFields[colFinalName].CalcFn(values);
              }
            }
          }
        }

        var orderedFieldSpecs = rtMap.OrderedFieldSpecs.Keys.ToList();
        var numOrderedCols = rtMap.OrderedColumns.Count();

        //Now fill in the rest and apply factors
        for (int r = 0; r < numRows; r++)
        {
          for (int c = 0; c < numOrderedCols; c++)
          {
            if (orderedFieldSpecs[c] is ImportedField)
            {
              var rtResultColIndex = rtMap.OrderedFieldSpecs[orderedFieldSpecs[c]];
              rtData[r, c] = rtResults[r, rtResultColIndex];
            }
            rtData[r, c] = ApplyFactors(rtData[r, c], GetFactors(orderedFieldSpecs[c].UnitTypes));
          }
          for (int cd = 0; cd < defaultFileCols.Count(); cd++)
          {
            var rtResultColIndex = allFileIndices[defaultFileCols[cd]];
            rtData[r, numOrderedCols + cd] = rtResults[r, rtResultColIndex];
          }
        }

        if (!data.ContainsKey(rt))
        {
          data.Add(rt, new Tuple<List<string>, object[,]>(rtMap.OrderedColumns.Concat(defaultFileCols).ToList(), rtData));
        }
      }
      return (data.Keys.Count > 0);
    }

    private object ApplyFactors(object val, List<double> factors)
    {
      if (factors == null || factors.Count() == 0 || !(val is float))
      {
        return val;
      }
      if ((float)val == 0)
      {
        return val;
      }
      foreach (var f in factors)
      {
        val = ((float)val * f);
      }
      return val;
    }

    private List<double> GetFactors(IEnumerable<ResultUnitType> ruts)
    {
      return ruts.Where(r => unitData.ContainsKey(r)).Select(r => unitData[r]).ToList();
    }

    private string GetTableName(ResultGroup csvGroup)
    {
      switch (csvGroup)
      {
        case ResultGroup.Node: return "result_node";
        case ResultGroup.Element1d: return "result_elem_1d";
        case ResultGroup.Element2d: return "result_elem_2d";
        case ResultGroup.Assembly: return "result_assembly";
        default: return null;
      }
    }
    */

    public bool LoadResults(ResultGroup group, List<string> cases = null, List<int> elemIds = null)
    {
      if (group == ResultGroup.Assembly)
      {
        resultProcessors.Add(group, new ResultsAssemblyProcessor(Path.Combine(resultDir, @"result_assembly\result_assembly.csv"), unitData, allResultTypes, cases, elemIds));
      }
      else if (group == ResultGroup.Element1d)
      {
        resultProcessors.Add(group, new Results1dProcessor(Path.Combine(resultDir, @"result_elem_1d\result_elem_1d.csv"), unitData, allResultTypes, cases, elemIds));
      }
      else if (group == ResultGroup.Element2d)
      {
        resultProcessors.Add(group, new Results2dProcessor(Path.Combine(resultDir, @"result_elem_2d\result_elem_2d.csv"), unitData, allResultTypes, cases, elemIds));
      }
      else if (group == ResultGroup.Node)
      {
        resultProcessors.Add(group, new ResultsNodeProcessor(Path.Combine(resultDir, @"result_node\result_node.csv"), unitData, allResultTypes, cases, elemIds));
      }
      else
      {
        return false;
      }
      resultProcessors[group].LoadFromFile();
      return true;
    }

    public bool GetResultHierarchy(ResultGroup group, int index, out Dictionary<string, Dictionary<string, object>> valueHierarchy, int dimension = 1)
    {
      valueHierarchy = (resultProcessors.ContainsKey(group)) ? resultProcessors[group].GetResultHierarchy(index) : new Dictionary<string, Dictionary<string, object>>();
      return (valueHierarchy != null && valueHierarchy.Count > 0);
    }

    public bool ClearResults(ResultGroup group)
    {
      if (resultProcessors.ContainsKey(group))
      {
        var removed = resultProcessors.Remove(group);
        if (removed)
        {
          GC.Collect();
          return true;
        }
      }
      return false;
    }

    /*
    public bool QueryResults(string tableName, IEnumerable<string> columns, string loadCase, out object[,] results, int? elemId = null)
    {

      results = new object[0, 0];
      elemId = null;
      return true;
    }
    */

    private class ResultQuery
    {
      public string TableName;
      public List<string> SrcCols;
      public List<string> DestCols;
      public ResultQuery(string tn, List<string> src, List<string> dest)
      {
        this.TableName = tn;
        this.SrcCols = src;
        this.DestCols = dest;
      }
    }

    private Dictionary<string, ResultQuery> ResultQueries = new Dictionary<string, ResultQuery>()
    {
      {  "Nodal Displacements", new ResultQuery("", new List<string>() {"a", "b" }, new List<string>() {"a", "b" }) },
      {  "1D Element Displacement", new ResultQuery("", new List<string>() {"a", "b" }, new List<string>() {"a", "b" }) },
      {  "Assembly Forces and Moments", new ResultQuery("", new List<string>() {"a", "b" }, new List<string>() {"a", "b" }) }
    };
    #endregion
  }
}
