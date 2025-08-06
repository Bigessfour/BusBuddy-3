using System.Collections.Generic;
using System.Threading.Tasks;
using BusBuddy.Core.Models;

namespace BusBuddy.Core.Services
{
    public interface IFamilyService
    {
        Task<Family?> GetFamilyAsync(int familyId);
        Task<List<Family>> GetAllFamiliesAsync();
        Task<Family> AddFamilyAsync(Family family);
        Task<Family?> UpdateFamilyAsync(Family family);
        Task<bool> DeleteFamilyAsync(int familyId);
    }
}
