using System.Windows;
using System.Windows.Controls;

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
