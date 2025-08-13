using System;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using BusBuddy.WPF.ViewModels;

namespace BusBuddy.WPF.Views.Schedule
{
    public partial class UnifiedSchedulerView : UserControl
    {
        public UnifiedSchedulerView()
        {
            InitializeComponent();
            DataContext = new UnifiedSchedulerViewModel();
        }
    }
}
