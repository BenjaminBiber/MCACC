using Mcacc.Core.Model;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Model;

public class AccountTests
{
    [Fact]
    public void CreateDefault_returns_the_standard_account()
    {
        var account = Account.CreateDefault();

        account.ShouldBe(new Account("standard", "Standard", "#D97757", IsDefault: true));
        Account.DefaultId.ShouldBe("standard");
        Account.DefaultName.ShouldBe("Standard");
    }
}
