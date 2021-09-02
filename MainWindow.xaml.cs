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

namespace FilterGUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }
        private void Image1_SizeChanged(object sender, EventArgs e)
        {
            if (Image1.Source == null) return;
            Canvas1.Height = Image1.Source.Height * ZoomSlider.Value;
            Canvas1.Width = Image1.Source.Width * ZoomSlider.Value;
        }
    }
}
