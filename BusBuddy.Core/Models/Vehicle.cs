using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel;

namespace BusBuddy.Core.Models;

/// <summary>
/// Represents a vehicle in the school bus fleet
/// </summary>
// DEPRECATED: Vehicle model is no longer used. All properties and logic have been migrated to Bus.cs.
// Please use Bus for all fleet operations. This file is retained for migration reference only and will be removed in future releases.
//
// Eradication Report: Vehicle model fully merged with Bus. All data and methods now reside in Bus.cs. No further changes should be made here.
