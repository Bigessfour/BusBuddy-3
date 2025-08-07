using System;
using System.ComponentModel.DataAnnotations;

namespace BusBuddy.Core.Models
{
    public class FuelRecord
    {
        public int Id { get; set; }
        public int BusId { get; set; }
        public required Bus Bus { get; set; }
        public DateTime Date { get; set; }
        public double Gallons { get; set; }
        public double Cost { get; set; }
        public double Odometer { get; set; }
    }
}
