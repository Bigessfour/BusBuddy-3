using System.ComponentModel;

namespace BusBuddy.Core.Models
{
    /// <summary>
    /// Represents a data point for Syncfusion chart controls in the BusBuddy dashboard
    /// </summary>
    public class ChartDataPoint : INotifyPropertyChanged
    {
        private string _category = string.Empty;
        private double _value;
        private string? _label;

        /// <summary>
        /// Category or label for the data point (e.g., "On Time", "Late", "Early")
        /// </summary>
        public string Category
        {
            get => _category;
            set
            {
                _category = value;
                OnPropertyChanged(nameof(Category));
            }
        }

        /// <summary>
        /// Numeric value for the data point
        /// </summary>
        public double Value
        {
            get => _value;
            set
            {
                _value = value;
                OnPropertyChanged(nameof(Value));
            }
        }

        /// <summary>
        /// Optional display label for the data point
        /// </summary>
        public string? Label
        {
            get => _label;
            set
            {
                _label = value;
                OnPropertyChanged(nameof(Label));
            }
        }

        /// <summary>
        /// Additional properties for advanced chart scenarios
        /// </summary>
        public string? Color { get; set; }
        public string? Description { get; set; }
        public DateTime? Timestamp { get; set; }

        #region INotifyPropertyChanged Implementation

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    /// <summary>
    /// Specialized chart data point for time-series data
    /// </summary>
    public class TimeSeriesDataPoint : ChartDataPoint
    {
        private DateTime _dateTime;

        /// <summary>
        /// Date/time for the data point
        /// </summary>
        public DateTime DateTime
        {
            get => _dateTime;
            set
            {
                _dateTime = value;
                OnPropertyChanged(nameof(DateTime));
            }
        }
    }

    /// <summary>
    /// Chart data point with additional performance metrics
    /// </summary>
    public class PerformanceDataPoint : ChartDataPoint
    {
        private double _target;
        private double _actual;

        /// <summary>
        /// Target value for performance comparison
        /// </summary>
        public double Target
        {
            get => _target;
            set
            {
                _target = value;
                OnPropertyChanged(nameof(Target));
            }
        }

        /// <summary>
        /// Actual value achieved
        /// </summary>
        public double Actual
        {
            get => _actual;
            set
            {
                _actual = value;
                OnPropertyChanged(nameof(Actual));
            }
        }

        /// <summary>
        /// Calculated variance from target (Actual - Target)
        /// </summary>
        public double Variance => Actual - Target;

        /// <summary>
        /// Percentage achievement (Actual / Target * 100)
        /// </summary>
        public double PercentageAchievement => Target > 0 ? (Actual / Target) * 100 : 0;
    }
}
