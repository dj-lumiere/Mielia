using TypeModel.Enums;

namespace Mielia;

/// <summary>The <c>mielia</c> command line.</summary>
internal static class MieliaCli
{
    /// <summary>Registers the Mielia front end and runs the command line.</summary>
    public static int Main(string[] args)
    {
        MieliaFrontend.Register();
        return Builder.Execution.Program.Run(args: args, cliLanguage: Language.Mielia);
    }
}
