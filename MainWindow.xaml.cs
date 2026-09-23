using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace StandalonePicturator;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var shared = Picturator.ViewModel;
        DataContext = shared;
        var analyzer = (StandalonePicturator.Viewmodel.SliderAnalyzerVm)Analyzer.DataContext;
        bool synchronizing = false;
        shared.PropertyChanged += (_, e) => {
            if (synchronizing || e.PropertyName != nameof(shared.BeatmapPath)) return;
            synchronizing = true;
            try { analyzer.OsuPath = shared.BeatmapPath; } finally { synchronizing = false; }
        };
        analyzer.PropertyChanged += (_, e) => {
            if (synchronizing || e.PropertyName != nameof(analyzer.OsuPath)) return;
            synchronizing = true;
            try { shared.BeatmapPath = analyzer.OsuPath; } finally { synchronizing = false; }
        };
        analyzer.OsuPath = shared.BeatmapPath;
    }
}
