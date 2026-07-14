# Roadmap

## Fase 1 ✅ — Estrutura Inicial
- [x] Solução .NET / Clean Architecture
- [x] Projetos Domain, Application, Infrastructure, Presentation
- [x] DI, Logging, MVVM configurados
- [x] Tema escuro
- [x] Documentação base

## Fase 2 ✅ — Sistema de Workspace
- [x] Entidades domínio (Workspace, WorkspaceSettings, CardIndexEntry)
- [x] Interfaces (IWorkspaceRepo, ICardRepo, ICardCache, IWalService, IBloomFilter, IMemoryBudgetManager, IDedupService)
- [x] LSM Storage: DataFileRepository, MemoryMappedIndexRepository, MemoryMappedIndexBloomFilter, WalService (group commit + CRC32 + incremental checkpoint), ContentAddressableStore, Lz4CompressionService, CardCacheLru2Q, PooledBuffer, MemoryBudgetManager, Prefetcher, TokenBucket
- [x] I/O: ConfigureAwait(false) infra-wide, SemaphoreSlim(max 4), true async I/O, PrefaultPages, SIMD binary search (AVX2+SSE2+scalar)
- [x] Persistência: JsonWorkspaceRepository + WorkspaceJsonContext (source generator)
- [x] Use Cases: WorkspaceService (CRUD), LoadCard, RecoverWorkspace, CompactWal, VacuumData, DirtyTrackerService, AutoSaveService
- [x] UI: StartScreenWindow (lista + criar), MainWindow (info + status bar + menu + WAL recovery)
- [x] DI integrada (Application + Infrastructure + Presentation)
- [x] ADR docs (4 decisions in docs/adr/)
- [x] Testes: 86 testes, 100% passing
- [x] App executa sem crash (StartScreenWindow abre)

## Fase 3 — Canvas Infinito
## Fase 4 — Sistema de Cards
## Fase 5 — Sistema de Conexões
## Fase 6 — Sistema de Componentes
## Fase 7 — Terminal
## Fase 8 — Sistema de IA
## Fase 9 — Plugins
## Fase 10 — Performance
## Fase 11 — Polimento
