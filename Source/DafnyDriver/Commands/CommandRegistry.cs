using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Boogie;

namespace Microsoft.Dafny;

internal interface ParseArgumentResult {
}

record ParseArgumentSuccess(DafnyOptions DafnyOptions) : ParseArgumentResult;
record ParseArgumentFailure(DafnyDriver.CommandLineArgumentsResult ExitResult) : ParseArgumentResult;

static class CommandRegistry {
  private static readonly HashSet<ICommandSpec> Commands = new();

  static void AddCommand(ICommandSpec command) {
    Commands.Add(command);
  }

  static CommandRegistry() {
    AddCommand(new VerifyCommand());
    AddCommand(new RunCommand());
    AddCommand(new BuildCommand());
    AddCommand(new TranslateCommand());
    AddCommand(new TestCommand());
    AddCommand(new GenerateTestsCommand());
    AddCommand(new DeadCodeCommand());

    FileArgument = new Argument<FileInfo>("file", "input file");
  }

  public static Argument<FileInfo> FileArgument { get; }

  [CanBeNull]
  public static ParseArgumentResult Create(string[] arguments) {

    bool allowHidden = true;
    if (arguments.Length != 0) {
      var first = arguments[0];
      if (first == "--dev") {
        allowHidden = false;
        arguments = arguments.Skip(1).ToArray();
      }
    }

    var wasInvoked = false;
    string optionFailure = null;
    var dafnyOptions = new DafnyOptions();
    var optionValues = new Dictionary<IOptionSpec, object>();
    var options = new Options(optionValues);
    dafnyOptions.ShowEnv = ExecutionEngineOptions.ShowEnvironment.Never;
    dafnyOptions.Options = options;

    var optionToSpec = Commands.SelectMany(c => c.Options).Distinct().ToDictionary(o => {
      var result = o.ToOption;
      if (!allowHidden) {
        result.IsHidden = false;
      }
      return result;
    }, o => o);
    var specToOption = optionToSpec.ToDictionary(o => o.Value, o => o.Key);
    var commandToSpec = Commands.ToDictionary(c => {
      var result = c.Create();
      foreach (var option in c.Options) {
        result.AddOption(specToOption[option]);
      }
      return result;
    }, c => c);
    foreach (var command in commandToSpec.Keys) {
      command.SetHandler(CommandHandler);
    }

    var commandNames = commandToSpec.Keys.Select(c => c.Name).ToHashSet();
    if (arguments.Length != 0) {
      var first = arguments[0];
      if (first != "--dev" && first != "--version" && first != "-h" && first != "--help" && !commandNames.Contains(first)) {
        var oldOptions = new DafnyOptions();
        if (oldOptions.Parse(arguments)) {
          return new ParseArgumentSuccess(oldOptions);
        }

        return new ParseArgumentFailure(DafnyDriver.CommandLineArgumentsResult.PREPROCESSING_ERROR);
      }
    }

    var rootCommand = new RootCommand("The Dafny CLI enables working with Dafny, a verification-aware programming language. Use 'dafny /help' to see help for a previous CLI format.");
    foreach (var command in commandToSpec.Keys) {
      rootCommand.AddCommand(command);
    }

    void CommandHandler(InvocationContext context) {
      wasInvoked = true;
      var command = context.ParseResult.CommandResult.Command;
      var commandSpec = commandToSpec[command];

      var singleFile = context.ParseResult.GetValueForArgument(FileArgument);
      if (singleFile != null) {
        dafnyOptions.AddFile(singleFile.FullName);
      }
      var files = context.ParseResult.GetValueForArgument(ICommandSpec.FilesArgument);
      if (files != null) {
        foreach (var file in files) {
          dafnyOptions.AddFile(file.FullName);
        }
      }

      foreach (var option in command.Options) {
        if (!context.ParseResult.HasOption(option)) {
          continue;
        }


        var optionSpec = optionToSpec[option];
        var value = context.ParseResult.GetValueForOption(option);
        options.OptionArguments[optionSpec] = value;
        optionFailure ??= optionSpec.PostProcess(dafnyOptions);
        if (optionFailure != null) {
          if (optionFailure == "") {
            optionFailure = $"Parsing option {option.Name} failed.";
          }
          break;
        }
      }

      dafnyOptions.ApplyDefaultOptionsWithoutSettingsDefault();
      commandSpec.PostProcess(dafnyOptions, options, context);
    }

#pragma warning disable VSTHRD002
    var exitCode = rootCommand.InvokeAsync(arguments).Result;
#pragma warning restore VSTHRD002
    if (optionFailure != null) {
      Console.WriteLine(optionFailure);
      return new ParseArgumentFailure(DafnyDriver.CommandLineArgumentsResult.PREPROCESSING_ERROR);
    }
    if (!wasInvoked) {
      return new ParseArgumentFailure(DafnyDriver.CommandLineArgumentsResult.OK_EXIT_EARLY);
    }
    if (exitCode == 0) {
      return new ParseArgumentSuccess(dafnyOptions);
    }

    return new ParseArgumentFailure(DafnyDriver.CommandLineArgumentsResult.PREPROCESSING_ERROR);
  }
}
