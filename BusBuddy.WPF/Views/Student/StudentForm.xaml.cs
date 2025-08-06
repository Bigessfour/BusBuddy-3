using System.Windows;
using BusBuddy.WPF.ViewModels.Student;

namespace BusBuddy.WPF.Views.Student
{
    /// <summary>
    /// Interaction logic for StudentForm.xaml
    /// </summary>
    public partial class StudentForm : Window
    {
        public StudentFormViewModel ViewModel { get; private set; }

        public StudentForm()
        {
            InitializeComponent();
            ViewModel = new StudentFormViewModel();
            DataContext = ViewModel;

            // Subscribe to ViewModel events for form closure
            ViewModel.RequestClose += OnRequestClose;
        }

        public StudentForm(Core.Models.Student student) : this()
        {
            ViewModel = new StudentFormViewModel(student);
            DataContext = ViewModel;
            ViewModel.RequestClose += OnRequestClose;
        }

        private void OnRequestClose(object? sender, bool? dialogResult)
        {
            DialogResult = dialogResult;
            Close();
        }

        protected override void OnClosed(System.EventArgs e)
        {
            // Unsubscribe from events to prevent memory leaks
            if (ViewModel != null)
            {
                ViewModel.RequestClose -= OnRequestClose;
                ViewModel.Dispose();
            }
            base.OnClosed(e);
        }
    }
}
