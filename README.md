# ProfileCleaner 🧹

Uma aplicação console em C# projetada para limpar perfis de usuário do Windows não utilizados de forma segura e eficiente. Utiliza WMI para consultar os perfis do sistema, filtra automaticamente perfis essenciais/do sistema, permite excluir sessões ativas e oferece modos de limpeza manual e automático.

Estilizado com uma interface de console moderna e responsiva utilizando **Spectre.Console**.

---

## ✨ Recursos

- **🛡️ Auto-Elevação**: Solicita automaticamente privilégios de Administrador na inicialização se ainda não estiver sendo executado como tal.
- **🔍 Filtragem Inteligente**: Detecta e ignora automaticamente:
  - SIDs do sistema (`S-1-5-18`, `S-1-5-19`, `S-1-5-20`).
  - Perfis especiais e do sistema.
  - Sessões ativas/carregadas (evitando a exclusão de perfis atualmente em uso).
  - Perfis de administrador corporativos ou personalizados pré-definidos (ex: `Administrador`, `Support`).
- **💡 Proteção do Operador**: Solicita interativamente que o operador selecione perfis para proteger/ignorar explicitamente antes de apresentar a lista de exclusão.
- **⚡ Modos de Execução**:
  - **Manual**: Escolha exatamente quais perfis qualificados excluir usando uma lista de seleção múltipla.
  - **Automático**: Limpe todos os perfis qualificados de uma só vez de forma segura após dupla confirmação.
- **📊 Feedback Visual**: Tabelas bonitas, banners de aviso estilizados e barras de progresso em tempo real para a exclusão de perfis.

---

## 🛠️ Pré-requisitos

- **SO**: Windows (requer suporte a WMI e APIs do Windows).
- **Runtime/SDK**: [.NET 10.0 SDK](https://dotnet.microsoft.com/download) ou superior.

---

## 🚀 Como Compilar e Executar

1. Clone ou baixe o repositório.
2. Abra um terminal no diretório do projeto.
3. Execute o seguinte comando para compilar o binário em modo Release:
   ```bash
   dotnet build -c Release
   ```
4. Execute o executável como Administrador (ou deixe que ele se eleve sozinho):
   ```bash
   dotnet run
   ```

---

## 📦 Dependências

- [Spectre.Console](https://github.com/spectreconsole/spectre.console) - Para elementos ricos de interface de console (tabelas, prompts, barras de progresso).
- `System.Management` - Para consultar `Win32_UserProfile` via WMI.
- `System.DirectoryServices.AccountManagement` - Para validação de usuários locais/domínio.

---

## ⚠️ Avisos de Segurança

- Perfis carregados ou em uso no momento não podem ser excluídos e são ignorados automaticamente para evitar corrupção de dados.
- Contas do sistema são excluídas obrigatoriamente através da verificação de SID.
- Sempre revise a lista de perfis selecionados com atenção antes de confirmar a exclusão.

---

## 📄 Licença

Este projeto está licenciado sob os termos da licença GNU General Public License v3.0 (GPL-3.0). Consulte o arquivo [LICENSE](LICENSE) para obter mais detalhes.
