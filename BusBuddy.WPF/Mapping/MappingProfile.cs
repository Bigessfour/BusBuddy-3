using AutoMapper;
using BusBuddy.Core.Models;
using BusBuddy.WPF.Models;
using System;
using BusViewModel = BusBuddy.WPF.ViewModels.Bus.BusViewModel;

namespace BusBuddy.WPF.Mapping
{
    /// <summary>
    /// AutoMapper profile for domain models ↔ view models (Bus, Driver).
    /// Route and Student screens bind Core models. Not a geospatial map service.
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

            // Route UI binds Core.Models.Route. Do not map a parallel RouteViewModel.
            // No Student <-> StudentViewModel mapping: the student UI binds Core.Models.Student
            // directly. The old DTO split StudentName into first/last and typed Grade as int, both of
            // which contradict the Core model.
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

    }
}
