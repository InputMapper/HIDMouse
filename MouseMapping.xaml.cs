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
using ODIF;

namespace HIDMouse
{
    /// <summary>
    /// Interaction logic for MouseMapping.xaml
    /// </summary>
    public partial class MouseMapping : SourceMappingWindow
    {
        public MouseMapping(Device device) : base(device)
        {
            InitializeComponent();
        }
    }
}
