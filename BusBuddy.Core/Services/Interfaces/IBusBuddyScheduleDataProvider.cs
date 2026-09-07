using BusBuddy.Core.Models;

namespace BusBuddy.Core.Services.Interfaces
{
    public interface IBusBuddyScheduleDataProvider
    {
        bool IsDirty { get; set; }
        void CommitChanges();
    }
}
