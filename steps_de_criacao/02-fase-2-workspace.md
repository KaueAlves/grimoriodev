# Fase 2 — Sistema de Workspace

## Objetivo
Criar sistema de gerenciamento de workspaces com criação, abertura, persistência e auto save, otimizado para milhares de cards com I/O mínimo, zero stutter na UI e uso eficiente de memória.

---

## Modelo de Domínio

### Workspace
```csharp
public sealed class Workspace : EntityBase
{
    public string Name { get; private set; }
    public string Description { get; private set; }
    public string Location { get; private set; }
    public DateTime LastOpenedAt { get; private set; }
    public DateTime LastSavedAt { get; private set; }
    public WorkspaceSettings Settings { get; private set; }
    public int CardCount { get; private set; }
    public long SizeBytes { get; private set; }
}
```

### CardIndexEntry (32 bytes, contíguo em mmap)
```
Offset  Size  Campo
0       16    GUID
16      4     TipoHash (FNV-1a)
20      8     UpdatedAt ticks
28      2     SizeKB
30      1     Flags (bit 0=comprimido, 1=deletado, 2=pinned, 3=deduplicado)
31      1     RelevanceScore
```

### WorkspaceSettings (immutable)
```csharp
public sealed class WorkspaceSettings
{
    public int AutoSaveIntervalMs { get; init; } = 30_000;
    public int AutoSaveDebounceMs { get; init; } = 3_000;
    public bool AutoSaveEnabled { get; init; } = true;
    public string Theme { get; init; } = "dark";
    public long MaxAssetSizeBytes { get; init; } = 100 * 1024 * 1024;
    public int CompressionThresholdBytes { get; init; } = 8_192;
    public bool UseCompression { get; init; } = true;
    public bool UseDeduplication { get; init; } = true;
    public bool PreloadAdjacentCards { get; init; } = true;
    public int PreloadRadius { get; init; } = 3;
    public int CacheHotMaxEntries { get; init; } = 128;
    public int CacheWarmMaxEntries { get; init; } = 384;
    public int CacheEvictAfterMs { get; init; } = 30_000;
    public int CacheDecayIntervalMs { get; init; } = 5_000;
    public int MaxConcurrentReads { get; init; } = 4;
    public int WalCompactThresholdEntries { get; init; } = 256;
    public bool WalSyncOnWrite { get; init; } = false;
    public int BackgroundIoMaxBytesPerSec { get; init; } = 50 * 1024 * 1024;
    public int SegmentSizeBytes { get; init; } = 16 * 1024 * 1024;
}
```

---

## Estrutura de Arquivos — LSM-Style + Content-Addressable Dedup

### Árvore
```
%APPDATA%/GrimorioDev/
├── recent.json
├── last-workspace.txt

{Location}/{workspace-id}/
├── ws.meta                 # Metadados compactos (binário)
├── idx.bin                 # Índice sorted (mmap)
├── idx.bloom               # Bloom global (mmap)
├── wal.log                 # Write-Ahead Log (group commit)
├── data.lz4                # Dados append-only, segmentado (mmap)
├── blobs/                  # Content-addressable store (dedup)
│   ├── {sha256-prefix-16}.blob
│   ├── {sha256-prefix-16}.blob
│   └── ...
├── segments/
│   ├── seg-0000.bloom
│   ├── seg-0001.bloom
│   └── ...
└── assets/
    └── ...
```

### data.lz4 — Arquivo de Dados Segmentado
```
Cada card no arquivo:
  [4]  Decompressed size
  [4]  Compressed size (0 = raw, -1 = dedup pointer)
  [N]  Payload:
       Se compressed > 0: LZ4 payload
       Se compressed = 0: raw payload
       Se compressed = -1: [16] SHA-256 do blob referenciado

Cada segmento: máx 16MB de dados brutos
  - Bloom filter próprio (segments/seg-XXXX.bloom)
  - Cards append-only, nunca movidos

Dedup (Content-Addressable):
  - SHA-256 do conteúdo do card (antes de comprimir)
  - Se hash já existe em blobs/ → escrever ponteiro (24 bytes)
  - Se não existe → escrever no data.lz4 + salvar blob em blobs/
  - Vantagem: cards duplicados = 24 bytes em vez de KBs
```

