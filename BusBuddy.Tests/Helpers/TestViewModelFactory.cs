using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using BusBuddy.Core.Data;
using BusBuddy.Core.Services;
using BusBuddy.Core.Domain;
using BusBuddy.WPF.ViewModels.Student;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;

namespace BusBuddy.Tests.Helpers
{
    /// <summary>
    /// Factory for creating test instances of ViewModels with proper dependency injection
    /// </summary>
    public static class TestViewModelFactory
    {
        /// <summary>
        /// Creates a StudentFormViewModel configured for testing with in-memory database
        /// </summary>
        public static StudentFormViewModel CreateStudentFormViewModel(
            BusBuddyDbContext testContext,
            IStudentService studentService,
            Student? student = null,
            bool enableValidation = true)
        {
            // Create a test ViewModel that uses the provided test context
            return new TestStudentFormViewModel(testContext, studentService, student, enableValidation);
        }
    }

    /// <summary>
    /// Test-specific StudentFormViewModel that accepts a test context directly
    /// </summary>
    public class TestStudentFormViewModel : StudentFormViewModel
    {
        private readonly BusBuddyDbContext _testContext;

        public TestStudentFormViewModel(
            BusBuddyDbContext testContext,
            IStudentService studentService,
            Student? student = null,
            bool enableValidation = true)
            : base(studentService, student, enableValidation)
        {
            _testContext = testContext;
            
            // Override the context with our test context
            SetTestContext(_testContext);
        }

        private void SetTestContext(BusBuddyDbContext testContext)
        {
            // Use reflection to set the private _context field
            var contextField = typeof(StudentFormViewModel).GetField("_context", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            contextField?.SetValue(this, testContext);
        }
    }
}
