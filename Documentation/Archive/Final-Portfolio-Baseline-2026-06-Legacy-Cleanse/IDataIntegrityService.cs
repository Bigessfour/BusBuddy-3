using System.Collections.Generic;
using System.Threading.Tasks;
using BusBuddy.WPF.Models;

namespace BusBuddy.WPF.Services
{
    /// <summary>
    /// Interface for data integrity validation services
    /// </summary>
    public interface IDataIntegrityService
    {
        /// <summary>
        /// Perform comprehensive data integrity validation across all entities
        /// </summary>
        Task<DataIntegrityReport> ValidateAllDataAsync();

        /// <summary>
        /// Validate route data integrity
        /// </summary>
        Task<List<DataIntegrityIssue>> ValidateRoutesAsync();

        /// <summary>
        /// Validate activity data integrity
        /// </summary>
        Task<List<DataIntegrityIssue>> ValidateActivitiesAsync();

        /// <summary>
        /// Validate student data integrity
        /// </summary>
        Task<List<DataIntegrityIssue>> ValidateStudentsAsync();

        /// <summary>
        /// Validate driver data integrity
        /// </summary>
        Task<List<DataIntegrityIssue>> ValidateDriversAsync();

        /// <summary>
        /// Validate vehicle data integrity
        /// </summary>
        Task<List<DataIntegrityIssue>> ValidateVehiclesAsync();

        /// <summary>
        /// Validate cross-entity relationships and business rules
        /// </summary>
        Task<List<DataIntegrityIssue>> ValidateCrossEntityRelationshipsAsync();

        /// <summary>
        /// Validate specific entity by ID
        /// </summary>
        Task<List<DataIntegrityIssue>> ValidateEntityAsync(string entityType, int entityId);
    }
}
