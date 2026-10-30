using System.Collections.ObjectModel;
using System.Collections.Specialized;
using BusBuddy.WPF.Services;
using Serilog;
using Syncfusion.UI.Xaml.Chat;

namespace BusBuddy.WPF.ViewModels.AIAssist
{
    /// <summary>
    /// Syncfusion SfAIAssistView ViewModel — Ollama chat + offline project knowledge.
    /// Docs: https://help.syncfusion.com/wpf/ai-assistview/getting-started
    /// Conversation history (2026 Vol 3): Conversations + ShowNavigationView.
    /// </summary>
    public class AIAssistViewModel : BaseViewModel, IDisposable
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<AIAssistViewModel>();
        private static readonly Author BotAuthor = new() { Name = "BusBuddy AI" };

        private readonly IAiChatService? _chatService;
        private readonly Queue<string> _pendingUserMessages = new();
        private Author _currentUser = new() { Name = "Clerk" };
        private bool _showTypingIndicator;
        private ObservableCollection<object> _messages = new();
        private ObservableCollection<string> _suggestions = new();
        private ObservableCollection<AssistConversationItem> _conversations = new();
        private AssistConversationItem? _activeConversation;
        private int _replyDepth;
        private bool _suppressReply;
        private bool _disposed;

        public Author CurrentUser
        {
            get => _currentUser;
            set => SetProperty(ref _currentUser, value);
        }

        public ObservableCollection<object> Messages
        {
            get => _messages;
            set
            {
                if (ReferenceEquals(_messages, value))
                {
                    return;
                }

                if (_messages != null)
                {
                    _messages.CollectionChanged -= OnMessagesCollectionChanged;
                }

                SetProperty(ref _messages, value);
                if (_messages != null)
                {
                    _messages.CollectionChanged += OnMessagesCollectionChanged;
                }
            }
        }

        public ObservableCollection<string> Suggestions
        {
            get => _suggestions;
            set => SetProperty(ref _suggestions, value);
        }

        public bool ShowTypingIndicator
        {
            get => _showTypingIndicator;
            set => SetProperty(ref _showTypingIndicator, value);
        }

        /// <summary>2026 Vol 3 conversation history source for SfAIAssistView.Conversations.</summary>
        public ObservableCollection<AssistConversationItem> Conversations
        {
            get => _conversations;
            set => SetProperty(ref _conversations, value);
        }

        /// <summary>Designer / emergency fallback without DI.</summary>
        public AIAssistViewModel()
            : this(chatService: null)
        {
        }

        public AIAssistViewModel(IAiChatService? chatService)
        {
            _chatService = chatService;
            CurrentUser = new Author { Name = "Clerk" };
            Conversations = new ObservableCollection<AssistConversationItem>();
            Suggestions = new ObservableCollection<string>
            {
                "How do I generate routes?",
                "Show student assignment flow",
                "Where is the map code?",
                "What is the BusBuddy architecture?",
                "Where are the project docs?"
            };

            Messages = new ObservableCollection<object>
            {
                new TextMessage
                {
                    Author = BotAuthor,
                    DateTime = DateTime.Now,
                    Text = "Ask about BusBuddy routes, students, map, architecture, or docs. Answers use local Ollama when available, otherwise project knowledge."
                }
            };

            // Warm context cache off the UI thread so the first question does not hitch.
            _ = Task.Run(() =>
            {
                try
                {
                    BusBuddyProjectKnowledge.BuildContextSnippet();
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "AI Assist context warm-up failed");
                }
            });

