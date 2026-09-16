using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace BusBuddy.Core.Services
{
    public class GuardianService : IGuardianService
    {
        private readonly IBusBuddyDbContextFactory _contextFactory;
        private readonly ILogger _logger;

        public GuardianService(IBusBuddyDbContextFactory contextFactory, ILogger logger)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Guardian?> GetGuardianAsync(int guardianId)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();
                return await context.Guardians
                    .Include(g => g.Family)
                    .FirstOrDefaultAsync(g => g.GuardianId == guardianId);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting guardian {GuardianId}", guardianId);
                return null;
            }
        }

        public async Task<List<Guardian>> GetAllGuardiansAsync()
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();
                return await context.Guardians
                    .Include(g => g.Family)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting all guardians");
                return new List<Guardian>();
            }
        }

        public async Task<Guardian> AddGuardianAsync(Guardian guardian)
        {
            using var context = _contextFactory.CreateWriteDbContext();
            var useTxn = context.Database?.ProviderName is not null && !context.Database.IsInMemory();
            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
            try
            {
                if (useTxn)
                {
                    transaction = await context.Database!.BeginTransactionAsync();
                }
                context.Guardians.Add(guardian);
                await context.SaveChangesAsync();
                if (transaction is not null)
                {
                    await transaction.CommitAsync();
                }
                return guardian;
            }
            catch (Exception ex)
            {
                if (transaction is not null)
                {
                    await transaction.RollbackAsync();
                }
                _logger.Error(ex, "Error adding guardian");
                throw;
            }
        }

        public async Task<Guardian?> UpdateGuardianAsync(Guardian guardian)
        {
            using var context = _contextFactory.CreateWriteDbContext();
            var useTxn = context.Database?.ProviderName is not null && !context.Database.IsInMemory();
            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
            try
            {
                if (useTxn)
                {
                    transaction = await context.Database!.BeginTransactionAsync();
                }
                var existing = await context.Guardians.FindAsync(guardian.GuardianId);
                if (existing == null)
                {
                    return null;
                }

                context.Entry(existing).CurrentValues.SetValues(guardian);
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
                _logger.Error(ex, "Error updating guardian {GuardianId}", guardian.GuardianId);
                return null;
            }
        }

        public async Task<bool> DeleteGuardianAsync(int guardianId)
        {
            using var context = _contextFactory.CreateWriteDbContext();
            var useTxn = context.Database?.ProviderName is not null && !context.Database.IsInMemory();
            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
            try
            {
                if (useTxn)
                {
                    transaction = await context.Database!.BeginTransactionAsync();
                }
                var guardian = await context.Guardians.FindAsync(guardianId);
                if (guardian == null)
                {
                    return false;
                }

                context.Guardians.Remove(guardian);
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
                _logger.Error(ex, "Error deleting guardian {GuardianId}", guardianId);
                return false;
            }
        }

        public async Task<List<Guardian>> GetGuardiansForStudentAsync(int studentId)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();
                var guardians = await context.Guardians
                    .Include(g => g.Family!)
                        .ThenInclude(f => f.Students)
                    .Where(g => g.Family != null && g.Family.Students.Any(s => s.StudentId == studentId))
                    .ToListAsync();

                return guardians;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting guardians for student {StudentId}", studentId);
                return new List<Guardian>();
            }
        }
    }
}
