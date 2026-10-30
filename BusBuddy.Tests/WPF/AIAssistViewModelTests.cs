using System.Collections.ObjectModel;
using System.Threading.Tasks;
using BusBuddy.WPF.Services;
using BusBuddy.WPF.ViewModels.AIAssist;
using NUnit.Framework;
using Syncfusion.UI.Xaml.Chat;

namespace BusBuddy.Tests.WPF;

[TestFixture]
[Category("AIAssist")]
public class AIAssistViewModelTests
{
    private sealed class FakeChat : IAiChatService
    {
        public bool Available { get; set; } = true;
        public string Response { get; set; } = "fake ollama answer about routes";

        public Task InitializeAsync() => Task.CompletedTask;

        public Task<bool> IsAvailableAsync() => Task.FromResult(Available);

        public Task<string> GetResponseAsync(string userMessage) => Task.FromResult(Response);
    }

    [Test]
    public async Task UserMessage_WithOllamaAvailable_AddsBusBuddyAiReply()
    {
        var chat = new FakeChat();
        var vm = new AIAssistViewModel(chat);

        vm.Messages.Add(new TextMessage
        {
            Author = vm.CurrentUser,
            DateTime = DateTime.Now,
            Text = "How do I generate routes?"
        });

        await WaitForAsync(() => vm.Messages.Count >= 3 && !vm.ShowTypingIndicator);

        Assert.That(vm.CurrentUser.Name, Is.EqualTo("Clerk"));
        Assert.That(vm.Messages.OfType<TextMessage>().Last().Author?.Name, Is.EqualTo("BusBuddy AI"));
        Assert.That(vm.Messages.OfType<TextMessage>().Last().Text, Does.Contain("fake ollama"));
        Assert.That(vm.Conversations, Is.Not.Empty);
        Assert.That(vm.Conversations[0].AssistItems, Is.Not.Null.And.Count.GreaterThanOrEqualTo(2));
    }

    [Test]
    public async Task UserMessage_WhenOllamaDown_UsesOfflineProjectKnowledge()
    {
        var chat = new FakeChat { Available = false };
        var vm = new AIAssistViewModel(chat);

        vm.Messages.Add(new TextMessage
        {
            Author = vm.CurrentUser,
            DateTime = DateTime.Now,
            Text = "Where is the map code?"
        });

        await WaitForAsync(() => vm.Messages.Count >= 3 && !vm.ShowTypingIndicator);

        var reply = vm.Messages.OfType<TextMessage>().Last().Text ?? string.Empty;
        Assert.That(reply, Does.Contain("MapView").IgnoreCase.Or.Contain("map").IgnoreCase);
        Assert.That(reply, Does.Not.Contain("fake ollama"));
    }

    [Test]
    public void OfflineKnowledge_GenerateRoutes_IsProjectRelevant()
    {
        var answer = BusBuddyProjectKnowledge.AnswerOffline("How do I generate routes?");
        Assert.That(answer, Does.Contain("Generate Routes"));
        Assert.That(answer, Does.Contain("IRouteDeterminationService"));
    }

    private static async Task WaitForAsync(Func<bool> condition, int timeoutMs = 5000)
    {
        var start = Environment.TickCount64;
        while (!condition())
        {
            if (Environment.TickCount64 - start > timeoutMs)
            {
                Assert.Fail("Timed out waiting for AI Assist reply");
            }

            await Task.Delay(25);
        }
    }
}