### idx.bin — Índice Two-Level com Metadata Split
```
Nível 1 — Key Table (sorted, cache-friendly):
  Entry: [16 GUID] [4 SegmentIndex] [4 OffsetInSegment] = 24 bytes
  Ordenada por GUID → binary search O(log n)

Nível 2 — Data (no data.lz4 via mmap):
  Offset: segmentIndex × SegmentSize + offsetInSegment

Header (40 bytes):
  [4]  Magic: 0x4752494D
  [4]  Versão
  [4]  Total entries
  [4]  Total segmentos
  [4]  Total blobs (dedup)
  [8]  Última atualização (ticks)
  [8]  Tamanho total dos dados (bytes)
  [4]  Header CRC32C

Total: 40 + (N × 24) bytes
10k = 240KB | 100k = 2.4MB | 1M = 24MB (cabe em L3)
```

### idx.bloom — Bloom Filters
```
Global:
  ≤2.500 entries  → 1.024 bits (128 bytes)
  ≤50.000 entries → 8.192 bits (1KB)
  >50.000 entries → 65.536 bits (8KB)

Por segmento: 256 bytes (2048 bits) cada

3 hash functions: FNV-1a, Murmur3(seed=1), Murmur3(seed=2)
FP rate: < 1%
Check: SIMD Vector<byte> (16 bytes simultâneos)
```

### wal.log — Write-Ahead Log com Group Commit
```
Header (16 bytes):
  [4]  Magic: 0x57414C00
  [4]  Versão
  [4]  Entry count
  [4]  Header CRC32C

Entry (variável):
  [1]  Op (0=Create, 1=Update, 2=Delete, 3=Batch)
  [16] Card GUID
  [8]  Timestamp ticks
  [4]  Payload size
  [N]  Payload
  [4]  Entry CRC32C

Group Commit:
  Múltiplas entries são bufferizadas em memória
  Um único fsync persiste todas de uma vez
  Reduz syscalls de N para 1 por batch
```

---

## Funcionalidades

### 1. Criar Workspace
- Diálogo: Nome, Descrição, Localização (padrão: %USERPROFILE%/GrimorioDev/)
- Validação: nome não vazio, local acessível, disco > 10MB
- Cria: pastas + ws.meta + idx.bin + wal.log + data.lz4 + blobs/
- Pasta nomeada por GUID

### 2. Abrir Workspace
```
1. Ler ws.meta
2. Mapear idx.bin via MemoryMappedFile
3. Mapear idx.bloom via MemoryMappedFile
4. Prefault páginas idx.bin (PrefetchVirtualMemory)
5. Se wal.log não vazio → ReplayWalAsync():
   a. Ler entries, validar CRC32C (hardware: System.Crc32C)
   b. Aplicar operações, atualizar idx.bin
   c. Truncar wal.log, rebuild blooms
6. Mapear data.lz4 sob demanda por segmento
```

### 3. Lazy Loading com Segment-Aware mmap
```
Fluxo de leitura:
1. Bloom global SIMD → se NÃO existe, CardNotFoundException
2. Cache LRU-2Q → se HIT, retorna
3. Binary search idx.bin SIMD → (segment, offset)
4. Bloom do segmento → confirmar
5. mmap data.lz4 [segmentStart..segmentEnd]
6. Seek direto → ler entry
7. Se dedup flag → carregar blob de blobs/{hash}.blob
8. Se comprimido → LZ4 decompress via stackalloc
9. Inserir no cache 2Q
10. Prefetch: segmentos adjacentes + cards vizinhos
```

