using NUnit.Framework;
using CSharpTestLogger;

namespace CSharpTestLoggers_Tests
{
    public class Tests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void Test1()
        {
            Assert.DoesNotThrow(() => Logger.RecreateConfigAndDB());
            
            string newText = System.Guid.NewGuid().ToString();

            Logger logger = null;
            Assert.DoesNotThrow(() => logger = new Logger());

            Assert.True(logger.fileStorage_Enabled || logger.dbStorage_Enabled);
            Assert.DoesNotThrow(() => logger.WriteLine(newText));

            if (logger.fileStorage_Enabled)
                Assert.AreEqual(newText, logger.GetLastLogLineFromFile());

            if (logger.dbStorage_Enabled)
                Assert.AreEqual(newText, logger.GetLastLogLineFromDB());
        }
    }
}