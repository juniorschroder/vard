using System;
using FluentAssertions;
using Vard.Abstractions;
using Vard.Builders;
using Xunit;

namespace Vard.Tests
{
    public class PolicyBuilderTests
    {
        // API-06: VardPolicy entry point e fluência do builder
        [Fact]
        public void VardPolicy_Handle_ReturnsIPolicyBuilder()
        {
            IPolicyBuilder builder = VardPolicy.Handle<InvalidOperationException>();
            builder.Should().NotBeNull();
        }

        [Fact]
        public void Handle_Build_ReturnsIPolicy()
        {
            IPolicy policy = VardPolicy.Handle<InvalidOperationException>().Build();
            policy.Should().NotBeNull();
        }

        [Fact]
        public void Handle_Or_Build_FluentChainReturnsIPolicy()
        {
            IPolicy policy = VardPolicy
                .Handle<InvalidOperationException>()
                .Or<TimeoutException>()
                .Build();
            policy.Should().NotBeNull();
        }

        [Fact]
        public void HandleResult_Build_ReturnsIPolicy()
        {
            IPolicy policy = VardPolicy
                .Handle<Exception>()
                .HandleResult<int>(r => r > 400)
                .Build();
            policy.Should().NotBeNull();
        }

        // API-05: IPolicyBuilder é interface (extensível via extension methods)
        [Fact]
        public void IPolicyBuilder_IsInterface()
        {
            typeof(IPolicyBuilder).IsInterface.Should().BeTrue();
        }

        // PolicyWrap execute throws NotSupportedException (D-16)
        [Fact]
        public void PolicyWrap_Execute_ThrowsNotSupportedException()
        {
            IPolicy policy = VardPolicy.Handle<Exception>().Build();
            Action act = () => policy.Execute<int>(() => 42);
            act.Should().Throw<NotSupportedException>();
        }

        // InternalsVisibleTo: se este teste compila, InternalsVisibleTo está correto
        [Fact]
        public void InternalsVisibleTo_AllowsAccessToInternalTypes()
        {
            // HandleCondition é internal — se isso compila, InternalsVisibleTo está configurado
            var condition = new HandleCondition();
            condition.Should().NotBeNull();
        }
    }
}
