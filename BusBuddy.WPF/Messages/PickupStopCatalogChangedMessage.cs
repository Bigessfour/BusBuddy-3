using CommunityToolkit.Mvvm.Messaging.Messages;

namespace BusBuddy.WPF.Messages;

/// <summary>
/// Raised when the pickup-stop catalog changes (e.g. Add Pickup Stop dialog saved).
/// Open student forms reload their pickup-stop list.
/// </summary>
public sealed class PickupStopCatalogChangedMessage : ValueChangedMessage<int?>
{
    public PickupStopCatalogChangedMessage(int? savedPickupStopId = null) : base(savedPickupStopId)
    {
    }
}
