using CommunityToolkit.Mvvm.Messaging.Messages;

namespace BusBuddy.WPF.Messages
{
    /// <summary>
    /// Published after a student CSV import so the students list can refresh
    /// without treating the import as a single-student save.
    /// </summary>
    public sealed class StudentsImportedMessage : ValueChangedMessage<int>
    {
        public StudentsImportedMessage(int added) : base(added)
        {
        }

        public int Added => Value;
    }
}
