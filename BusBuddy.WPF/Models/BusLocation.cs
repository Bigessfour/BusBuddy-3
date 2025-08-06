using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BusBuddy.WPF.Models
{
    /// <summary>
    /// Represents a bus location for Google Earth integration
    /// </summary>
    public class BusLocation : INotifyPropertyChanged
    {
        private string _busNumber = string.Empty;
        private string _routeNumber = string.Empty;
        private string _status = string.Empty;
        private double _latitude;
        private double _longitude;
        private double _heading;
        private double _speed;
        private DateTime _lastUpdate = DateTime.Now;
        private string _driver = string.Empty;

        /// <summary>
        /// Bus identification number
        /// </summary>
        public string BusNumber
        {
            get => _busNumber;
            set
            {
                _busNumber = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Route number the bus is currently on
        /// </summary>
        public string RouteNumber
        {
            get => _routeNumber;
            set
            {
                _routeNumber = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Current status of the bus
        /// </summary>
        public string Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Latitude coordinate
        /// </summary>
        public double Latitude
        {
            get => _latitude;
            set
            {
                _latitude = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Longitude coordinate
        /// </summary>
        public double Longitude
        {
            get => _longitude;
            set
            {
                _longitude = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Heading direction in degrees
        /// </summary>
        public double Heading
        {
            get => _heading;
            set
            {
                _heading = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Current speed in mph
        /// </summary>
        public double Speed
        {
            get => _speed;
            set
            {
                _speed = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Last update timestamp
        /// </summary>
        public DateTime LastUpdate
        {
            get => _lastUpdate;
            set
            {
                _lastUpdate = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Driver name
        /// </summary>
        public string Driver
        {
            get => _driver;
            set
            {
                _driver = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
