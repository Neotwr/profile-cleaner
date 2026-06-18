using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Security.Principal;
using Spectre.Console;

namespace ProfileCleaner
{
    class Program
    {
        private static readonly string[] SidsDoSistema = { "S-1-5-18", "S-1-5-19", "S-1-5-20" }; // Usuários do sistema, padrão do Windows
        private static readonly string[] PerfilsLocais = { "Administrador", "Support" }; // Espaço para os usuários padrões que todos os computadores num ambiente corporativo (provavalmente) possui
        private static readonly string[] ModosDeExecucao =
        {
            "Manual (Selecionar da lista)",
            "Automático (Apagar TODOS os qualificados)",
            "Sair"
        };

        static void Main()
        {
            // Define o título da janela do console
            Console.Title = "Profile Cleaner";

            // Ajusta o encoding para garantir caracteres especiais como acentuação e ícones no console
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            // 1. Verificar privilégios de administrador
            if (!IsAdministrator())
            {
                AnsiConsole.MarkupLine("[yellow]Executando sem privilégios de administrador. Solicitando elevação...[/]");
                Elevate();
                return;
            }

            // Exibir cabeçalho estilizado
            AnsiConsole.Write(new Rule("[yellow]Gerenciador de Perfis de Usuário do Windows[/]") { Justification = Justify.Left });
            AnsiConsole.WriteLine();

            // 2. Buscar e filtrar os perfis
            List<ProfileInfo> profiles = new();

            AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .Start("[cyan]Buscando perfis no sistema (WMI)...[/]", ctx =>
                {
                    profiles = ObterPerfis();
                });

            if (profiles.Count == 0)
            {
                AnsiConsole.MarkupLine("[red]Nenhum perfil de usuário foi retornado pelo WMI.[/]");
                AguardarSaida();
                return;
            }

            // Selecionar os perfis que o usuário deseja proteger (não serão apagados)
            // Filtramos apenas perfis não carregados (ativos já são protegidos automaticamente)
            var perfisParaProtegerPrompt = profiles.Where(p => !p.IsLoaded).ToList();
            if (perfisParaProtegerPrompt.Count > 0)
            {
                var perfisProtegidos = AnsiConsole.Prompt(
                    new MultiSelectionPrompt<ProfileInfo>()
                        .Title("[bold yellow]Selecione os perfis que deseja PROTEGER (NÃO serão excluídos):[/]")
                        .NotRequired()
                        .PageSize(10)
                        .MoreChoicesText("[grey](Mova para cima e para baixo para ver mais perfis)[/]")
                        .InstructionsText(
                            "[grey](Pressione [blue]<Espaço>[/] para marcar, [green]<Enter>[/] para confirmar)[/]")
                        .AddChoices(perfisParaProtegerPrompt)
                        .UseConverter(p => $"{p.Name} ({p.Path})"));

                foreach (var p in perfisProtegidos)
                {
                    p.ShouldIgnore = true;
                    p.IgnoreReason = "Protegido pelo Operador";
                }
            }

            // 3. Exibir tabela formatada com todos os perfis
            ExibirTabelaPerfis(profiles);

            // Filtrar apenas os perfis que podem ser excluídos
            var perfisExcluiveis = profiles.Where(p => !p.ShouldIgnore).ToList();

            if (perfisExcluiveis.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]Nenhum perfil qualificado para exclusão foi encontrado (todos os perfis estão protegidos ou em uso).[/]");
                AguardarSaida();
                return;
            }

