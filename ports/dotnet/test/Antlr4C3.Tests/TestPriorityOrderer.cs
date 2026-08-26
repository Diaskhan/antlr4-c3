using System;
using System.Collections.Generic;
using System.Linq;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Antlr4C3.Tests
{
    /// <summary>
    /// Mirrors JUnit's @TestMethodOrder(OrderAnnotation) so that tests relying on the
    /// shared static follow-sets cache (as in the Java suite) run in a deterministic order.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class TestOrderAttribute : Attribute
    {
        public int Order { get; }
        public TestOrderAttribute(int order) => Order = order;
    }

    public sealed class TestPriorityOrderer : ITestCaseOrderer
    {
        public IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases)
            where TTestCase : ITestCase
        {
            int GetOrder(TTestCase tc)
            {
                var attr = tc.TestMethod.Method
                    .GetCustomAttributes(typeof(TestOrderAttribute).AssemblyQualifiedName)
                    .FirstOrDefault();
                return attr?.GetNamedArgument<int>(nameof(TestOrderAttribute.Order)) ?? int.MaxValue;
            }

            return testCases.OrderBy(GetOrder);
        }
    }
}
