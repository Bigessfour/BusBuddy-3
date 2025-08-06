// This file is obsolete. All seeding logic has been merged into SeedDataService. Safe to delete.
            new Bus { BusNumber = "SB-004", Make = "Blue Bird", Model = "All American", LicenseNumber = "SB-004", SeatingCapacity = 90, VINNumber = "1BAANKCL7LF123459", Year = 2020, Status = "Active" },
            new Bus { BusNumber = "SB-005", Make = "IC Bus", Model = "RE Series", LicenseNumber = "SB-005", SeatingCapacity = 35, VINNumber = "1BAANKCL7LF123460", Year = 2018, Status = "Active" },
            new Bus { BusNumber = "SB-006", Make = "Thomas Built", Model = "Minotour", LicenseNumber = "SB-006", SeatingCapacity = 24, VINNumber = "1BAANKCL7LF123461", Year = 2022, Status = "Active" },
            new Bus { BusNumber = "SB-007", Make = "Blue Bird", Model = "Micro Bird", LicenseNumber = "SB-007", SeatingCapacity = 30, VINNumber = "1BAANKCL7LF123462", Year = 2021, Status = "Active" },
            new Bus { BusNumber = "SB-008", Make = "IC Bus", Model = "AC Series", LicenseNumber = "SB-008", SeatingCapacity = 48, VINNumber = "1BAANKCL7LF123463", Year = 2019, Status = "Active" },
            new Bus { BusNumber = "SB-009", Make = "Thomas Built", Model = "Saf-T-Liner HDX", LicenseNumber = "SB-009", SeatingCapacity = 81, VINNumber = "1BAANKCL7LF123464", Year = 2020, Status = "Active" },
            new Bus { BusNumber = "SB-010", Make = "Blue Bird", Model = "Vision", LicenseNumber = "SB-010", SeatingCapacity = 72, VINNumber = "1BAANKCL7LF123465", Year = 2021, Status = "Active" },
            new Bus { BusNumber = "SB-011", Make = "IC Bus", Model = "CE Series", LicenseNumber = "SB-011", SeatingCapacity = 84, VINNumber = "1BAANKCL7LF123466", Year = 2018, Status = "Active" },
            new Bus { BusNumber = "SB-012", Make = "Thomas Built", Model = "Saf-T-Liner C2", LicenseNumber = "SB-012", SeatingCapacity = 77, VINNumber = "1BAANKCL7LF123467", Year = 2022, Status = "Active" },
            new Bus { BusNumber = "SB-013", Make = "Blue Bird", Model = "All American", LicenseNumber = "SB-013", SeatingCapacity = 90, VINNumber = "1BAANKCL7LF123468", Year = 2020, Status = "Active" }
        };

        await _context.Vehicles.AddRangeAsync(vehicles);
        Logger.Information($"✅ Added {vehicles.Length} vehicles");
    }

    private async Task SeedActivitiesAsync()
    {
        Logger.Information("📅 Seeding activities...");

        var baseDate = DateTime.Today;
        var activities = new List<Activity>();

        // Morning Routes (7:00 AM - 8:30 AM)
        for (int day = 0; day < 5; day++) // Weekdays
        {
            var date = baseDate.AddDays(day);

            activities.AddRange(new[]
            {
                new Activity { ActivityType = "Transport", Description = "Elementary schools pickup route", Date = date, LeaveTime = new TimeSpan(7, 0, 0), EventTime = new TimeSpan(8, 0, 0), Destination = "Lincoln Elementary", RequestedBy = "Transportation Dept", Status = "Scheduled", AssignedVehicleId = 1 },
                new Activity { ActivityType = "Transport", Description = "Middle school pickup route", Date = date, LeaveTime = new TimeSpan(7, 15, 0), EventTime = new TimeSpan(8, 15, 0), Destination = "Roosevelt Middle School", RequestedBy = "Transportation Dept", Status = "Scheduled", AssignedVehicleId = 2 },
                new Activity { ActivityType = "Transport", Description = "High school pickup route", Date = date, LeaveTime = new TimeSpan(6, 30, 0), EventTime = new TimeSpan(7, 30, 0), Destination = "Washington High School", RequestedBy = "Transportation Dept", Status = "Scheduled", AssignedVehicleId = 3 },
                new Activity { ActivityType = "Special Transport", Description = "Special needs students transport", Date = date, LeaveTime = new TimeSpan(7, 30, 0), EventTime = new TimeSpan(8, 30, 0), Destination = "Multiple Locations", RequestedBy = "Special Ed Dept", Status = "Scheduled", AssignedVehicleId = 1 },
                new Activity { ActivityType = "Field Trip", Description = "5th grade field trip to science museum", Date = date, LeaveTime = new TimeSpan(9, 0, 0), EventTime = new TimeSpan(15, 0, 0), Destination = "Science Museum", RequestedBy = "Ms. Johnson", Status = "Scheduled", AssignedVehicleId = 2 },
                new Activity { ActivityType = "Athletics", Description = "Soccer team away game transport", Date = date, LeaveTime = new TimeSpan(14, 0, 0), EventTime = new TimeSpan(18, 0, 0), Destination = "Away School", RequestedBy = "Coach Smith", Status = "Scheduled", AssignedVehicleId = 3 }
            });
        }

        // Afternoon Routes (2:30 PM - 4:00 PM)
        for (int day = 0; day < 5; day++) // Weekdays
        {
            var date = baseDate.AddDays(day);

            activities.AddRange(new[]
            {
                new Activity { ActivityType = "Transport", Description = "Elementary schools dropoff route", Date = date, LeaveTime = new TimeSpan(14, 30, 0), EventTime = new TimeSpan(15, 30, 0), Destination = "Lincoln Elementary", RequestedBy = "Transportation Dept", Status = "Scheduled", AssignedVehicleId = 1 },
                new Activity { ActivityType = "Transport", Description = "Middle school dropoff route", Date = date, LeaveTime = new TimeSpan(15, 0, 0), EventTime = new TimeSpan(16, 0, 0), Destination = "Roosevelt Middle School", RequestedBy = "Transportation Dept", Status = "Scheduled", AssignedVehicleId = 2 },
                new Activity { ActivityType = "Transport", Description = "High school dropoff route", Date = date, LeaveTime = new TimeSpan(15, 15, 0), EventTime = new TimeSpan(16, 15, 0), Destination = "Washington High School", RequestedBy = "Transportation Dept", Status = "Scheduled", AssignedVehicleId = 3 },
                new Activity { ActivityType = "Special Transport", Description = "Special needs students return transport", Date = date, LeaveTime = new TimeSpan(15, 30, 0), EventTime = new TimeSpan(16, 30, 0), Destination = "Multiple Locations", RequestedBy = "Special Ed Dept", Status = "Scheduled", AssignedVehicleId = 1 }
            });
        }

        await _context.Activities.AddRangeAsync(activities);
        Logger.Information($"✅ Added {activities.Count} activities");
    }

    /// <summary>
    /// Gets a summary of seeded data for verification
    /// </summary>
    public async Task<string> GetDataSummaryAsync()
    {
        var driverCount = await _context.Drivers.CountAsync();
        var vehicleCount = await _context.Vehicles.CountAsync();
        var activityCount = await _context.Activities.CountAsync();

        return $"📊 Phase 1 Data Summary:\n" +
               $"🚌 Drivers: {driverCount}\n" +
               $"🚐 Vehicles: {vehicleCount}\n" +
               $"📅 Activities: {activityCount}";
    }
}
