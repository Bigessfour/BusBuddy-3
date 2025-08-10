using CommunityToolkit.Mvvm.Messaging.Messages;

namespace BusBuddy.WPF.Messages
{
    /// <summary>
    /// Message published when a student has been successfully saved.
    /// Carries the saved student instance as payload.
    /// </summary>
    public sealed class StudentSavedMessage : ValueChangedMessage<BusBuddy.Core.Models.Student>
    {
        public StudentSavedMessage(BusBuddy.Core.Models.Student value) : base(value) { }
    }
}
