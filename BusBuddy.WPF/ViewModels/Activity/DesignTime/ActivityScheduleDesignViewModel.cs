// <copyright file="ActivityScheduleDesignViewModel.cs" company="BusBuddy Transportation Solutions">
// Copyright (c) BusBuddy Transportation Solutions. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections.ObjectModel;
using BusBuddy.Core.Models;

namespace BusBuddy.WPF.ViewModels.Activity.DesignTime
{
    /// <summary>
    /// Design-time ViewModel for ActivityScheduleView to provide sample data for XAML designer.
    /// This class provides sample data without requiring dependency injection or database access,
    /// enabling proper XAML designer preview and development experience.
    /// </summary>
    /// <remarks>
    /// This ViewModel is only used at design-time and should not be instantiated at runtime.
    /// It provides sample data that matches the structure expected by the ActivityScheduleView.
    /// </remarks>
    public class ActivityScheduleDesignViewModel
    {
        /// <summary>
        /// Gets the collection of sample activity schedules for designer preview.
        /// </summary>
        public ObservableCollection<ActivitySchedule> ActivitySchedules { get; }

        /// <summary>
        /// Gets the currently selected activity schedule for designer preview.
        /// </summary>
        public ActivitySchedule? SelectedSchedule { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ActivityScheduleDesignViewModel"/> class.
        /// Populates the ViewModel with realistic sample data for XAML designer preview.
        /// </summary>
        public ActivityScheduleDesignViewModel()
        {
            // Populate with sample activity schedules for designer preview
            ActivitySchedules = new ObservableCollection<ActivitySchedule>
            {
                new()
                {
                    ActivityScheduleId = 1,
                    ScheduledDate = DateTime.Today.AddDays(7),
                    TripType = "Field Trip",
                    ScheduledVehicleId = 101,
                    ScheduledDestination = "Natural History Museum",
                    ScheduledLeaveTime = new TimeSpan(9, 0, 0),
                    ScheduledEventTime = new TimeSpan(10, 0, 0),
                    ScheduledRiders = 25,
                    ScheduledDriverId = 1,
                },
                new()
                {
                    ActivityScheduleId = 2,
                    ScheduledDate = DateTime.Today.AddDays(14),
                    TripType = "Sports Trip",
                    ScheduledVehicleId = 205,
                    ScheduledDestination = "City Sports Complex",
                    ScheduledLeaveTime = new TimeSpan(13, 30, 0),
                    ScheduledEventTime = new TimeSpan(14, 0, 0),
                    ScheduledRiders = 14,
                    ScheduledDriverId = 2,
                },
                new()
                {
                    ActivityScheduleId = 3,
                    ScheduledDate = DateTime.Today.AddDays(21),
                    TripType = "Academic Competition",
                    ScheduledVehicleId = 150,
                    ScheduledDestination = "Central High School",
                    ScheduledLeaveTime = new TimeSpan(8, 0, 0),
                    ScheduledEventTime = new TimeSpan(9, 0, 0),
                    ScheduledRiders = 8,
                    ScheduledDriverId = 3,
                },
            };

            // Set a default selected item for designer preview
            SelectedSchedule = ActivitySchedules[0];
        }
    }
}
