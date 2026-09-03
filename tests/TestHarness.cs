using System;

namespace HeroLoadoutFixer.Tests
{
    public static class Check
    {
        public static int Failures;
        public static int Passes;

        public static void Equal(int expected, int actual, string what)
        {
            if (expected == actual) { Passes++; return; }
            Failures++;
            Console.WriteLine("FAIL " + what + ": expected " + expected + ", got " + actual);
        }

        public static void Equal(string expected, string actual, string what)
        {
            if (expected == actual) { Passes++; return; }
            Failures++;
            Console.WriteLine("FAIL " + what + ": expected '" + expected + "', got '" + actual + "'");
        }

        public static void True(bool condition, string what)
        {
            if (condition) { Passes++; return; }
            Failures++;
            Console.WriteLine("FAIL " + what + ": expected true");
        }

        public static void False(bool condition, string what)
        {
            if (!condition) { Passes++; return; }
            Failures++;
            Console.WriteLine("FAIL " + what + ": expected false");
        }
    }

    public static class TestHarness
    {
        public static int Main()
        {
            SmokeTests.RunAll();
            SkillProfileTests.RunAll();

            Console.WriteLine();
            Console.WriteLine(Check.Passes + " passed, " + Check.Failures + " failed");
            return Check.Failures;
        }
    }

    public static class SmokeTests
    {
        public static void RunAll()
        {
            Check.Equal(3, (int)HeroLoadoutFixer.Core.SkillKind.Bow, "SkillKind.Bow ordinal");
        }
    }
}
