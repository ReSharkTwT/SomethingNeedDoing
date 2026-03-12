using Microsoft.Extensions.DependencyInjection;
using NLua;
using SomethingNeedDoing.Core.Interfaces;
using SomethingNeedDoing.NativeMacro;
using System.Threading;
using System.Threading.Tasks;

namespace SomethingNeedDoing.LuaMacro.Modules;

public class CommandModule : LuaModuleBase
{
    public override string ModuleName => "Command";

    private MacroParser? _parser;

    private MacroParser Parser
    {
        get
        {
            if (_parser == null)
            {
                var provider = Plugin.Services;

                if (provider == null)
                    throw new InvalidOperationException("Plugin Services are not initialized yet.");

                _parser = provider.GetService<MacroParser>()
                       ?? throw new InvalidOperationException("MacroParser not registered in DI container.");
            }
            return _parser;
        }
    }

    public CommandModule() { }

    [LuaFunction]
    public void Echo(string str) => Svc.Chat.Print(str);

    [LuaFunction]
    public async Task Execute(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            FrameworkLogger.Warning("[CommandModule] Empty command provided.");
            return;
        }

        if (!command.StartsWith("/"))
        {
            command = "/" + command;
        }

        try
        {
            IMacroCommand macroCommand = Parser.ParseLine(command);

            var context = new MacroContext(new TemporaryMacro(command));

            using var cts = new CancellationTokenSource();

            if (macroCommand.RequiresFrameworkThread)
            {
                await Svc.Framework.RunOnTick(
                    () => ExecuteCommandInternal(macroCommand, context, cts.Token),
                    TimeSpan.Zero,  
                    0,           
                    cts.Token      
                );
            }
            else
            {
                await ExecuteCommandInternal(macroCommand, context, cts.Token);
            }
        }
        catch (Exception ex)
        {
            FrameworkLogger.Error($"[CommandModule] Failed to execute command '{command}': {ex}");
            Svc.Chat.PrintError($"[Lua] Command failed: {ex.Message}");
        }
    }

    private async Task ExecuteCommandInternal(IMacroCommand command, MacroContext context, CancellationToken token)
    {
        await command.Execute(context, token);
        FrameworkLogger.Debug($"[CommandModule] Executed: {command.CommandText}");
    }
}
