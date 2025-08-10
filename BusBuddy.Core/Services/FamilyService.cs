using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Serilog;

namespace BusBuddy.Core.Services
{
    /// <summary>
    /// Service for managing Family entities with async CRUD operations.
    /// Uses DI for BusBuddyDbContext and Serilog ILogger.
    /// </summary>
    public class FamilyService : IFamilyService
    {
        private readonly BusBuddyDbContext _context;
        private readonly ILogger _logger;

        /// <summary>
        /// Constructs FamilyService with injected DbContext and logger.
        /// </summary>
        /// <param name="context">Injected BusBuddyDbContext</param>
        /// <param name="logger">Injected Serilog ILogger</param>
        public FamilyService(BusBuddyDbContext context, ILogger logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets a Family by its ID, including Students and Guardians.
        /// </summary>
        /// <param name="familyId">Family ID</param>
        /// <returns>Family or null</returns>
        public async Task<Family?> GetFamilyAsync(int familyId)
        {
            try
            {
                return await _context.Families
                    .Include(f => f.Students)
                    .Include(f => f.Guardians)
                    .FirstOrDefaultAsync(f => f.FamilyId == familyId);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting family {FamilyId}", familyId);
                return null;
            }
        }

        /// <summary>
        /// Gets all Families, including Students and Guardians.
        /// </summary>
        /// <returns>List of Families</returns>
        public async Task<List<Family>> GetAllFamiliesAsync()
        {
            try
            {
                return await _context.Families
                    .Include(f => f.Students)
                    .Include(f => f.Guardians)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting all families");
                return new List<Family>();
            }
        }

        /// <summary>
        /// Adds a new Family and commits transaction.
        /// </summary>
        /// <param name="family">Family entity</param>
        /// <returns>Added Family</returns>
        public async Task<Family> AddFamilyAsync(Family family)
        {
            var db = _context.Database;
            var useTxn = db.ProviderName is not null && !db.IsInMemory();
            IDbContextTransaction? transaction = null;
            try
            {
                if (useTxn)
                {
                    transaction = await _context.Database.BeginTransactionAsync();
                }

                _context.Families.Add(family);
                await _context.SaveChangesAsync();

                if (transaction is not null)
                {
                    await transaction.CommitAsync();
                }
                return family;
            }
            catch (Exception ex)
            {
                if (transaction is not null)
                {
                    await transaction.RollbackAsync();
                }
                _logger.Error(ex, "Error adding family");
                throw;
            }
        }

        /// <summary>
        /// Updates an existing Family and commits transaction.
        /// </summary>
        /// <param name="family">Family entity</param>
        /// <returns>Updated Family or null</returns>
        public async Task<Family?> UpdateFamilyAsync(Family family)
        {
            var db = _context.Database;
            var useTxn = db.ProviderName is not null && !db.IsInMemory();
            IDbContextTransaction? transaction = null;
            try
            {
                if (useTxn)
                {
                    transaction = await _context.Database.BeginTransactionAsync();
                }

                var existing = await _context.Families.FindAsync(family.FamilyId);
                if (existing == null)
                {
                    return null;
                }

                _context.Entry(existing).CurrentValues.SetValues(family);
                await _context.SaveChangesAsync();

                if (transaction is not null)
                {
                    await transaction.CommitAsync();
                }
                return existing;
            }
            catch (Exception ex)
            {
                if (transaction is not null)
                {
                    await transaction.RollbackAsync();
                }
                _logger.Error(ex, "Error updating family {FamilyId}", family.FamilyId);
                return null;
            }
        }

        /// <summary>
        /// Deletes a Family by ID and commits transaction.
        /// </summary>
        /// <param name="familyId">Family ID</param>
        /// <returns>True if deleted, false if not found</returns>
        public async Task<bool> DeleteFamilyAsync(int familyId)
        {
            var db = _context.Database;
            var useTxn = db.ProviderName is not null && !db.IsInMemory();
            IDbContextTransaction? transaction = null;
            try
            {
                if (useTxn)
                {
                    transaction = await _context.Database.BeginTransactionAsync();
                }

                var family = await _context.Families.FindAsync(familyId);
                if (family == null)
                {
                    return false;
                }

                _context.Families.Remove(family);
                await _context.SaveChangesAsync();

                if (transaction is not null)
                {
                    await transaction.CommitAsync();
                }
                return true;
            }
            catch (Exception ex)
            {
                if (transaction is not null)
                {
                    await transaction.RollbackAsync();
                }
                _logger.Error(ex, "Error deleting family {FamilyId}", familyId);
                return false;
            }
        }
    }
}
