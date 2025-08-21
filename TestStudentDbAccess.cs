using Microsoft.EntityFrameworkCore;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using System;

// This is a simple script to validate the Student model and database connection
namespace BusBuddy.TestScript
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                using (var context = new BusBuddyDbContext())
                {
                    // Ensure database is created with the Student table
                    context.Database.EnsureCreated();

                    // Add a test student
                    var student = new Student
                    {
                        StudentNumber = "TEST123",
                        StudentName = "Test Student"
                    };

                    context.Students.Add(student);
                    context.SaveChanges();

                    Console.WriteLine("Successfully added a test student to the database!");

                    // Query back to verify
                    var students = context.Students.ToList();
                    Console.WriteLine($"Found {students.Count} students in the database:");

                    foreach (var s in students)
                    {
                        Console.WriteLine($"Student ID: {s.StudentId}, Name: {s.StudentName}, Number: {s.StudentNumber}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Error: {ex.InnerException.Message}");
                }
            }

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
