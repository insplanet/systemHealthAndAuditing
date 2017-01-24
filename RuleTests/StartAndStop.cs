using System;
using System.Threading;
using SystemHealthExternalInterface;
using HealthAndAuditShared;
using HealthAndAuditShared.Fakes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RuleTests
{
    //todo
    [TestClass]
    public class StartAndStop
    {
        TimeBetweenOperations Rule { get; } = new TimeBetweenOperations { KeepOperationInPileTime = new TimeSpan(0, 0, 9) };
        private string StartName = "Start";
        private string EndName = "End";

        [TestMethod]
        public void TestWithOneOperation()
        {
            Rule.OperationName = "one";
            var operation = new SystemEvent(SystemEvent.OperationResult.Success);
            operation.OperationName = "one";
            Assert.IsFalse(Rule.AddAndCheckIfTriggered(operation));
            Assert.IsFalse(Rule.AddAndCheckIfTriggered(operation));
            Assert.IsFalse(Rule.AddAndCheckIfTriggered(operation));
            Thread.Sleep(9000);
            Assert.IsTrue(Rule.AddAndCheckIfTriggered(operation));
            Assert.IsFalse(Rule.AddAndCheckIfTriggered(operation));
        }

        [TestMethod]
        public void TestWithStartAndStop()
        {
            Rule.OperationName = null;
            Rule.StartOperationName = StartName;
            Rule.EndOperationName = EndName;
            var startOp = new SystemEvent(SystemEvent.OperationResult.Success);
            startOp.OperationName = StartName;

            var endOp = new SystemEvent(SystemEvent.OperationResult.Success);
            endOp.OperationName = EndName;

            Assert.IsTrue(Rule.AddAndCheckIfTriggered(endOp));
        }

        [TestMethod]
        public void TestIfLastOperationIsRunnedBeforeFirst()
        {
            Rule.OperationName = null;
            Rule.StartOperationName = StartName;
            Rule.EndOperationName = EndName;
            var endOp = new SystemEvent(SystemEvent.OperationResult.Success);
            endOp.OperationName = EndName;
            Assert.IsTrue(Rule.AddAndCheckIfTriggered(endOp));
        }

        [TestMethod]
        public void TestIfFirstOperationIsRunnedTwoTimesInARow()
        {
            Rule.OperationName = null;
            Rule.StartOperationName = StartName;
            Rule.EndOperationName = EndName;
            var startOp = new SystemEvent(SystemEvent.OperationResult.Success);
            startOp.OperationName = StartName;
            Assert.IsFalse(Rule.AddAndCheckIfTriggered(startOp));
            Assert.IsTrue(Rule.AddAndCheckIfTriggered(startOp));
        }

        [TestMethod]
        public void TestIfTimeBetweenOperationsIsBelowTimeout()
        {
            Rule.OperationName = null;
            Rule.StartOperationName = StartName;
            Rule.EndOperationName = EndName;
            var startOp = new SystemEvent(SystemEvent.OperationResult.Success);
            startOp.OperationName = StartName;
            var endOp = new SystemEvent(SystemEvent.OperationResult.Success);
            endOp.OperationName = EndName;
            Assert.IsFalse(Rule.AddAndCheckIfTriggered(startOp));
            Thread.Sleep(6000);
            Assert.IsFalse(Rule.AddAndCheckIfTriggered(endOp));
            Assert.IsFalse(Rule.AddAndCheckIfTriggered(startOp));
            Thread.Sleep(4500);
            Assert.IsFalse(Rule.AddAndCheckIfTriggered(endOp));
        }

        [TestMethod]
        public void TestIfTimeBetweenOperationsExceedsTimeout()
        {
            Rule.OperationName = null;
            Rule.StartOperationName = StartName;
            Rule.EndOperationName = EndName;
            var startOp = new SystemEvent(SystemEvent.OperationResult.Success);
            startOp.OperationName = StartName;
            var endOp = new SystemEvent(SystemEvent.OperationResult.Success);
            endOp.OperationName = EndName;
            Assert.IsFalse(Rule.AddAndCheckIfTriggered(startOp));
            Thread.Sleep(9000);
            Assert.IsTrue(Rule.AddAndCheckIfTriggered(endOp));
        }

        [TestMethod]
        public void TestIfUpdateIsInvoked()
        {
            Rule.OperationName = "one";
            var operation = new SystemEvent(SystemEvent.OperationResult.Success);
            var fakeObserver = new FakeTimeBetweenOperationsObserver();
            Rule.AttachObserver(fakeObserver);
            Assert.IsFalse(Rule.AddAndCheckIfTriggered(operation));
            Assert.IsFalse(fakeObserver.IsInvoked);
            Thread.Sleep(10000);
            Assert.IsTrue(fakeObserver.IsInvoked);
        }
    }
}
