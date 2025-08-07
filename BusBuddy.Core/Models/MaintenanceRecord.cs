using System;
using System.ComponentModel.DataAnnotations;

namespace BusBuddy.Core.Models
{
    public class MaintenanceRecord
    {
        public int Id { get; set; }
        public int BusId { get; set; }
        public required Bus Bus { get; set; }
        public DateTime Date { get; set; }
        public string Description { get; set; } = string.Empty;
        public double Cost { get; set; }
        public double Odometer { get; set; }
    }
}
