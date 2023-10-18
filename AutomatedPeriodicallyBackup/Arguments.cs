using CommandLine;

//Pay attention to boolean flags (called Switches)
//https://github.com/commandlineparser/commandline/wiki/CommandLine-Grammar#switch-option
public class Arguments
{
    [Option(Required = false, Default = "settings.json", HelpText = "Settings JSON filename")]
    public string SettingsFilename { get; set; } = "";
}
