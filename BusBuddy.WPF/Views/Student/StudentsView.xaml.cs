using System.Windows.Controls;
using BusBuddy.WPF.ViewModels.Student;

namespace BusBuddy.WPF.Views.Student
{
    /// <summary>
    /// Interaction logic for StudentsView.xaml
    /// Student management view with Syncfusion SfDataGrid for listing and managing students
    /// Enhanced for route building with AI optimization and mapping integration
    /// </summary>
    public partial class StudentsView : UserControl
    {
        public StudentsView()
        {
            InitializeComponent();

            // Set the ViewModel for data binding
            DataContext = new StudentsViewModel();
        }
    }
}
