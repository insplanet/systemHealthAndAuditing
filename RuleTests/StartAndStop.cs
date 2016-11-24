using System;
using System.Threading;
using HealthAndAuditShared;
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
            var operation = new OperationResult(OperationResult.OpResult.Success);
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
            var startOp = new OperationResult(OperationResult.OpResult.Success);
            startOp.OperationName = StartName;

            var endOp = new OperationResult(OperationResult.OpResult.Success);
            endOp.OperationName = EndName;

            Assert.IsTrue(Rule.AddAndCheckIfTriggered(endOp));



        }

    }
}
