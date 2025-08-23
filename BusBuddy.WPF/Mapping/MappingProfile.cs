using AutoMapper;
using BusBuddy.Core.Models;
using BusBuddy.WPF.Models;
using System;

namespace BusBuddy.WPF.Mapping
{
    /// <summary>
    /// AutoMapper profile for mapping between domain models and view models
    /// </summary>
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // Bus mappings
            CreateMap<Bus, BusViewModel>()
                .ForMember(dest => dest.MaintenanceStatus, opt => opt.MapFrom(src =>
                    GetMaintenanceStatus(src.LastServiceDate, src.CurrentOdometer)))
                .ForMember(dest => dest.LastMaintenanceDateFormatted, opt => opt.MapFrom(src =>
                    src.LastServiceDate.HasValue ?
                    src.LastServiceDate.Value.ToString("MM/dd/yyyy") : "Not Available"))
                .ForMember(dest => dest.BusId, opt => opt.MapFrom(src => src.BusId))
                .ForMember(dest => dest.Capacity, opt => opt.MapFrom(src => src.SeatingCapacity))
                .ForMember(dest => dest.LicensePlate, opt => opt.MapFrom(src => src.LicenseNumber));

            CreateMap<BusViewModel, Bus>()
                .ForMember(dest => dest.LastServiceDate, opt => opt.Condition(src => src.LastMaintenanceDateFormatted != null))
                .ForMember(dest => dest.BusId, opt => opt.MapFrom(src => src.BusId))
                .ForMember(dest => dest.SeatingCapacity, opt => opt.MapFrom(src => src.Capacity))
                .ForMember(dest => dest.LicenseNumber, opt => opt.MapFrom(src => src.LicensePlate));

            // Driver mappings
            CreateMap<Driver, DriverViewModel>()
                .ForMember(dest => dest.FullNameWithId, opt => opt.MapFrom(src => $"{src.FullName} (ID: {src.DriverId})"))
                .ForMember(dest => dest.LicenseStatusDisplay, opt => opt.MapFrom(src => GetLicenseStatus(src.LicenseExpiryDate)))
                .ForMember(dest => dest.LicenseExpiryDateFormatted, opt => opt.MapFrom(src =>
                    src.LicenseExpiryDate.HasValue ?
                    src.LicenseExpiryDate.Value.ToString("MM/dd/yyyy") : "Unknown"));

            CreateMap<DriverViewModel, Driver>();

            // Route mappings
            CreateMap<Route, RouteViewModel>()
                .ForMember(dest => dest.StartTimeFormatted, opt => opt.MapFrom(src =>
                    src.AMBeginTime.HasValue ? src.AMBeginTime.Value.ToString(@"hh\:mm") : ""))
                .ForMember(dest => dest.EndTimeFormatted, opt => opt.MapFrom(src =>
                    src.PMBeginTime.HasValue ? src.PMBeginTime.Value.ToString(@"hh\:mm") : ""))
                .ForMember(dest => dest.DurationMinutes, opt => opt.MapFrom(src =>
                    CalculateDurationMinutes(src.AMBeginTime, src.PMBeginTime)))
                .ForMember(dest => dest.StatusDisplay, opt => opt.MapFrom(src =>
                    src.IsActive ? "Active" : "Inactive"))
                .ForMember(dest => dest.RouteNumber, opt => opt.MapFrom(src => src.RouteId.ToString()))
                .ForMember(dest => dest.DistanceMiles, opt => opt.MapFrom(src => src.Distance));

            CreateMap<RouteViewModel, Route>()
                .ForMember(dest => dest.AMBeginTime, opt => opt.Ignore())  // These are handled in conversion methods
                .ForMember(dest => dest.PMBeginTime, opt => opt.Ignore())
                .ForMember(dest => dest.Distance, opt => opt.MapFrom(src => src.DistanceMiles));

