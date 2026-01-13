namespace PowerApps.CLI.Models;

public class OptionSchema
{
    public int Value { get; set; }
    public string? Label { get; set; }

    public override string ToString()
    {
        return Label ?? Value.ToString();
    }
}
