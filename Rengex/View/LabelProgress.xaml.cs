using System.Windows;
using System.Windows.Controls;

namespace Rengex {

  /// <summary>
  /// Supported properties: Label, Value, Foreground
  /// </summary>
  public partial class LabelProgress : UserControl {

    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
       "Label",
       typeof(string),
       typeof(LabelProgress),
       new FrameworkPropertyMetadata(default(string)));

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
       "Value",
       typeof(double),
       typeof(LabelProgress),
       new FrameworkPropertyMetadata(default(double)));

    public LabelProgress() {
      InitializeComponent();
    }

    public LabelProgress(ILabelProgressVM viewModel) : this() {
      DataContext = viewModel;
    }

    public string Label {
      get { return GetValue(LabelProperty) as string; }
      set { SetValue(LabelProperty, value); }
    }

    public double Value {
      get { return (double)GetValue(ValueProperty); }
      set { SetValue(ValueProperty, value); }
    }
  }
}
