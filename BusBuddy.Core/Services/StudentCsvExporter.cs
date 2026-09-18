using System.Text;
using BusBuddy.Core.Models;
using BusBuddy.Core.Utilities;
using Serilog;

namespace BusBuddy.Core.Services;

/// <summary>Roster CSV export. Not student intake CRUD.</summary>
public static class StudentCsvExporter
{
    private static readonly ILogger Logger = Log.ForContext(typeof(StudentCsvExporter));

    public static string ToCsv(IReadOnlyList<Student> students)
    {
        Logger.Information("Exporting students to CSV format");

        var csv = new StringBuilder();
        csv.AppendLine("Student ID,Student Number,Student Name,Grade,School,Home Address,City,State,ZIP," +
                      "Home Phone,Parent/Guardian,Emergency Phone,AM Route,PM Route,Bus Stop," +
                      "Medical Notes,Transportation Notes,Active,Enrollment Date");

        foreach (var student in students)
        {
            csv.AppendLine(CsvLine.Join(
                student.StudentId,
                student.StudentNumber,
                student.StudentName,
                student.Grade,
                student.School,
                student.HomeAddress,
                student.City,
                student.State,
                student.Zip,
                student.HomePhone,
                student.ParentGuardian,
                student.EmergencyPhone,
                student.AMRoute,
                student.PMRoute,
                student.BusStop,
                student.MedicalNotes,
                student.TransportationNotes,
                student.Active,
                student.EnrollmentDate));
        }

        Logger.Information("Successfully exported {Count} students to CSV", students.Count);
        return csv.ToString();
    }
}
