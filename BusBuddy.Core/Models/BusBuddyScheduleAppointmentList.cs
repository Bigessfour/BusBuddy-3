namespace BusBuddy.Core.Models
{
    // Stub for BusBuddyScheduleAppointmentList
    public class BusBuddyScheduleAppointmentList : IScheduleAppointmentList
    {
        private readonly List<IScheduleAppointment> _appointments = new();

        public IScheduleAppointment this[int index]
        {
            get => _appointments[index];
            set => _appointments[index] = value;
        }

        public int Count => _appointments.Count;
        public bool IsReadOnly => false;

        public void Add(IScheduleAppointment item) => _appointments.Add(item);
        public void Clear() => _appointments.Clear();
        public bool Contains(IScheduleAppointment item) => _appointments.Contains(item);
        public void CopyTo(IScheduleAppointment[] array, int arrayIndex) => _appointments.CopyTo(array, arrayIndex);
        public IEnumerator<IScheduleAppointment> GetEnumerator() => _appointments.GetEnumerator();
        public int IndexOf(IScheduleAppointment item) => _appointments.IndexOf(item);
        public void Insert(int index, IScheduleAppointment item) => _appointments.Insert(index, item);
        public bool Remove(IScheduleAppointment item) => _appointments.Remove(item);
        public void RemoveAt(int index) => _appointments.RemoveAt(index);
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _appointments.GetEnumerator();
    }
}
