using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BusBuddy.WPF.Models
{
    /// <summary>
    /// Represents a chat message in the XAI chat interface
    /// </summary>
    public class ChatMessage : INotifyPropertyChanged
    {
        private string _message = string.Empty;
        private ChatMessageType _messageType = ChatMessageType.User;
        private DateTime _timestamp = DateTime.Now;
        private bool _isDelivered;
        private bool _isRead;

        /// <summary>
        /// The message content
        /// </summary>
        public string Message
        {
            get => _message;
            set
            {
                _message = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// The type of message (User, AI, System)
        /// </summary>
        public ChatMessageType MessageType
        {
            get => _messageType;
            set
            {
                _messageType = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// When the message was created
        /// </summary>
        public DateTime Timestamp
        {
            get => _timestamp;
            set
            {
                _timestamp = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Whether the message has been delivered
        /// </summary>
        public bool IsDelivered
        {
            get => _isDelivered;
            set
            {
                _isDelivered = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Whether the message has been read
        /// </summary>
        public bool IsRead
        {
            get => _isRead;
            set
            {
                _isRead = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Types of chat messages
    /// </summary>
    public enum ChatMessageType
    {
        /// <summary>
        /// Message from the user
        /// </summary>
        User,

        /// <summary>
        /// Message from the AI assistant
        /// </summary>
        AI,

        /// <summary>
        /// System message (status updates, notifications)
        /// </summary>
        System
    }
}
