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
- [x] DataFileRepository (data.lz4 append-only + segments, async SemaphoreSlim)
- [x] MemoryMappedIndexRepository (idx.bin two-level sorted, Guid.CompareTo binary search)
- [x] MemoryMappedIndexBloomFilter (global, FNV-1a + Murmur3, SIMD Vector<byte>)
- [x] ContentAddressableStore (blobs/ SHA-256 dedup, SemaphoreSlim)
- [x] WalService (group commit + CRC32 + compactação + incremental checkpoint)
- [x] Lz4CompressionService (adaptive L0/L9)
- [x] WorkspaceJsonContext (source generator)
- [x] TokenBucket (rate limiter 50MB/s)

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
- [x] AutoSaveService (Channel<SaveRequest>, coalescing, batching, prioridade)

### I/O e Otimizações
- [x] ConfigureAwait(false) infra-wide (41 awaits em 8 arquivos)
- [x] SemaphoreSlim(max 4) em DataFileRepository + ContentAddressableStore
- [x] true async I/O (WriteAsync/ReadExactlyAsync) em DataFileRepository
- [x] SIMD binary search: TryFindSimd (AVX2 + SSE2 + scalar fallback)
- [x] PrefaultPages() no MemoryMappedIndexRepository.Open()
- [x] Incremental checkpoint no WalService (500 entries / 5min)
- [x] Channel<SaveRequest> no AutoSaveService (System.Threading.Channels)
- [x] ArrayPool<byte> em todos os buffers
- [x] TokenBucket 50MB/s para throttling de I/O

### Presentation
- [x] WorkspaceViewModel (MVVM, WAL replay, session lifecycle)
- [x] StartScreenWindow (tela inicial com workspaces recentes)
- [x] MainViewModel (status bar, card count, size, save status)
- [x] MainWindow (menu + status bar + workspace info)
- [x] Converters WPF (BoolToVisibility, ZeroToVisibility)
- [x] DI completa (AddApplication + AddInfrastructure + AddPresentation)
- [x] Navegação StartScreen → MainWindow com WAL recovery

### Testes
- [x] Directory.Build.props (TreatWarningsAsErrors, AnalysisLevel)
- [x] 86 testes unitários (xUnit + Shouldly + NSubstitute)
- [x] WorkspaceServiceTests (10) — CRUD + edge cases
- [x] DataFileRepositoryTests (4) — append/read/segment/dedup pointer
- [x] WalServiceTests (9) — replay/group commit/compactação/CRC/corrupção
- [x] ContentAddressableStoreTests (11) — hash/store/load/dedup/size
- [x] MemoryMappedIndexBloomFilterTests (9) — add/check/FP/persist/rebuild
- [x] CardCacheLru2QTests (10) — set/get/eviction/write buffer/hit rate
- [x] MemoryMappedIndexRepositoryTests (10) — upsert/find/remove/persist/rebuild
- [x] Lz4CompressionServiceTests (5) — roundtrip/empty/large/span
- [x] PooledBufferTests (6) — rent/advance/reset/dispose
- [x] MemoryBudgetManagerTests (8) — allocation/pressure/critical

### Documentação
- [x] ADR-001: data file vs per-card files
- [x] ADR-002: dedup SHA-256 vs xxHash
- [x] ADR-003: group commit vs per-entry WAL
- [x] ADR-004: adaptive LZ4 vs compressão fixa
- [x] Localização padrão: %USERPROFILE%/GrimorioDev

---

## Fase 3 — Canvas Infinito ✅
### Domain
- [x] Card entity (Title, Content, Position, Width, Height, IsPinned)
- [x] CardPosition readonly record struct (X, Y, ZIndex) + Offset()
- [x] CardIndexEntry struct (32 bytes, já existente)

### Application
- [x] IWorkspaceSessionService interface (CurrentWorkspace, IsActive)
- [x] ICardRepository interface (workspace-scoped CRUD: GetAllAsync, GetByIdAsync, SaveAsync, DeleteAsync, SaveBatchAsync)
- [x] CardDto + CreateCardRequest + MoveCardRequest DTOs
- [x] CreateCard use case (valida título, posição, tamanho, persistência LSM)
- [x] LoadCanvasCards use case (carrega todos do workspace ativo)
- [x] MoveCard use case (move + salva via LSM)

### Infrastructure
- [x] CardRepository LSM (DataFileRepository + MemoryMappedIndexRepository + WalService + Bloom + Cache + Dedup)
- [x] MemoryMappedIndexRepository.EnumerateAllEntries() — varredura completa do índice
- [x] DI: CardRepository registrado como singleton com acesso ao WorkspaceSessionService

### Presentation — InfiniteCanvas Control
- [x] Custom Canvas com DrawingVisual (zoom/pan/grid)
- [x] Zoom: Ctrl+ScrollWheel, 10%–1000%, centrado no mouse
- [x] Pan: botão direito arrastar
- [x] Grid adaptativo (linhas, espaçamento escala com zoom, mínimo 5px)
- [x] Viewport culling: só desenha cards na viewport
- [x] DrawingVisualPool (reuso de objetos DrawingVisual)
- [x] Z-Order: cards ordenados por ZIndex
- [x] Hit test: scan linear, conversão screen→canvas coordinates
- [x] Drag: seleção via clique, ghost na posição original, nova posição ao soltar
- [x] Duplo-clique em espaço vazio: cria card na posição
- [x] LOD 5 níveis: Mini (<30%) → Compact (30-50%) → Normal (50-150%) → Detailed (150-500%) → MaxDetail (>500%)
- [x] CardRenderData sealed record (DTO de renderização)
- [x] CardMovedEventArgs event args
- [x] CanvasViewModel (ObservableCollection<CardRenderData>, load/move/create commands)
- [x] CanvasPage + navegação via MainWindow Frame

### Testes — 32 novos, 117 total
- [x] CardTests (13) — Create, UpdateTitle, UpdateContent, MoveTo, Resize, TogglePin, Restore, validações
- [x] CardPositionTests (5) — Offset, Equality, Deconstruct, Default
- [x] LoadCanvasCardsTests (4) — load, empty workspace, empty cards, full mapping
- [x] MoveCardTests (4) — move, no workspace, not found, timestamp update
- [x] CreateCardTests (6) — create, custom size, default content, no workspace, empty title validation
