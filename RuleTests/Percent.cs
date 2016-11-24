using System;
using System.Threading;
using HealthAndAuditShared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RuleTests
{
    [TestClass]
    public class Percent
    {
        FailurePercentRule Rule { get; } = new FailurePercentRule { MaxFailurePercent = 50, KeepOperationInPileTime = new TimeSpan(0, 0, 9) };

        [TestMethod]
        public void TestAllFail()
        {
            var fail = new OperationResult(OperationResult.OpResult.Failure);
            Rule.MinimumAmountOfOperationsBeforeRuleCanBeTriggered = 4;
            Assert.IsFalse(Rule.AddAndCheckIfTriggered(fail));
            Assert.IsFalse(Rule.AddAndCheckIfTriggered(fail));
            Assert.IsFalse(Rule.AddAndCheckIfTriggered(fail));
            Assert.IsTrue(Rule.AddAndCheckIfTriggered(fail));
        }
        [TestMethod]
        public void TestExactPercentFail()
        {
            var fail = new OperationResult(OperationResult.OpResult.Failure);
            var success = new OperationResult(OperationResult.OpResult.Success);
            Rule.MinimumAmountOfOperationsBeforeRuleCanBeTriggered = 4;
            Assert.IsFalse(Rule.AddAndCheckIfTriggered(fail));
            Assert.IsFalse(Rule.AddAndCheckIfTriggered(fail));
            Assert.IsFalse(Rule.AddAndCheckIfTriggered(success));
            Assert.IsTrue(Rule.AddAndCheckIfTriggered(success));
        }
        [TestMethod]
        public void TesPercentFailSuccess()
        {
            var fail = new OperationResult(OperationResult.OpResult.Failure);
            var success = new OperationResult(OperationResult.OpResult.Success);
            Rule.MinimumAmountOfOperationsBeforeRuleCanBeTriggered = 4;
            Assert.IsFalse(Rule.AddAndCheckIfTriggered(fail));
            Assert.IsFalse(Rule.AddAndCheckIfTriggered(success));
            Assert.IsFalse(Rule.AddAndCheckIfTriggered(success));
            Assert.IsFalse(Rule.AddAndCheckIfTriggered(success));
        }
        [TestMethod]
        public void TestLongFlow()
        {
            var fail = new OperationResult(OperationResult.OpResult.Failure);
            var success = new OperationResult(OperationResult.OpResult.Success);

            Rule.MinimumAmountOfOperationsBeforeRuleCanBeTriggered = 4;

            Assert.IsFalse(Rule.AddAndCheckIfTriggered(fail));
            Thread.Sleep(5000);
            Assert.IsFalse(Rule.AddAndCheckIfTriggered(success));
            Assert.IsFalse(Rule.AddAndCheckIfTriggered(success));
            Assert.IsFalse(Rule.AddAndCheckIfTriggered(success));
            Thread.Sleep(4000);
            Assert.IsFalse(Rule.AddAndCheckIfTriggered(fail));
            Assert.IsFalse(Rule.AddAndCheckIfTriggered(fail));
            Assert.IsTrue(Rule.AddAndCheckIfTriggered(fail));
        }
    }
}
