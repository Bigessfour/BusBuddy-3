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
    /// One context per call via <see cref="IBusBuddyDbContextFactory"/>.
    /// </summary>
    public class FamilyService : IFamilyService
    {
        private readonly IBusBuddyDbContextFactory _contextFactory;
        private readonly ILogger _logger;

        public FamilyService(IBusBuddyDbContextFactory contextFactory, ILogger logger)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Family?> GetFamilyAsync(int familyId)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();
                return await context.Families
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

        public async Task<List<Family>> GetAllFamiliesAsync()
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();
                return await context.Families
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

        public async Task<Family> AddFamilyAsync(Family family)
        {
            using var context = _contextFactory.CreateWriteDbContext();
            var db = context.Database;
            var useTxn = db is not null && db.ProviderName is not null && !db.IsInMemory();
            IDbContextTransaction? transaction = null;
            try
            {
                if (useTxn)
                {
                    transaction = await context.Database.BeginTransactionAsync();
                }

                context.Families.Add(family);
                await context.SaveChangesAsync();

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

        public async Task<Family?> UpdateFamilyAsync(Family family)
        {
            using var context = _contextFactory.CreateWriteDbContext();
            var db = context.Database;
            var useTxn = db is not null && db.ProviderName is not null && !db.IsInMemory();
            IDbContextTransaction? transaction = null;
            try
            {
                if (useTxn)
                {
                    transaction = await context.Database.BeginTransactionAsync();
                }

                var existing = await context.Families.FindAsync(family.FamilyId);
                if (existing == null)
                {
                    return null;
                }

                context.Entry(existing).CurrentValues.SetValues(family);
                await context.SaveChangesAsync();

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

        public async Task<bool> DeleteFamilyAsync(int familyId)
        {
            using var context = _contextFactory.CreateWriteDbContext();
            var db = context.Database;
            var useTxn = db is not null && db.ProviderName is not null && !db.IsInMemory();
            IDbContextTransaction? transaction = null;
            try
            {
                if (useTxn)
                {
                    transaction = await context.Database.BeginTransactionAsync();
                }

                var family = await context.Families.FindAsync(familyId);
                if (family == null)
                {
                    return false;
                }

                context.Families.Remove(family);
                await context.SaveChangesAsync();

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
