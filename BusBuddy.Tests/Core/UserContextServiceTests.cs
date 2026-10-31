using BusBuddy.Core.Services;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Core")]
public class UserContextServiceTests
{
    [Test]
    public void Constructor_BindsWindowsSessionOperator()
    {
        var service = new UserContextService();

        Assert.That(service.HasOperatorIdentity, Is.True);
        Assert.That(service.CurrentUserId, Is.EqualTo(Environment.UserName).Or.EqualTo("SYSTEM"));
        Assert.That(service.CurrentUserName, Is.Not.Null.And.Not.Empty);
        Assert.That(service.GetCurrentUserForAudit(), Is.EqualTo(service.CurrentUserId));
    }

    [Test]
    public void ClearCurrentUser_UnbindsIdentity_AuditFallsBackToSystem()
    {
        var service = new UserContextService();
        Assume.That(service.HasOperatorIdentity, Is.True);

        service.ClearCurrentUser();

        Assert.That(service.HasOperatorIdentity, Is.False);
        Assert.That(service.CurrentUserId, Is.Empty);
        Assert.That(service.CurrentUserName, Is.Empty);
        Assert.That(service.GetCurrentUserForAudit(), Is.EqualTo("SYSTEM"));
    }

    [Test]
    public void SetCurrentUser_RebindsOperatorIdentity()
    {
        var service = new UserContextService();
        service.ClearCurrentUser();

        service.SetCurrentUser("clerk.smith", "Clerk Smith", "clerk.smith@district.local");

        Assert.That(service.HasOperatorIdentity, Is.True);
        Assert.That(service.CurrentUserId, Is.EqualTo("clerk.smith"));
        Assert.That(service.CurrentUserName, Is.EqualTo("Clerk Smith"));
        Assert.That(service.CurrentUserEmail, Is.EqualTo("clerk.smith@district.local"));
        Assert.That(service.GetCurrentUserForAudit(), Is.EqualTo("clerk.smith"));
    }

    [Test]
    public void SetCurrentUser_NullUserId_Throws()
    {
        var service = new UserContextService();

        Action setNull = () => service.SetCurrentUser(null!, "Name", "a@b.c");
        Assert.That(setNull, Throws.TypeOf<ArgumentNullException>());
    }

    [Test]
    public void IsAuthenticated_IsNotOnInterface()
    {
        // Guard: do not reintroduce fake-auth wording on the public surface.
        Assert.That(typeof(IUserContextService).GetProperty("IsAuthenticated"), Is.Null);
        Assert.That(typeof(IUserContextService).GetProperty("HasOperatorIdentity"), Is.Not.Null);
    }
}