            // Student mappings
            CreateMap<Student, StudentViewModel>()
                .ForMember(dest => dest.FullName, opt => opt.MapFrom(src => src.StudentName))
                .ForMember(dest => dest.FirstName, opt => opt.MapFrom(src => ExtractFirstName(src.StudentName)))
                .ForMember(dest => dest.LastName, opt => opt.MapFrom(src => ExtractLastName(src.StudentName)))
                .ForMember(dest => dest.GradeDisplay, opt => opt.MapFrom(src => $"Grade {src.Grade}"))
                .ForMember(dest => dest.AddressFormatted, opt => opt.MapFrom(src =>
                    FormatAddress(src.HomeAddress, src.City, src.State, src.Zip)))
                .ForMember(dest => dest.Address, opt => opt.MapFrom(src => src.HomeAddress))
                .ForMember(dest => dest.ZipCode, opt => opt.MapFrom(src => src.Zip))
                .ForMember(dest => dest.ParentName, opt => opt.MapFrom(src => src.ParentGuardian))
                .ForMember(dest => dest.ParentPhone, opt => opt.MapFrom(src => src.HomePhone))
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => src.Active));

            CreateMap<StudentViewModel, Student>()
                .ForMember(dest => dest.StudentName, opt => opt.MapFrom(src => $"{src.FirstName} {src.LastName}"))
                .ForMember(dest => dest.HomeAddress, opt => opt.MapFrom(src => src.Address))
                .ForMember(dest => dest.Zip, opt => opt.MapFrom(src => src.ZipCode))
                .ForMember(dest => dest.ParentGuardian, opt => opt.MapFrom(src => src.ParentName))
                .ForMember(dest => dest.HomePhone, opt => opt.MapFrom(src => src.ParentPhone))
                .ForMember(dest => dest.Active, opt => opt.MapFrom(src => src.IsActive));
        }

        // Helper methods for name extraction
        private string ExtractFirstName(string fullName)
        {
            if (string.IsNullOrEmpty(fullName))
            {
                return string.Empty;
            }


            var parts = fullName.Split(' ');
            return parts.FirstOrDefault() ?? string.Empty;
        }

        private string ExtractLastName(string fullName)
        {
            if (string.IsNullOrEmpty(fullName))
            {
                return string.Empty;
            }


            var parts = fullName.Split(' ');
            if (parts.Length <= 1)
            {
                return string.Empty;
            }


            return string.Join(" ", parts.Skip(1));
        }

        // Helper methods for the mappings
        private string GetMaintenanceStatus(DateTime? lastMaintenanceDate, int? currentOdometer)
        {
            if (!lastMaintenanceDate.HasValue)
            {

                return "Unknown";
            }

            // If maintenance was over 6 months ago

            if (lastMaintenanceDate.Value.AddMonths(6) < DateTime.Now)
            {

                return "Maintenance Required";
            }

            // If maintenance is due within a month

            if (lastMaintenanceDate.Value.AddMonths(5) < DateTime.Now)
            {

                return "Maintenance Soon";
            }


            return "Good";
        }

        private string GetLicenseStatus(DateTime? licenseExpiryDate)
        {
            if (!licenseExpiryDate.HasValue)
            {

                return "Unknown";
            }


            if (licenseExpiryDate.Value < DateTime.Now)
            {

                return "Expired";
            }


            if (licenseExpiryDate.Value < DateTime.Now.AddDays(30))
            {

                return "Expiring Soon";
            }


            return "Valid";
        }

        private int? CalculateDurationMinutes(TimeSpan? amTime, TimeSpan? pmTime)
        {
            if (!amTime.HasValue || !pmTime.HasValue)
            {

                return null;
            }

            // Assuming PM time is later than AM time in a school day

            var amMinutes = amTime.Value.TotalMinutes;
            var pmMinutes = pmTime.Value.TotalMinutes;

            // Typically PM routes are in the afternoon, so they should be later
            return (int)(pmMinutes - amMinutes);
        }

        private string FormatAddress(string? address, string? city, string? state, string? zipCode)
        {
            if (string.IsNullOrWhiteSpace(address))
            {

                return "No address on file";
            }


            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(address))
            {
                parts.Add(address);
            }


            var cityStateParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(city))
            {
                cityStateParts.Add(city);
            }


            if (!string.IsNullOrWhiteSpace(state))
            {
                cityStateParts.Add(state);
            }


            if (cityStateParts.Count > 0)
            {
                parts.Add(string.Join(", ", cityStateParts));
            }


            if (!string.IsNullOrWhiteSpace(zipCode))
            {

                parts.Add(zipCode);
            }


            return string.Join(" ", parts);
        }
    }
}
