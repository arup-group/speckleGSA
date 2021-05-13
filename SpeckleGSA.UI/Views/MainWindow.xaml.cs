using System.Windows;

namespace SpeckleGSA.UI
{
  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class MainWindow : Window
  {
    public MainWindow()
    {
      var test1 = SpeckleStructuralGSA.Schema.AnalysisType.BAR;
      var test2 = SpeckleStructuralClasses.StructuralSpringPropertyType.Axial;
      InitializeComponent();
    }
  }
}
