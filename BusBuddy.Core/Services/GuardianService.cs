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
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.Guardians.Add(guardian);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return guardian;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.Error(ex, "Error adding guardian");
                throw;
            }
        }

        public async Task<Guardian?> UpdateGuardianAsync(Guardian guardian)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var existing = await _context.Guardians.FindAsync(guardian.GuardianId);
                if (existing == null) return null;

                _context.Entry(existing).CurrentValues.SetValues(guardian);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return existing;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.Error(ex, "Error updating guardian {GuardianId}", guardian.GuardianId);
                return null;
            }
        }

        public async Task<bool> DeleteGuardianAsync(int guardianId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var guardian = await _context.Guardians.FindAsync(guardianId);
                if (guardian == null) return false;

                _context.Guardians.Remove(guardian);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.Error(ex, "Error deleting guardian {GuardianId}", guardianId);
                return false;
            }
        }

        public async Task<List<Guardian>> GetGuardiansForStudentAsync(int studentId)
        {
            try
            {
                var student = await _context.Students
                    .Include(s => s.Family)
                    .ThenInclude(f => f.Guardians)
                    .FirstOrDefaultAsync(s => s.StudentId == studentId);

                // Use null-conditional operator to avoid possible null dereference
                return student?.Family?.Guardians != null ? student.Family.Guardians.ToList() : new List<Guardian>();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting guardians for student {StudentId}", studentId);
                return new List<Guardian>();
            }
        }
    }
}
