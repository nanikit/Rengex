using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

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

    public string Label {
      get { return GetValue(LabelProperty) as string; }
      set { SetValue(LabelProperty, value); }
    }

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
       "Value",
       typeof(double),
       typeof(LabelProgress),
       new FrameworkPropertyMetadata(default(double)));

    public double Value {
      get { return (double)GetValue(ValueProperty); }
      set { SetValue(ValueProperty, value); }
    }

    public LabelProgress() {
      InitializeComponent();
    }

    public LabelProgress(ILabelProgressVM viewModel) : this() {
      DataContext = viewModel;
    }
  }
}