            Log.Debug("AIAssistViewModel ready (chatService={HasChat})", _chatService != null);
        }

        /// <summary>
        /// Vol 3 history: load a selected conversation into Messages without triggering AI replies.
        /// </summary>
        public void SelectConversation(AssistConversationItem? item)
        {
            if (item == null)
            {
                return;
            }

            _suppressReply = true;
            try
            {
                SaveConversationToHistory();
                _activeConversation = item;
                var items = item.AssistItems ?? new ObservableCollection<object>();
                Messages = new ObservableCollection<object>(items);
            }
            finally
            {
                _suppressReply = false;
            }
        }

        /// <summary>Start a fresh chat while keeping archived Conversations.</summary>
        public void StartNewConversation()
        {
            _suppressReply = true;
            try
            {
                SaveConversationToHistory();
                _activeConversation = null;
                Messages = new ObservableCollection<object>
                {
                    new TextMessage
                    {
                        Author = BotAuthor,
                        DateTime = DateTime.Now,
                        Text = "New chat. Ask about routes, students, map, architecture, or docs."
                    }
                };
            }
            finally
            {
                _suppressReply = false;
            }
        }

        private async void OnMessagesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_suppressReply)
            {
                return;
            }

            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                if (Messages.Count == 0)
                {
                    _activeConversation = null;
                }

                return;
            }

            if (e.Action != NotifyCollectionChangedAction.Add || e.NewItems == null)
            {
                return;
            }

            foreach (var added in e.NewItems)
            {
                if (added is not ITextMessage textMessage)
                {
                    continue;
                }

                if (!string.Equals(textMessage.Author?.Name, CurrentUser.Name, StringComparison.Ordinal))
                {
                    continue;
                }

                var text = textMessage.Text ?? string.Empty;
                if (_replyDepth > 0)
                {
                    _pendingUserMessages.Enqueue(text);
                    Log.Debug("Queued AI Assist user message while a reply is in flight");
                    continue;
                }

                await ReplyToUserAsync(text).ConfigureAwait(true);
            }
        }

        private async Task ReplyToUserAsync(string userText)
        {
            ShowTypingIndicator = true;
            _replyDepth++;
            try
            {
                EnsureActiveConversation(userText);
                var answer = await BuildAnswerAsync(userText).ConfigureAwait(true);
                Messages.Add(new TextMessage
                {
                    Author = BotAuthor,
                    DateTime = DateTime.Now,
                    Text = answer
                });
                SaveConversationToHistory();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "AI Assist reply failed for user message");
                Messages.Add(new TextMessage
                {
                    Author = BotAuthor,
                    DateTime = DateTime.Now,
                    Text = "Sorry — something went wrong answering that. Check Serilog logs for details."
                });
            }
            finally
            {
                _replyDepth--;
                ShowTypingIndicator = false;
                if (_replyDepth == 0 && _pendingUserMessages.Count > 0)
                {
                    var next = _pendingUserMessages.Dequeue();
                    await ReplyToUserAsync(next).ConfigureAwait(true);
                }
            }
        }

        private async Task<string> BuildAnswerAsync(string userText)
        {
            var context = await Task.Run(() => BusBuddyProjectKnowledge.BuildContextSnippet())
                .ConfigureAwait(true);
            var enriched =
                "Use the following BusBuddy project context. Prefer concrete file paths and clerk hops when relevant.\n\n"
                + context
                + "\n\nUser question: "
                + userText;

            if (_chatService == null)
            {
                Log.Warning("IAiChatService not injected; using offline project knowledge");
                return BusBuddyProjectKnowledge.AnswerOffline(userText);
            }

            try
            {
                var available = await _chatService.IsAvailableAsync().ConfigureAwait(true);
                if (!available)
                {
                    Log.Warning("Ollama unavailable for AI Assist; using offline project knowledge");
                    return BusBuddyProjectKnowledge.AnswerOffline(userText);
                }

                var response = await _chatService.GetResponseAsync(enriched).ConfigureAwait(true);
                if (string.IsNullOrWhiteSpace(response)
                    || response.Contains("Ollama) is not available", StringComparison.OrdinalIgnoreCase))
                {
                    Log.Warning("Ollama returned unavailable/empty response; using offline project knowledge");
                    return BusBuddyProjectKnowledge.AnswerOffline(userText);
                }

                return response.Trim();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ollama chat failed in AI Assist; falling back to offline knowledge");
                return BusBuddyProjectKnowledge.AnswerOffline(userText);
            }
        }

        private void EnsureActiveConversation(string firstUserText)
        {
            if (_activeConversation != null)
            {
                return;
            }

            var title = string.IsNullOrWhiteSpace(firstUserText)
                ? "New chat"
                : (firstUserText.Length <= 48 ? firstUserText : firstUserText[..45] + "…");

            _activeConversation = new AssistConversationItem
            {
                Title = title,
                DateTime = DateTime.Now,
                AssistItems = new ObservableCollection<object>(Messages)
            };
            Conversations.Insert(0, _activeConversation);
        }

        private void SaveConversationToHistory()
        {
            if (_activeConversation == null)
            {
                return;
            }

            _activeConversation.AssistItems = new ObservableCollection<object>(Messages);
            _activeConversation.DateTime = DateTime.Now;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_messages != null)
            {
                _messages.CollectionChanged -= OnMessagesCollectionChanged;
            }

            GC.SuppressFinalize(this);
        }
    }
}
