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
  /// Interaction logic for LabelProgress.xaml
  /// </summary>
  public partial class LabelProgress : Grid {
    public LabelProgress() {
      InitializeComponent();
    }

    public void SetProgressAndLabel(double progress, string description) {
      PbProgress.Value = progress;
      TbLabel.Text = description;
    }

    public void SetError(string description) {
      PbProgress.Value = 100;
      PbProgress.Foreground = new SolidColorBrush(Colors.Red);
      TbLabel.Text = description;
    }
  }
}
