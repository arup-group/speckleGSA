using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSAProxy.Results
{
  /*
  internal class SpeckleGSAResultTypeSpec
  {
    //This details which file to load the results from
    private ResultCsvGroup ResultCsvGroup;
    //Specification of which columns to read from the file
    private string ElementIdCol;
    private string CaseIdCol;
    private Dictionary<string, Dictionary<string, string>> ResultTypeCsvColumnMap;
    private Dictionary<string, CalculatedField> ResultTypeCalcColumns;
    //Output
    public List<string> Headers;
    public object[,] Data;

    public SpeckleGSAResultTypeSpec(ResultCsvGroup resultCsvGroup, string elementIdCol, string caseIdCol,
      Dictionary<string, Dictionary<string, string>> resultTypeCsvColumnMap,
      Dictionary<string, CalculatedField> resultTypeCalcColumns)
    {
      this.ResultCsvGroup = resultCsvGroup;
      this.ElementIdCol = elementIdCol;
      this.CaseIdCol = caseIdCol;
      this.ResultTypeCsvColumnMap = resultTypeCsvColumnMap;
      this.ResultTypeCalcColumns = resultTypeCalcColumns;
    }
  }
  */

  internal enum ResultCsvGroup
  {
    Unknown = 0,
    Node,
    Element1d,
    Element2d,
    Assembly
  }

  internal class FileToResultTableSpec
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

  internal class ColMap
  {
    public Dictionary<string, ImportedField> FileCols { get; }
    public Dictionary<string, CalculatedField> CalcFields { get; }
    public List<string> OrderedColumns { get; }
    public ColMap(Dictionary<string, ImportedField> fileCols, Dictionary<string, CalculatedField> calcFields, List<string> orderedCols)
    {
      this.FileCols = fileCols;
      this.CalcFields = calcFields;
      this.OrderedColumns = orderedCols;
    }
  }
  
  internal class CalculatedField
  {
    public int[] FileColIndices;
    public Func<object[], object> CalcFn;
    public CalculatedField(Func<object[], object> calcFn, params int[] fileColIndices)
    {
      this.FileColIndices = fileColIndices;
      this.CalcFn = calcFn;
    }
  }

  internal class ImportedField
  {
    public string FileCol;
    public Type DestType;

    public ImportedField(string fileCol, Type destType)
    {
      this.FileCol = fileCol;
      this.DestType = destType;
    }
  }
}
