using System.Collections.Generic;
using System.Threading.Tasks;
using BusBuddy.Core.Models;

namespace BusBuddy.Core.Services
{
    public interface IGuardianService
    {
        Task<Guardian?> GetGuardianAsync(int guardianId);
        Task<List<Guardian>> GetAllGuardiansAsync();
        Task<Guardian> AddGuardianAsync(Guardian guardian);
        Task<Guardian?> UpdateGuardianAsync(Guardian guardian);
        Task<bool> DeleteGuardianAsync(int guardianId);
        Task<List<Guardian>> GetGuardiansForStudentAsync(int studentId);
    }
}
