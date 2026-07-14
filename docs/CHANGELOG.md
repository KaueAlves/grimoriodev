# Changelog

## [0.2.0] — 2026-07-14
### Adicionado
- **Fase 2: Sistema de Workspace**
- Entidade `Workspace` (sealed, factory method, restore para deserialização)
- Value Object `WorkspaceSettings` (19 configurações, init-only, immutable)
- Struct `CardIndexEntry` (32 bytes, layout de mmap, FNV-1a type hash)
- Interfaces: `IWorkspaceRepository`, `ICardRepository`, `ICardCache`, `IWalService`, `IBloomFilter`, `IMemoryBudgetManager`, `IDeduplicationService`
- `JsonWorkspaceRepository` — persistência JSON com recent workspaces
- `WorkspaceService` — Create, Open, Save, List, Delete workspaces
- `DirtyTrackerService` — rastreamento de alterações com prioridade
- `AutoSaveService` — timer com batch e throttling
- `WorkspaceViewModel` — MVVM para UI de workspace
- `StartScreenWindow` — tela inicial com lista de workspaces recentes
- Converters WPF (BoolToVisibility, ZeroToVisibility)
- Source Generator `WorkspaceJsonContext` (System.Text.Json AOT)
- .gitignore

## [0.1.0] — 2026-07-14
### Adicionado
- Estrutura inicial da solução (Fase 1)
- Projetos: Domain, Application, Infrastructure, Presentation
- Configuração de DI, Logging (Serilog), MVVM (CommunityToolkit)
- Tema escuro inicial
- Documentação: SPEC, ARCHITECTURE, CHANGELOG, ROADMAP, TASKS

### Corrigido
- Removido `CommunityToolkit.Mvvm` redundante de `Application.csproj` (não utilizado nessa camada)
- Removidos pacotes Serilog/Logging redundantes de `Presentation.csproj` (já recebidos via Infrastructure)
- Corrigido conflito de namespace `Application` em `App.xaml.cs`
- Atualizado step 01 com contagem correta de arquivos (19→21)
- Atualizado TASKS.md com pendências reais da Fase 1