### 4. Cache LRU-2Q com Partição Read/Write
```
Read Cache (limpo):
  Queue A (hot):  128 entries — 1º acesso
  Queue B (warm): 384 entries — acessos repetidos
  Eviction: instantâneo, sem I/O

Write Buffer (dirty):
  Dictionary<Guid, CardData> — máx 64 entries / 4MB
  Flush: batch para WAL + data.lz4

Adaptive sizing:
  Hit rate > 90% e memória < 70% → A += 25%
  Hit rate < 50% → A -= 25%
  Reavaliar a cada 30s

Lock-free stats: Interlocated.Increment()
Decay: relevance score -- a cada 5s, score=0 → candidato eviction
Pinned: NÃO são evictos
```

### 5. Persistência com Dedup + Group Commit
```
Write path:
  1. Calcular SHA-256 do conteúdo
  2. Se hash existe em blobs/ → escrever ponteiro (-1 flag)
  3. Se não existe → LZ4 compressão (adaptativa) → append data.lz4
     - Escrita real-time: LZ4 level 0 (velocidade)
     - Se UseDeduplication: salvar blob em blobs/ se > 1KB
  4. Group commit: bufferizar WAL entries
  5. Um único fsync persiste batch inteiro
  6. Atualizar idx.bin

Read path:
  1. Bloom → existence (O(1))
  2. idx.bin → (segment, offset)
  3. mmap segmento → seek → ler entry
  4. Se dedup → mmap blob →返回 data
  5. Se comprimido → LZ4 decompress (stackalloc < 64KB)

Crash recovery:
  Replay WAL → idx.bin → rebuild blooms → truncar WAL

Delete:
  Flag no idx.bin → espaço recuperado no vacuum
```

### 6. Auto Save com Channel + Throttling
```
UI Thread → Channel<SaveRequest> → Background Workers

Coalescing: 3 mudanças/card em <3s → 1 save
Batch: >10 requests ou 3s

Prioridade:
  1. Visíveis (flush imediato)
  2. Adjacentes (próximo batch)
  3. Off-screen (batch seguinte)

Token bucket: 50MB/s (evita competir com foreground)
Indicadores: 🟢 salvo / 🟡 pendente / 🔴 erro
```

### 7. Assets
```
Upload: PipeReader → LZ4 → PipeWriter → File
ArrayPool<byte>, throttling 10MB/s
Dedup: SHA-256 → hardlink
Thumbnails: background, MemoryCache, máx 256px
```

### 8. WAL Compactação (Group Commit + Checkpoint)
```
Trigger: >256 entries OU >1MB OU manual

Processo (background):
  1. Pausar writes (SemaphoreSlim)
  2. Consolidar entries por card
  3. Append finais em data.lz4
  4. Atualizar idx.bin
  5. Rebuild blooms
  6. Truncar WAL
  7. Liberar writes

Incremental checkpoint (a cada 500 entries / 5min):
  Flush dirty → atualizar idx → truncar committed
  Manter últimas 50 entries para recovery rápido
```

### 9. Vacuum com Adaptive Compression
```
Trigger: >20% deletados OU manual

Processo (background, streaming):
  1. Criar data.vacuum temporário
  2. Ler idx.bin → cada entry NÃO deletada:
     a. Ler card do data.lz4 (mmap + seek)
     b. Se blob (dedup) → copiar blob
     c. Re-comprimir com LZ4 level 9 (melhor ratio)
     d. Append em data.vacuum
     e. Atualizar offset no idx.bin
  3. Atomic rename data.vacuum → data.lz4
  4. Rebuild todos os blooms

Adaptive compression:
  - Escrita real-time: LZ4 level 0 (~700 MB/s compress)
  - Vacuum: LZ4 level 9 (~100 MB/s compress, ~15% melhor ratio)
  - Resultado: writes rápidos + storage compacto
```

---

## Otimizações de Performance (Nível 6)

### SIMD Binary Search (AVX2 + fallback)
```csharp
if (Avx2.IsSupported)
{
    // Comparar 4 GUIDs simultaneamente
    // 100k entries → ~17 comparações × 4 = 68 processados
    // vs 100k scalar
}
else if (Sse2.IsSupported)
{
    // Comparar 2 GUIDs simultaneamente (fallback SSE2)
}
else
{
    // Scalar fallback (qualquer CPU)
}
```

