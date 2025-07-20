using ToSound.Core;

namespace ToSound.Tests
{
    public class ConstructorTests
    {
        private readonly string testDataDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "TestData");

        [Fact]
        public void CreatesInstance_Successsfully()
        {
            MindToSoundEmulator emulator = new();
            Assert.NotNull(emulator);
        }

        [Fact]
        public void CreateMultipleInstances_Successsfully()
        {
            MindToSoundEmulator emulatorA = new();
            MindToSoundEmulator emulatorB = new();

            Assert.NotNull(emulatorA);
            Assert.NotNull(emulatorB);

            Assert.NotSame(emulatorA, emulatorB);
        }

        [Fact]
        public void CreateInstanceWithFile_Successsfully()
        {
            MindToSoundEmulator emulator = new(testDataDirectory + "\\Regular Recording {1} [05-25-2025 1608] (1).csv");

            Assert.NotNull(emulator);
        }

        [Fact]
        public void CreateInstanceWithFileAndOSC_Successsfully()
        {
            MindToSoundEmulator emulator = new(testDataDirectory + "\\Regular Recording {1} [05-25-2025 1608] (1).csv", "127.0.0.1", 55555);

            Assert.NotNull(emulator);
        }
    }

    public class SetFileTests
    {
        private readonly string testDataDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "TestData");
        private MindToSoundEmulator emulator = new();

        [Fact]
        public void FileWithNoStates_Successsfully()
        {
            Assert.NotNull(emulator);

            emulator.SetFile(testDataDirectory + "\\Regular Recording {1} [05-25-2025 1608] (1).csv");

            Assert.Equal(68, emulator.GetFileLength());
            Assert.Equal(123.134328358, Double.Round(emulator.GetTransmissionDelay(), 9));
            Assert.Equal([false, false, false, false, false], emulator.GetAvailablePlaybackStates());
        }

        [Fact]
        public void FileWithAllStates_Successsfully()
        {
            Assert.NotNull(emulator);

            emulator.SetFile(testDataDirectory + "\\All States {1} [03-31-2025] (1).csv");

            Assert.Equal(421, emulator.GetFileLength());
            Assert.Equal(126.190476190, Double.Round(emulator.GetTransmissionDelay(), 9));
            Assert.Equal([true, true, true, true, true], emulator.GetAvailablePlaybackStates());
        }

        [Fact]
        public void FileWithSelectStates_Successsfully()
        {
            Assert.NotNull(emulator);

            emulator.SetFile(testDataDirectory + "\\Select States {1} [03-31-2025] (2).csv");

            Assert.Equal(271, emulator.GetFileLength());
            Assert.Equal(125.925925926, Double.Round(emulator.GetTransmissionDelay(), 9));
            Assert.Equal([true, false, true, true, false], emulator.GetAvailablePlaybackStates());
        }

        [Fact]
        public void FileWithTooFewData_ThrowsException()
        {
            Assert.NotNull(emulator);

            Assert.Throws<Exception>(() => emulator.SetFile(testDataDirectory + "\\Too Short {1} [05-25-2025 1630] (1).csv"));
        }

        [Fact]
        public void FileOpenedByAnotherProcess_ThrowsException()
        {
            Assert.NotNull(emulator);

            // Open the file
            using var fileStream = new FileStream(testDataDirectory + "\\Regular Recording {1} [05-25-2025 1608] (1).csv", FileMode.Open, FileAccess.Read, FileShare.None);

            Assert.Throws<IOException>(() => emulator.SetFile(testDataDirectory + "\\Regular Recording {1} [05-25-2025 1608] (1).csv"));
        }

        [Fact]
        public void FileWithTooSmallTransmissionDelay_SelfFixSuccessful()
        {
            Assert.NotNull(emulator);

            emulator.SetFile(testDataDirectory + "\\Small Delay {1} [05-25-2025 1608] (1).csv");

            Assert.Equal(100.0, emulator.GetTransmissionDelay());
        }

        [Fact]
        public void ChangingFiles_Successsfully()
        {
            Assert.NotNull(emulator);

            // Start with a file with all states
            emulator.SetFile(testDataDirectory + "\\All States {1} [03-31-2025] (1).csv");
            Assert.Equal(421, emulator.GetFileLength());
            Assert.Equal(126.190476190, Double.Round(emulator.GetTransmissionDelay(), 9));
            Assert.Equal([true, true, true, true, true], emulator.GetAvailablePlaybackStates());

            // Change to a file with select states
            emulator.SetFile(testDataDirectory + "\\Select States {1} [03-31-2025] (2).csv");
            Assert.Equal(271, emulator.GetFileLength());
            Assert.Equal(125.925925926, Double.Round(emulator.GetTransmissionDelay(), 9));
            Assert.Equal([true, false, true, true, false], emulator.GetAvailablePlaybackStates());

            // Change to a file with no states
            emulator.SetFile(testDataDirectory + "\\Regular Recording {1} [05-25-2025 1608] (1).csv");
            Assert.Equal(68, emulator.GetFileLength());
            Assert.Equal(123.134328358, Double.Round(emulator.GetTransmissionDelay(), 9));
            Assert.Equal([false, false, false, false, false], emulator.GetAvailablePlaybackStates());
        }
    }
}