using System;
using System.Collections.Generic;

namespace BusBuddy.Core.Models;

public class WileyFamily
{
    public int Id { get; set; }
    public string ParentGuardian { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string County { get; set; } = string.Empty;
    public string? Zip { get; set; }
    public string? HomePhone { get; set; }
    public string? CellPhone { get; set; }
    public string? EmergencyContact { get; set; }
    public string? JointParent { get; set; }
    public string? DataQuality { get; set; }
}

public class WileyStudent
{
    public int FamilyId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string? Grade { get; set; }
    public string School { get; set; } = string.Empty;
    public string HomeAddress { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string? Zip { get; set; }
    public string ParentGuardian { get; set; } = string.Empty;
    public string? HomePhone { get; set; }
    public string? EmergencyPhone { get; set; }
    public string? FullTimeTransport { get; set; }
    public string? InfrequentTransport { get; set; }
    public string? TransportationNotes { get; set; }
    public bool Active { get; set; }
    public DateTime? EnrollmentDate { get; set; }
    public string? DataQuality { get; set; }
}

public class WileyRoute
{
    public string RouteName { get; set; } = string.Empty;
    public string RouteType { get; set; } = string.Empty;
    public string SchoolName { get; set; } = string.Empty;
    public string Driver { get; set; } = string.Empty;
    public string Vehicle { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string ServiceArea { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class WileyBusStop
{
    public string StopName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string RouteName { get; set; } = string.Empty;
    public List<string> Students { get; set; } = new();
    public bool IsActive { get; set; }
}

public class WileySchoolDistrictData
{
    public List<WileyFamily> Families { get; set; } = new();
    public List<WileyStudent> Students { get; set; } = new();
    public List<WileyRoute> Routes { get; set; } = new();
    public List<WileyBusStop> BusStops { get; set; } = new();
}