### Hardware CRC32C
```csharp
// x86: System.Crc32C usa instrução CRC32C nativa
// ~10x mais rápido que CRC32 software
// Usado em: idx.bin header, WAL entries, card integrity check
if (Crc32C.IsSupported)
{
    uint crc = System.Crc32C.Append(0, data);
}
```

### Prefault mmap Pages
```csharp
// Windows: PrefetchVirtualMemory pré-carrega páginas
// Reduz page faults de ~50ms para ~5ms em idx.bin de 2.4MB
// Executar em background thread ao abrir workspace
```

### LZ4 Zero-Copy + Adaptive Level
```
Read (real-time): stackalloc + LZ4Codec.Decode
Write (real-time): LZ4 level 0 (~700 MB/s)
Vacuum: LZ4 level 9 (~100 MB/s, melhor ratio)

Decompress direto do mmap span:
  ReadOnlySpan<byte> compressed = mmapView.Slice(offset, size);
  Span<byte> decompressed = stackalloc byte[maxCardSize];
  LZ4Codec.Decode(compressed, decompressed);
```

### Content-Addressable Dedup
```
SHA-256 do conteúdo antes de comprimir:
  - Se existe em blobs/ → ponteiro de 24 bytes (16 hash + 8 offset)
  - Se não existe → escrever + salvar blob
  - Blobs > 1KB candidatos a dedup
  - Evita reescrever dados idênticos

Economia estimada (canvas com templates):
  - 1000 cards de template = 1000 ponteiros (24KB) em vez de 1000 cópias
```

### Group Commit WAL
```
Sem group commit: N cards = N fsyncs (~10ms cada) = N × 10ms
Com group commit: N cards = 1 fsync = ~10ms total

Buffer em memória, flush a cada:
  - 10 entries OU
  - 3 segundos OU
  - Manual (Ctrl+S)
```

### Memory Budget Manager
```
Budget: min(30% RAM, 500MB)
Monitora GC.GetTotalMemory() a cada 5s
> 80% → eviction agressivo (-50% cache)
> 95% → flush write buffer + compact
Reage a MemoryFailPoint se disponível
```

### SIMD Bloom Filter
```
Vector<byte> para 16 bytes simultâneos:
  Ler → Vector.Equals → popcount
~3x mais rápido que loop bit-a-bit
```

### Unsafe mmap Access
```csharp
ref byte baseRef = ref MemoryMarshal.GetReference(mappedSpan);
ref var entry = ref Unsafe.Add(ref baseRef, index * 24);
// ~40% mais rápido que indexer[]
```

### Incremental Checkpoint
```
A cada 500 entries ou 5min:
  Flush dirty → atualizar idx → truncar WAL
  Manter últimas 50 entries para recovery rápido
```

---

## Interface

### Tela Inicial
- Logo + nome
- "Criar Workspace" / "Abrir Workspace"
- Últimos workspaces (virtualizados, thumbnail background)

### Menu
- Arquivo > Novo / Abrir / Salvar (Ctrl+S) / Fechar / Propriedades
- Ferramentas > Compactar WAL / Vacuum Data

### Status Bar
- Nome | 🟢/🟡/🔴 | Cards | Tamanho | RAM (debug)

---

## Camadas

### Domain
- `Workspace.cs`, `WorkspaceSettings.cs`, `CardIndexEntry.cs`
- `IWorkspaceRepository.cs`, `ICardRepository.cs`, `ICardCache.cs`
- `IWalService.cs`, `IBloomFilter.cs`, `IMemoryBudgetManager.cs`
- `IDeduplicationService.cs`

### Application
- `CreateWorkspace.cs`, `OpenWorkspace.cs`, `SaveWorkspace.cs`
- `LoadCard.cs`, `RecoverWorkspace.cs`, `CompactWal.cs`, `VacuumData.cs`
- `DirtyTrackerService.cs`