            // 4. Selecionar o modo de execução
            var modo = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold yellow]Escolha o modo de execução:[/]")
                    .PageSize(10)
                    .AddChoices(ModosDeExecucao));

            if (modo == ModosDeExecucao[2]) // Sair
            {
                AnsiConsole.MarkupLine("[blue]Operação cancelada pelo usuário.[/]");
                return;
            }

            List<ProfileInfo> perfisParaApagar = new();

            if (modo == ModosDeExecucao[1]) // Automático (Apagar TODOS os qualificados)
            {
                AnsiConsole.MarkupLine("\n[bold red]ATENÇÃO:[/] Os seguintes perfis serão apagados automaticamente:");
                foreach (var p in perfisExcluiveis)
                {
                    AnsiConsole.MarkupLine($" - [white]{p.Name}[/] ({p.Path})");
                }
                AnsiConsole.WriteLine();

                if (AnsiConsole.Confirm("[red]Tem certeza absoluta que deseja prosseguir com a exclusão de TODOS esses perfis?[/]"))
                {
                    perfisParaApagar = perfisExcluiveis;
                }
                else
                {
                    AnsiConsole.MarkupLine("[yellow]Operação abortada.[/]");
                    AguardarSaida();
                    return;
                }
            }
            else // Manual (Selecionar da lista)
            {
                var selecao = AnsiConsole.Prompt(
                    new MultiSelectionPrompt<ProfileInfo>()
                        .Title("Use [blue]<Espaço>[/] para selecionar/desmarcar e [green]<Enter>[/] para confirmar:")
                        .NotRequired()
                        .PageSize(10)
                        .MoreChoicesText("[grey](Mova para cima e para baixo para ver mais perfis)[/]")
                        .InstructionsText(
                            "[grey](Pressione [blue]<Espaço>[/] para selecionar, [green]<Enter>[/] para confirmar)[/]")
                        .AddChoices(perfisExcluiveis)
                        .UseConverter(p => $"{p.Name} ({p.Path})"));

                if (selecao.Count == 0)
                {
                    AnsiConsole.MarkupLine("[yellow]Nenhum perfil foi selecionado para exclusão.[/]");
                    AguardarSaida();
                    return;
                }

                perfisParaApagar = selecao;
            }

            // Executar exclusão com barra de progresso
            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule("[red]Executando Exclusão[/]") { Justification = Justify.Left });
            AnsiConsole.WriteLine();

            var resultados = new List<(string Nome, string Caminho, bool Sucesso, string Msg)>();

            AnsiConsole.Progress()
                .Columns(new ProgressColumn[]
                {
                    new TaskDescriptionColumn(),    // Descrição do perfil sendo processado
                    new PercentageColumn(),         // Porcentagem de conclusão
                    new ElapsedTimeColumn(),        // Tempo decorrido
                    new SpinnerColumn()             // Indicador de atividade (spinner)
                })
                .Start(ctx =>
                {
                    var task = ctx.AddTask("[green]Removendo perfis selecionados...[/]", maxValue: perfisParaApagar.Count);

                    foreach (var profile in perfisParaApagar)
                    {
                        task.Description = $"[white]Removendo {profile.Name}...[/]";
                        try
                        {
                            profile.ManagementObj.Delete();
                            resultados.Add((profile.Name, profile.Path, true, "OK"));
                        }
                        catch (Exception ex)
                        {
                            resultados.Add((profile.Name, profile.Path, false, ex.Message));
                        }
                        task.Increment(1);
                    }
                });

            // Exibir resumo no final
            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule("[yellow]Resumo da Operação[/]") { Justification = Justify.Left });
            AnsiConsole.WriteLine();

            foreach (var res in resultados)
            {
                if (res.Sucesso)
                {
                    AnsiConsole.MarkupLine($"[green]✔[/] Perfil [bold white]{res.Nome}[/] ({res.Caminho}) removido com sucesso.");
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]❌[/] Falha ao remover [bold white]{res.Nome}[/] ({res.Caminho}): [red]{res.Msg}[/]");
                }
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold green]Processo finalizado![/]");
            AguardarSaida();
        }

        private static bool IsAdministrator()
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static void Elevate()
        {
            string? processPath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(processPath)) return;

            var startInfo = new ProcessStartInfo
            {
                FileName = processPath,
                UseShellExecute = true,
                Verb = "runas"
            };

            string[] args = Environment.GetCommandLineArgs();

            // Verifica se está rodando via dotnet host ou exe direto
            if (Path.GetFileNameWithoutExtension(processPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            {
                // Inclui todos os argumentos (incluindo o caminho da dll que é o args[0])
                startInfo.Arguments = string.Join(" ", args.Select(a => $"\"{a}\""));
            }
            else
            {
                // Se for o exe direto, ignora o primeiro elemento que é o próprio caminho do exe
                startInfo.Arguments = string.Join(" ", args.Skip(1).Select(a => $"\"{a}\""));
            }

            try
            {
                Process.Start(startInfo);
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[bold red]Erro ao elevar privilégios:[/] {ex.Message}");
                AguardarSaida();
            }
        }

        private static List<ProfileInfo> ObterPerfis()
        {
            List<ProfileInfo> list = new();
            try
            {
                string query = "SELECT * FROM Win32_UserProfile";
                using ManagementObjectSearcher searcher = new(query);

                foreach (ManagementObject profile in searcher.Get().Cast<ManagementObject>())
                {
                    string sid = profile["SID"]?.ToString() ?? string.Empty;
                    string localPath = profile["LocalPath"]?.ToString() ?? string.Empty;
                    bool isSpecial = (bool)(profile["Special"] ?? false);
                    bool isLoaded = (bool)(profile["Loaded"] ?? false);
                    string name = Path.GetFileName(localPath) ?? string.Empty;

                    if (string.IsNullOrWhiteSpace(localPath))
                        continue;

                    // Ignorar perfis do sistema ou locais protegidos (Administrador, Support) que nunca devem ser removidos
                    if (SidsDoSistema.Contains(sid, StringComparer.OrdinalIgnoreCase) ||
                        isSpecial ||
                        PerfilsLocais.Contains(name, StringComparer.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    bool shouldIgnore = false;
                    string ignoreReason = string.Empty;

                    if (isLoaded)
                    {
                        shouldIgnore = true;
                        ignoreReason = "Em Uso (Carregado)";
                    }

                    list.Add(new ProfileInfo
                    {
                        Path = localPath,
                        Name = string.IsNullOrEmpty(name) ? sid : name,
                        Sid = sid,
                        IsSpecial = isSpecial,
                        IsLoaded = isLoaded,
                        ManagementObj = profile,
                        ShouldIgnore = shouldIgnore,
                        IgnoreReason = ignoreReason
                    });
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Erro ao consultar WMI:[/] {ex.Message}");
            }
            return list;
        }

        private static void ExibirTabelaPerfis(List<ProfileInfo> profiles)
        {
            var table = new Table();
            table.Border(TableBorder.Rounded);
            table.AddColumn("[bold]Usuário[/]");
            table.AddColumn("[bold]Caminho[/]");
            table.AddColumn("[bold]Status / Ação[/]");

            foreach (var p in profiles)
            {
                string statusText;
                if (p.ShouldIgnore)
                {
                    statusText = $"[yellow]Ignorado ({p.IgnoreReason})[/]";
                }
                else
                {
                    statusText = "[green]Qualificado para Exclusão[/]";
                }

                table.AddRow(
                    p.Name,
                    p.Path,
                    statusText
                );
            }

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
        }

        private static void AguardarSaida()
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[grey]Pressione qualquer tecla para sair...[/]");
            Console.ReadKey(true);
        }
    }

    class ProfileInfo
    {
        public string Path { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Sid { get; set; } = string.Empty;
        public bool IsSpecial { get; set; }
        public bool IsLoaded { get; set; }
        public required ManagementObject ManagementObj { get; set; }
        public bool ShouldIgnore { get; set; }
        public string IgnoreReason { get; set; } = string.Empty;
    }
}