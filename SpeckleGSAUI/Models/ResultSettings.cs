using System.Collections.Generic;
using System.Linq;

namespace SpeckleGSAUI.Models
{
  public class ResultSettings
  {
    public List<ResultSettingItem> ResultSettingItems { get; set; }

    public ResultSettings()
    {
      ResultSettingItems = SpeckleGSAProxy.GSAProxy.resultTypeSpecs.Keys.SelectMany(k => SpeckleGSAProxy.GSAProxy.resultTypeSpecs[k].ResultTypeCsvColumnMap.Keys.Select(rt => new ResultSettingItem(rt, true))).ToList();

      /*
      ResultSettingItems = new List<ResultSettingItem>()
      {
        new ResultSettingItem("Nodal Displacements", true),
        new ResultSettingItem("Nodal Velocity", false),
        new ResultSettingItem("Nodal Acceleration", false),
        new ResultSettingItem("Nodal Reaction", true),
        new ResultSettingItem("Constraint Forces", true),
        new ResultSettingItem("Nodal Forces", true),
        new ResultSettingItem("Nodal Mass", false),
        new ResultSettingItem("Nodal Soil", false),
        new ResultSettingItem("0D Element Displacement", false),
        new ResultSettingItem("0D Element Force", true),
        new ResultSettingItem("1D Element Displacement", false),
        new ResultSettingItem("1D Element End Rotation", false),
        new ResultSettingItem("1D Element Force", true),
        new ResultSettingItem("1D Element Stress", false),
        new ResultSettingItem("1D Element Derived Stress", false),
        new ResultSettingItem("1D Element Strain", false),
        new ResultSettingItem("1D Element Strain Energy Density",  false),
        new ResultSettingItem("1D Element Average Strain Energy Density",  false),
        new ResultSettingItem("1D Element Steel Utilization", false),
        new ResultSettingItem("2D Element Displacement", true),
        new ResultSettingItem("2D Element Derived Force", false),
        new ResultSettingItem("2D Element Projected Moment",true),
        new ResultSettingItem("2D Element Projected Force", false),
        new ResultSettingItem("2D Element Derived Stress - Bottom", false),
        new ResultSettingItem("2D Element Derived Stress - Middle", false),
        new ResultSettingItem("2D Element Derived Stress - Top", false),
        new ResultSettingItem("2D Element Ax Stress - Bottom", false),
        new ResultSettingItem("2D Element Ax Stress - Middle", false),
        new ResultSettingItem("2D Element Ax Stress - Top", false),
        new ResultSettingItem("2D Element Projected Stress - Bottom", false),
        new ResultSettingItem("2D Element Projected Stress - Middle", false),
        new ResultSettingItem("2D Element Projected Stress - Top", false),
        new ResultSettingItem("RC Slab Reinforcement", false),
        new ResultSettingItem("Assembly Forces and Moments", true)
      };
      */
    }
  }
}