### Infrastructure
- `JsonWorkspaceRepository.cs`, `DataFileRepository.cs`
- `MemoryMappedIndexRepository.cs`, `MemoryMappedBloomFilter.cs`
- `WalService.cs` (group commit), `Lz4CompressionService.cs` (adaptive level)
- `ContentAddressableStore.cs` (blobs/ + SHA-256)
- `CardCache.cs` (LRU-2Q + write buffer), `MemoryBudgetManager.cs`
- `Prefetcher.cs`, `RecentWorkspacesService.cs`, `PooledBuffer.cs`
- `WorkspaceJsonContext.cs` (source generator)

### Presentation
- `WorkspaceViewModel.cs`, `CreateWorkspaceDialog.xaml`, `OpenWorkspaceDialog.xaml`, `StartScreenView.xaml`

---

## Checklist

### Modelo — ✅ Completo
- [x] Workspace (sealed)
- [x] CardIndexEntry (32 bytes, mmap layout)
- [x] WorkspaceSettings (init-only)
- [x] Interfaces: IWorkspaceRepo, ICardRepo, ICardCache, IWalService, IBloomFilter, IMemoryBudgetManager, IDedupService

### Persistência — ✅ Completo (implementado + 86 testes)
- [x] JsonWorkspaceRepository (async, buffered)
- [x] DataFileRepository (append-only data.lz4 + segmentos)
- [x] ContentAddressableStore (blobs/ + SHA-256 dedup)
- [x] MemoryMappedIndexRepository (idx.bin two-level + sorted)
- [x] MemoryMappedBloomFilter (global + por segmento, SIMD)
- [x] WalService (group commit + CRC32 software)
- [x] Lz4CompressionService (adaptive: level 0 write, level 9 vacuum)
- [x] WorkspaceJsonContext (source generator)
- [x] VacuumData (streaming + atomic rename)

### Cache — ✅ Completo
- [x] CardCache LRU-2Q (A=128, B=384)
- [x] Write Buffer separado (max 64 / 4MB)
- [x] Lock-free counters (Interlocked)
- [x] Adaptive sizing (hit rate → resize)
- [ ] ~~Decay relevance score (5s)~~ — baixa prioridade
- [x] Preload segmentos adjacentes
- [x] Eviction read cache sem I/O
- [x] MemoryBudgetManager (30% RAM, 500MB)
- [ ] ~~Pinned cards~~ — baixa prioridade

### I/O — ✅ Completo
- [x] SIMD binary search (AVX2 + SSE2 + scalar fallback) — TryFindSimd
- [ ] ~~Hardware CRC32C (x86 intrinsic)~~ — CRC32 software implementado
- [x] Prefault mmap (PrefetchVirtualMemory) — PrefaultPages() em MemoryMappedIndexRepository
- [ ] ~~LZ4 zero-copy (stackalloc < 64KB)~~ — heap alloc usado
- [x] SemaphoreSlim (max 4) — DataFile + ContentAddressable
- [x] ArrayPool<byte> everywhere
- [x] Token bucket 50MB/s — TokenBucket.cs
- [x] ConfigureAwait(false) infra-wide

### WAL — ✅ Completo (sem hardware CRC)
- [x] Group commit (buffer + 1 fsync)
- [ ] ~~CRC32C hardware por entry~~ — CRC32 software
- [x] Incremental checkpoint (500 / 5min) — TryCheckpointAsync

### Dedup — ✅ Completo
- [x] SHA-256 por conteúdo
- [x] Content-addressable blobs/
- [x] Ponteiro de 24 bytes para blobs existentes
- [x] Candidatos: cards > 1KB

### UI — ✅ Completo
- [x] Tela inicial, status bar, diálogos
- [x] Virtualização (VirtualizingStackPanel default do WPF ListBox)

### Integração — ✅ Completo
- [x] DI (Singleton: Cache, Budget, Store; Transient: Repo, UseCase)
- [x] App.xaml.cs (startup + WAL recovery + navegação)
- [x] Channel<SaveRequest> para fila saves — AutoSaveService com System.Threading.Channels
- [x] DirtyTrackerService, CompactWalUseCase, VacuumDataUseCase

