using System;
using System.Threading;
using HealthAndAuditShared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RuleTests
{
    [TestClass]
    public class Amount
    {
        MaxAmountOfFailuresRule Rule { get; } = new MaxAmountOfFailuresRule {MaxTimesFailureAllowed = 4,KeepOperationInPileTime = new TimeSpan(0,0,9)};

        [TestMethod]
        public void TestFourOps()
        {
            var op = new OperationResult(OperationResult.OpResult.Failure);
            Assert.IsFalse(Rule.AddAndCheckIfTriggered(op));
            Assert.IsFalse(Rule.AddAndCheckIfTriggered(op));
            Assert.IsFalse(Rule.AddAndCheckIfTriggered(op));
            Assert.IsTrue(Rule.AddAndCheckIfTriggered(op));
        }
        [TestMethod]
        public void TestTtl()
        {
            var op = new OperationResult(OperationResult.OpResult.Failure);
            Assert.IsFalse(Rule.AddAndCheckIfTriggered(op));
            Assert.IsFalse(Rule.AddAndCheckIfTriggered(op));
            Assert.IsFalse(Rule.AddAndCheckIfTriggered(op));
            Thread.Sleep(9000);
            Assert.IsFalse(Rule.AddAndCheckIfTriggered(op));
        }
        [TestMethod]
        public void TestLongFlow()
        {
            var op = new OperationResult(OperationResult.OpResult.Failure);
            Assert.IsFalse(Rule.AddAndCheckIfTriggered(op));
            Assert.IsFalse(Rule.AddAndCheckIfTriggered(op));
            Assert.IsFalse(Rule.AddAndCheckIfTriggered(op));
            Assert.IsTrue(Rule.AddAndCheckIfTriggered(op));

            Assert.IsFalse(Rule.AddAndCheckIfTriggered(op));
            Assert.IsFalse(Rule.AddAndCheckIfTriggered(op));
            Assert.IsFalse(Rule.AddAndCheckIfTriggered(op));
            Assert.IsTrue(Rule.AddAndCheckIfTriggered(op));

            Assert.IsFalse(Rule.AddAndCheckIfTriggered(op));
            Assert.IsFalse(Rule.AddAndCheckIfTriggered(op));
            Thread.Sleep(5000);
            Assert.IsFalse(Rule.AddAndCheckIfTriggered(op));
            Assert.IsTrue(Rule.AddAndCheckIfTriggered(op));


            Assert.IsFalse(Rule.AddAndCheckIfTriggered(op));
            Assert.IsFalse(Rule.AddAndCheckIfTriggered(op));
            Thread.Sleep(5000);
            Assert.IsFalse(Rule.AddAndCheckIfTriggered(op));
            Thread.Sleep(4000);
            Assert.IsFalse(Rule.AddAndCheckIfTriggered(op));
            Assert.IsFalse(Rule.AddAndCheckIfTriggered(op));
            Assert.IsTrue(Rule.AddAndCheckIfTriggered(op));
        }
    }
}
