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
        // Use the canonical context type
        private readonly BusBuddy.Core.Data.BusBuddyDbContext _context;
        private readonly ILogger _logger;

        public GuardianService(BusBuddyDbContext context, ILogger logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Guardian?> GetGuardianAsync(int guardianId)
        {
            try
            {
                return await _context.Guardians
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
                return await _context.Guardians
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
            var useTxn = _context.Database?.ProviderName is not null && !_context.Database.IsInMemory();
            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = default;
            try
            {
                if (useTxn)
                {
                    transaction = await _context.Database!.BeginTransactionAsync();
                }
                _context.Guardians.Add(guardian);
                await _context.SaveChangesAsync();
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
            var useTxn = _context.Database?.ProviderName is not null && !_context.Database.IsInMemory();
            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = default;
            try
            {
                if (useTxn)
                {
                    transaction = await _context.Database!.BeginTransactionAsync();
                }
                var existing = await _context.Guardians.FindAsync(guardian.GuardianId);
                if (existing == null)
                {
                    return null;
                }

                _context.Entry(existing).CurrentValues.SetValues(guardian);
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
                _logger.Error(ex, "Error updating guardian {GuardianId}", guardian.GuardianId);
                return null;
            }
        }

        public async Task<bool> DeleteGuardianAsync(int guardianId)
        {
            var useTxn = _context.Database?.ProviderName is not null && !_context.Database.IsInMemory();
            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = default;
            try
            {
                if (useTxn)
                {
                    transaction = await _context.Database!.BeginTransactionAsync();
                }
                var guardian = await _context.Guardians.FindAsync(guardianId);
                if (guardian == null)
                {
                    return false;
                }

                _context.Guardians.Remove(guardian);
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
                _logger.Error(ex, "Error deleting guardian {GuardianId}", guardianId);
                return false;
            }
        }

        public async Task<List<Guardian>> GetGuardiansForStudentAsync(int studentId)
        {
            try
            {
                var guardians = await _context.Guardians
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
