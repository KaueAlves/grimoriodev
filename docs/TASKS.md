# Tasks

## Fase 1 — Estrutura Inicial ✅
- [x] Criar solução .NET
- [x] Criar projetos Domain, Application, Infrastructure, Presentation
- [x] Configurar referências entre projetos (Clean Architecture)
- [x] Instalar pacotes NuGet (DI, Logging, MVVM)
- [x] Configurar DI no startup
- [x] Configurar Serilog
- [x] Configurar CommunityToolkit.Mvvm
- [x] Criar tema escuro (styles/colors)
- [x] Criar MainWindow + MainViewModel
- [x] Documentar (SPEC, ARCHITECTURE, CHANGELOG, ROADMAP, TASKS)
- [x] Verificar compilação (todos os 4 projetos — 0 warnings, 0 errors)
- [x] Criar `.gitignore`

## Fase 2 — Sistema de Workspace ✅
### Domínio
- [x] Entidade Workspace (sealed, factory, restore)
- [x] Value Object WorkspaceSettings (19 configurações, immutable)
- [x] Struct CardIndexEntry (32 bytes, mmap layout)
- [x] Interfaces: IWorkspaceRepo, ICardRepo, ICardCache, IWalService, IBloomFilter, IMemoryBudgetManager, IDedupService

### Persistência
- [x] JsonWorkspaceRepository (JSON + recent workspaces)
- [x] DataFileRepository (data.lz4 append-only + segments)
- [x] MemoryMappedIndexRepository (idx.bin two-level sorted, binary search)
- [x] MemoryMappedIndexBloomFilter (global, FNV-1a + Murmur3)
- [x] ContentAddressableStore (blobs/ SHA-256 dedup)
- [x] WalService (group commit + CRC32 + compaction)
- [x] Lz4CompressionService (adaptive L0/L9)
- [x] WorkspaceJsonContext (source generator)

### Cache
- [x] CardCacheLru2Q (hot/warm queues, adaptive sizing, write buffer)
- [x] MemoryBudgetManager (30% RAM, 500MB budget)
- [x] PooledBuffer (ArrayPool<byte> wrapper)
- [x] Prefetcher (segmentos adjacentes)

### Use Cases
- [x] LoadCard (bloom → cache → index → mmap → decompress)
- [x] RecoverWorkspace (WAL replay → rebuild index → truncate)
- [x] CompactWal (consolidate + truncate)
- [x] VacuumData (rewrite + rebuild index + bloom)
- [x] WorkspaceService (Create, Open, Save, List, Delete)
- [x] DirtyTrackerService (rastreamento de alterações)
- [x] AutoSaveService (timer + batch + throttling)

### Presentation
- [x] WorkspaceViewModel (MVVM)
- [x] StartScreenWindow (tela inicial com workspaces recentes)
- [x] Converters WPF (BoolToVisibility, ZeroToVisibility)

### Integração
- [x] DI registrada (Application + Infrastructure)
- [x] Compilação: 0 warnings, 0 errors

## Pendências 🔴
- [ ] Criar `Directory.Build.props` (configurações compartilhadas)
- [ ] Criar `GlobalUsings.cs`
- [ ] Criar testes unitários
- [ ] Integrar workspace services no fluxo de UI (abrir → mmap → bloom → cache)
- [ ] Format ws.meta binário (atualmente usa JSON)
- [ ] Prefault mmap pages (PrefetchVirtualMemory)
