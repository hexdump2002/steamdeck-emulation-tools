using CommandLine;
using SteamDeckEmuTools;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(theme: SystemConsoleTheme.Literate)
    .CreateLogger();


Parser.Default.ParseArguments<CommandLineVerbs.Cd2ChdParser, CommandLineVerbs.CdLayoutVerifierParser>(args)
             .MapResult(
                (CommandLineVerbs.Cd2ChdParser opts) => Cd2ChdConverter.Convert(opts), 
                (CommandLineVerbs.CdLayoutVerifierParser opts) => CdLayoutVerifier.Verify(opts),
                errs => 1);