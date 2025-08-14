using System;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using BusBuddy.WPF.ViewModels;
using Syncfusion.UI.Xaml.Scheduler;
using System.ComponentModel;

namespace BusBuddy.WPF.Views.Schedule
{
    public partial class UnifiedSchedulerView : UserControl
    {
        public UnifiedSchedulerView()
        {
            InitializeComponent();
            DataContext = new UnifiedSchedulerViewModel();
        }

        // Based on Syncfusion SfScheduler docs: cancel editor, deletion, drag, and context menus to make the view read-only
        // https://help.syncfusion.com/cr/wpf/Syncfusion.UI.Xaml.Scheduler.SfScheduler.html#events
        private void Scheduler_AppointmentEditorOpening(object? sender, AppointmentEditorOpeningEventArgs e)
        {
            // Cancel editor opening to keep scheduler read-only (Syncfusion docs: SfScheduler.AppointmentEditorOpening)
            e.Cancel = true;
        }

        private void Scheduler_AppointmentDeleting(object? sender, AppointmentDeletingEventArgs e)
        {
            // Cancel deletion to keep scheduler read-only (Syncfusion docs: SfScheduler.AppointmentDeleting)
            e.Cancel = true;
        }

        private void Scheduler_AppointmentDragStarting(object? sender, AppointmentDragStartingEventArgs e)
        {
            if (e is CancelEventArgs cea)
            {
                cea.Cancel = true;
            }
        }

        private void Scheduler_SchedulerContextMenuOpening(object? sender, SchedulerContextMenuOpeningEventArgs e)
        {
            // Cancel context menu to prevent editing actions (Syncfusion docs: SfScheduler.SchedulerContextMenuOpening)
            e.Cancel = true;
        }
    }
}
