namespace BusBuddy.WPF.Messages
{
    /// <summary>
    /// Published after a student CSV import so the students list can refresh
    /// without treating the import as a single-student save.
    /// </summary>
    public sealed class StudentsImportedMessage
    {
        public StudentsImportedMessage(int added)
        {
            Added = added;
        }

        public int Added { get; }
    }
}
