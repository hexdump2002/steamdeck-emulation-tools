using CommandLine;
using SteamDeckEmuTools;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(theme: SystemConsoleTheme.Literate)
    .CreateLogger();

/*List<string> group1 = new List<string>() { "/pepe/manolo/juego.img" };
List<string> group2 = new List<string>() { "/pepe/manolo/juego.bin" };
List<string> group3 = new List<string>() { "/pepe/manolo/juego.ccd" };
List<string> group4 = new List<string>() { "/pepe/manolo/juego.cue" };
List<string> group5 = new List<string>() { "/pepe/manolo/juego.img", "/pepe/manolo/juego.cue" };
List<string> group6 = new List<string>() { "/pepe/manolo/juego.ccd", "/pepe/manolo/juego.cue", "/pepe/manolo/juego.img" };
List<string> group7 = new List<string>() { "/pepe/manolo/juego.ccd", "/pepe/manolo/juego.bin", "/pepe/manolo/juego.img" };
List<string> group8 = new List<string>() { "/pepe/manolo/juego.ccd", "/pepe/manolo/juego.cue","/pepe/manolo/juego.bin", "/pepe/manolo/juego.img" };

List<List<string>> groups = new() { group1, group2, group3, group4, group5, group6, group7, group8 };

var states = CdService.GetGroupStates(groups);*/

Parser.Default.ParseArguments<CommandLineVerbs.Cd2ChdParser, CommandLineVerbs.CdLayoutVerifierParser>(args)
             .MapResult(
                (CommandLineVerbs.Cd2ChdParser opts) => Cd2ChdConverter.Convert(opts), 
                (CommandLineVerbs.CdLayoutVerifierParser opts) => CdLayoutVerifier.Verify(opts),
                errs => 1);

Log.Logger.Information("Done!");