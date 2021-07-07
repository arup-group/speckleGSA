using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSAProxy.Results
{
  public enum ResultUnitType
  {
    None = 0,
    Force,
    Length,
    Disp,
    Mass,
    Time,
    Temp,
    Stress,
    Accel,
    Angle // not supported in GWA but added here to reflect its use in the UI; being unsupported in GWA, the code will hard-wire values
    //energy and others don't seem to be supported in GWA but also not needed in result extraction code so they're left out
  }

  //These span distance, force and other unit types, so that they can be put into an array which represents x per x per x, e.g. "N/m"
  internal enum ResultUnit
  {
    N,
    KN,
    mm,
    m,
    Pa,
    kPa,
    rad
  }

  public class FileToResultTableSpec
  {
    public string ElementIdCol;
    public string CaseIdCol;
    public Dictionary<string, ColMap> ResultTypeCsvColumnMap;
    public FileToResultTableSpec(string elementIdCol, string caseIdCol)
    {
      this.ElementIdCol = elementIdCol;
      this.CaseIdCol = caseIdCol;
    }
  }

  public class ColMap
  {
    //Input values from the specification
    public Dictionary<string, ImportedField> FileCols { get; }
    public Dictionary<string, CalculatedField> CalcFields { get; }
    public List<string> OrderedColumns { get; }
    //Calculated values for faster processing
    public Dictionary<FieldSpec, int> OrderedFieldSpecs { get; }

    public ColMap(Dictionary<string, ImportedField> fileCols, Dictionary<string, CalculatedField> calcFields, List<string> orderedCols)
    {
      this.FileCols = fileCols;
      this.CalcFields = calcFields;
      this.OrderedColumns = orderedCols;
      //the value is the index of the original list
      this.OrderedFieldSpecs = new Dictionary<FieldSpec, int>();
      var fileIndex = 0;
      var calcIndex = 0;
      for (int c = 0; c < OrderedColumns.Count(); c++)
      {
        var col = orderedCols[c];
        if (fileCols.ContainsKey(col))
        {
          OrderedFieldSpecs.Add(fileCols[col], fileIndex);
          fileIndex++;
        }
        else if (calcFields.ContainsKey(col))
        {
          OrderedFieldSpecs.Add(calcFields[col], calcIndex);
          calcIndex++;
        }
      }
    }
  }

  public abstract class FieldSpec
  {
    public ResultUnitType[] UnitTypes;  //Multiple, because they can be N/m, etc
    public FieldSpec(ResultUnitType[] unitTypes)
    {
      this.UnitTypes = unitTypes;
    }
  }
  
  public class CalculatedField : FieldSpec
  {
    public int[] FileColIndices;
    public Func<object[], object> CalcFn;
    
    public CalculatedField(Func<object[], object> calcFn, ResultUnitType[] unitTypes, params int[] fileColIndices) : base(unitTypes)
    {
      this.FileColIndices = fileColIndices;
      this.CalcFn = calcFn;
    }

    public CalculatedField(Func<object[], object> calcFn, ResultUnitType unitType, params int[] fileColIndices) : base(new[] { unitType })
    {
      this.FileColIndices = fileColIndices;
      this.CalcFn = calcFn;
    }
  }

  public class ImportedField : FieldSpec
  {
    public string FileCol;
    public Type DestType;

    public ImportedField(string fileCol, Type destType, ResultUnitType[] unitTypes) : base(unitTypes)
    {
      this.FileCol = fileCol;
      this.DestType = destType;
    }

    public ImportedField(string fileCol, Type destType, ResultUnitType unitType) : base(new[] { unitType })
    {
      this.FileCol = fileCol;
      this.DestType = destType;
    }
  }
}
