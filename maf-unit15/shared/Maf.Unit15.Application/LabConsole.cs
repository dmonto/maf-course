namespace Maf.Unit15.Application;

// Didactic console trace, same [KIND] text style as Units 14 and 16. Never starts a line with "RESULT ".
public static class LabConsole
{
    public static void Write(string kind, string text) => Console.WriteLine($"[{kind}] {text}");
    public static void Check(string name, bool passed)
    {
        Write("CHECK", $"{(passed ? "PASS" : "FAIL")} | {name}");
        if (!passed) throw new InvalidOperationException(name);
    }
}
