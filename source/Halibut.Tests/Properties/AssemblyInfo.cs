using System;
using System.Reflection;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Util;
using NUnit.Framework;
using Xunit;

// Information about this assembly is defined by the following attributes. 
// Change them to the values specific to your project.

[assembly: AssemblyTitle("Halibut.Tests")]
[assembly: Parallelizable(ParallelScope.All)]
[assembly: FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[assembly: TestTimeout]
[assembly: CustomLevelOfParallelism]
[assembly: TestFramework("Halibut.Tests.Util." + nameof(ParallelTestFramework), "Halibut.Tests")]
[assembly: CollectionBehavior(MaxParallelThreads = 2)]