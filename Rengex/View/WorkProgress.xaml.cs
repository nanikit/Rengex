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
  /// Interaction logic for WorkProgress.xaml
  /// </summary>
  public partial class WorkProgress : UserControl {
    public WorkProgress() {
      InitializeComponent();
    }

    public WorkProgress(Jp2KrTranslationVM jp2kr) : this() {
      DataContext = jp2kr;
    }

    private void OngoingSizeChanged(object sender, SizeChangedEventArgs e) {
      if (e.NewSize.Height < e.PreviousSize.Height) {
        LvOngoing.Height = e.PreviousSize.Height;
      }
    }
  }
}