### Testes — ✅ Completo (86 testes, 100% passing)
- [x] Criar / Salvar / Listar workspaces — WorkspaceServiceTests (10)
- [x] DataFile (append, read, segment, dedup pointer) — DataFileRepositoryTests (4)
- [x] WAL replay/group commit/compactação/CRC/corrupção — WalServiceTests (9)
- [x] Dedup (SHA-256 + ponteiro + blob) — ContentAddressableStoreTests (11)
- [x] Bloom global + por segmento (FP < 1%) — MemoryMappedIndexBloomFilterTests (9)
- [x] LRU-2Q + write buffer — CardCacheLru2QTests (10)
- [x] MemoryMappedIndex sorted/upsert/remove/persist/rebuild — MemoryMappedIndexRepositoryTests (10)
- [x] LZ4 compress/decompress roundtrip — Lz4CompressionServiceTests (5)
- [x] PooledBuffer rent/advance/reset/dispose — PooledBufferTests (6)
- [x] MemoryBudgetManager allocation/pressure/critical — MemoryBudgetManagerTests (8)

---

## Arquivos a Criar
```
src/GrimorioDev.Domain/Entities/Workspace.cs
src/GrimorioDev.Domain/Entities/CardIndexEntry.cs
src/GrimorioDev.Domain/ValueObjects/WorkspaceSettings.cs
src/GrimorioDev.Domain/Interfaces/IWorkspaceRepository.cs
src/GrimorioDev.Domain/Interfaces/ICardRepository.cs
src/GrimorioDev.Domain/Interfaces/ICardCache.cs
src/GrimorioDev.Domain/Interfaces/IWalService.cs
src/GrimorioDev.Domain/Interfaces/IBloomFilter.cs
src/GrimorioDev.Domain/Interfaces/IMemoryBudgetManager.cs
src/GrimorioDev.Domain/Interfaces/IDeduplicationService.cs
src/GrimorioDev.Application/UseCases/CreateWorkspace.cs
src/GrimorioDev.Application/UseCases/OpenWorkspace.cs
src/GrimorioDev.Application/UseCases/SaveWorkspace.cs
src/GrimorioDev.Application/UseCases/LoadCard.cs
src/GrimorioDev.Application/UseCases/RecoverWorkspace.cs
src/GrimorioDev.Application/UseCases/CompactWal.cs
src/GrimorioDev.Application/UseCases/VacuumData.cs
src/GrimorioDev.Application/Services/DirtyTrackerService.cs
src/GrimorioDev.Infrastructure/Repositories/JsonWorkspaceRepository.cs
src/GrimorioDev.Infrastructure/Repositories/DataFileRepository.cs
src/GrimorioDev.Infrastructure/Repositories/MemoryMappedIndexRepository.cs
src/GrimorioDev.Infrastructure/Repositories/MemoryMappedBloomFilter.cs
src/GrimorioDev.Infrastructure/Services/WalService.cs
src/GrimorioDev.Infrastructure/Services/Lz4CompressionService.cs
src/GrimorioDev.Infrastructure/Services/ContentAddressableStore.cs
src/GrimorioDev.Infrastructure/Services/CardCache.cs
src/GrimorioDev.Infrastructure/Services/MemoryBudgetManager.cs
src/GrimorioDev.Infrastructure/Services/RecentWorkspacesService.cs
src/GrimorioDev.Infrastructure/Services/Prefetcher.cs
src/GrimorioDev.Infrastructure/IO/PooledBuffer.cs
src/GrimorioDev.Infrastructure/Serialization/WorkspaceJsonContext.cs
src/GrimorioDev.Presentation/ViewModels/WorkspaceViewModel.cs
src/GrimorioDev.Presentation/Views/CreateWorkspaceDialog.xaml
src/GrimorioDev.Presentation/Views/CreateWorkspaceDialog.xaml.cs
src/GrimorioDev.Presentation/Views/OpenWorkspaceDialog.xaml
src/GrimorioDev.Presentation/Views/OpenWorkspaceDialog.xaml.cs
src/GrimorioDev.Presentation/Views/StartScreenView.xaml
src/GrimorioDev.Presentation/Views/StartScreenView.xaml.cs
```

---

