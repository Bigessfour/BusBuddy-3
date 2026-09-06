using CommunityToolkit.Mvvm.Messaging.Messages;

namespace BusBuddy.WPF.Messages;

/// <summary>
/// Raised when the school destination catalog changes (e.g. Add School dialog saved).
/// Open student forms reload their school list.
/// </summary>
public sealed class SchoolCatalogChangedMessage : ValueChangedMessage<int?>
{
    public SchoolCatalogChangedMessage(int? savedDestinationId = null) : base(savedDestinationId)
    {
    }
}
