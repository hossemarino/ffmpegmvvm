using System.Windows;
using FFMpegWrap.ViewModels;

namespace FFMpegWrap
{
    public partial class MainWindow : Window
    {
        public MainWindow(MainWindowViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}