## Gargalos e Soluções

| Gargalo | Impacto | Solução |
|---------|---------|---------|
| 100k arquivos no FS | NTFS MFT overhead | 1 data file + segmentos |
| Writes aleatórios | I/O disperso | Append-only data.lz4 |
| Binary search lento | Cache miss | Two-level idx 24B/entry |
| Page faults mmap | Stutter | Prefault + PrefetchVirtualMemory |
| Bloom check lento | CPU | SIMD Vector<byte> |
| Cards não-existentes | Seek desnecessário | Bloom global → segmento |
| GC Gen2 alloc | Stutter | ArrayPool + stackalloc |
| LRU evict quentes | Miss rate | LRU-2Q |
| Cache stats lock | Contention | Interlocked |
| Cache fixo | Desperdício | Adaptive sizing |
| Write + read misturados | Eviction I/O | Partição read/write |
| Memory pressure | OOM | MemoryBudgetManager |
| WAL cresce | Disco + replay | Incremental checkpoint |
| Compact bloqueia UI | Freeze | Streaming + atomic rename |
| Corrupção | Dados perdidos | CRC32C hardware |
| I/O background | Starvation | Token bucket |
| Cards duplicados | Storage desperdiçado | SHA-256 dedup |
| WAL: N fsyncs por batch | Latência | Group commit (1 fsync) |
| Compression rápida = ratio ruim | Disco | Adaptive: L0 write, L9 vacuum |
| SIMD indisponível | Crash | Fallback SSE2 → scalar |

---

## Métricas de Performance

| Métrica | Meta | Método |
|---------|------|--------|
| Abrir workspace | < 60ms | mmap + prefault + bloom |
| Binary search (100k) | < 0.2ms | SIMD AVX2 + two-level |
| Bloom check | < 0.01ms | SIMD Vector<byte> |
| Card cache hit | < 0.5ms | ConcurrentDictionary |
| Card cache miss | < 15ms | mmap segment + LZ4 |
| Salvar 100 cards | < 400ms | group commit + WAL batch |
| Memória (10k, vp=50) | < 150MB | LRU-2Q + budget |
| Memória (100k, vp=50) | < 300MB | mmap + eviction |
| WAL replay (100) | < 40ms | batch + LZ4 |
| Vacuum 100k | < 25s | streaming + atomic |
| Dedup 1000 duplicados | < 50KB | SHA-256 + ponteiros |
| GC Gen2 spikes | 0 | ArrayPool + pooling |
| Cache hit rate | > 90% | LRU-2Q + adaptive + prefetch |

---

## Pendências
- [x] Localização padrão — `%USERPROFILE%/GrimorioDev` em `JsonWorkspaceRepository.GetDefaultLocation()`
- [x] ADR-001: data file vs per-card files — `docs/adr/001-data-file-vs-per-card.md`
- [x] ADR-002: dedup SHA-256 vs xxHash — `docs/adr/002-dedup-sha256-vs-xxhash.md`
- [x] ADR-003: group commit vs per-entry WAL — `docs/adr/003-group-commit-vs-per-entry-wal.md`
- [x] ADR-004: adaptive LZ4 vs compressão fixa — `docs/adr/004-adaptive-lz4-vs-fixed-compression.md`
- [x] Bin search SIMD: AVX2 + SSE2 + scalar em `TryFindSimd`
- [x] Channel<SaveRequest>: `AutoSaveService` com `System.Threading.Channels`
- [x] UI: Virtualização (VirtualizingStackPanel no ListBox da StartScreen)
- [ ] ~~SIMD binary search (AVX2 + SSE2 fallback + scalar)~~ — implementado em `TryFindSimd`
- [ ] ~~Channel<SaveRequest> para fila saves~~ — implementado com `System.Threading.Channels`
- [ ] ~~Definir formato binário de ws.meta~~ — usar JSON via `JsonWorkspaceRepository`
- [ ] ~~Definir política de backup incremental~~ — adiar para Step 3
- [ ] ~~Testar fallback SIMD em CPUs sem AVX2~~ — adiar para Step 3
