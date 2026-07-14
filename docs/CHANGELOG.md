# Changelog

## [0.2.0] — 2026-07-14
### Adicionado
- **Fase 2: Sistema de Workspace** (completo — CRUD + UI + LSM engine + otimizações)
- Entidade `Workspace`, `WorkspaceSettings`, `CardIndexEntry`
- Interfaces: IWorkspaceRepo, ICardRepo, ICardCache, IWalService, IBloomFilter, IMemoryBudgetManager, IDedupService
- **LSM Storage Engine**: DataFileRepository (data.lz4 async + SemaphoreSlim), MemoryMappedIndexRepository (idx.bin sorted + PrefaultPages), MemoryMappedIndexBloomFilter (SIMD Vector), WalService (group commit + CRC32 + incremental checkpoint), ContentAddressableStore (dedup SHA-256 + SemaphoreSlim), Lz4CompressionService (adaptive L0/L9), CardCacheLru2Q, PooledBuffer, MemoryBudgetManager, Prefetcher
- **I/O**: ConfigureAwait(false) infra-wide (41 awaits), TokenBucket (50MB/s), SemaphoreSlim(max 4) em DataFile + ContentAddressable, true async I/O (WriteAsync/ReadExactlyAsync)
- **Use Cases**: LoadCard, RecoverWorkspace, CompactWal, VacuumData, WorkspaceService, DirtyTrackerService, AutoSaveService
- **Integração**: WorkspaceSessionService (gerencia sessão LSM), navegação StartScreen → MainWindow, WAL replay no open workspace
- **UI**: StartScreenWindow (lista workspaces + criar), MainWindow (info + status bar + menu)
- **ADR**: 4 architecture decision records em `docs/adr/` (data file, SHA-256, group commit, LZ4 adaptive)
- **Testes**: 86 testes (xUnit + Shouldly + NSubstitute), 100% passing
- **Compilação**: 5 projetos, 0 warnings, 0 errors

### Corrigido
- `EntrySize` em MemoryMappedIndexRepository: 24 → 28 bytes (corrompia entries adjacentes)
- MMF flush antes de Dispose/recriação em Upsert
- `WalService`: FileShare.ReadWrite + `_writeLock` em ReplayAsync
- `DataFileRepository.ReadEntryAsync`: lê do stream existente sob lock (sem MMF separada)
- `Read(Span)` → `ReadExactly(Span)` (CA2022)


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
