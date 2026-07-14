# Changelog

## [0.2.0] — 2026-07-14
### Adicionado
- **Fase 2: Sistema de Workspace** (parte 1 — CRUD + UI + LSM engine)
- Entidade `Workspace`, `WorkspaceSettings`, `CardIndexEntry`
- Interfaces: IWorkspaceRepo, ICardRepo, ICardCache, IWalService, IBloomFilter, IMemoryBudgetManager, IDedupService
- **LSM Storage Engine**: DataFileRepository (data.lz4), MemoryMappedIndexRepository (idx.bin), MemoryMappedIndexBloomFilter, WalService (group commit), ContentAddressableStore (dedup SHA-256), Lz4CompressionService, CardCacheLru2Q, PooledBuffer, MemoryBudgetManager, Prefetcher
- **Use Cases**: LoadCard, RecoverWorkspace, CompactWal, VacuumData, WorkspaceService, DirtyTrackerService, AutoSaveService
- **Integração**: WorkspaceSessionService (gerencia sessão LSM), navegação StartScreen → MainWindow, WAL replay no open workspace
- **UI**: StartScreenWindow (lista workspaces + criar), MainWindow (info + status bar + menu)
- Converters WPF, Source Generator WorkspaceJsonContext, .gitignore
- NuGet: K4os.Compression.LZ4 1.3.8
- **Compilação**: 0 warnings, 0 errors

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
