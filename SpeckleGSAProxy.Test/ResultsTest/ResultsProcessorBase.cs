using SpeckleGSAInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSAProxy.Test.ResultsTest
{
  public abstract class ResultsProcessorBase
  {
    /*
    Nodal Displacements
    Nodal Velocity
    Nodal Acceleration
    Nodal Reaction
    Constraint Forces
    1D Element Displacement
    1D Element Force
    2D Element Displacement
    2D Element Projected Moment
    2D Element Projected Force
    2D Element Projected Stress - Bottom
    2D Element Projected Stress - Middle
    2D Element Projected Stress - Top
    */
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
      { ResultType.Element2dProjectedStressTop, "2D Element Projected Stress - Top" }
    };

    public string ResultTypeName(ResultType rt) => rtStrings[rt];
  }
}
