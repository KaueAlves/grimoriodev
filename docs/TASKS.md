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
- [x] Entidade Workspace (sealed, factory, restore)
- [x] Value Object WorkspaceSettings (19 configurações, immutable)
- [x] Struct CardIndexEntry (32 bytes, mmap layout)
- [x] Interfaces: IWorkspaceRepo, ICardRepo, ICardCache, IWalService, IBloomFilter, IMemoryBudgetManager, IDedupService
- [x] JsonWorkspaceRepository (persistência + recent workspaces)
- [x] WorkspaceService (Create, Open, Save, List, Delete)
- [x] DirtyTrackerService (rastreamento de alterações)
- [x] AutoSaveService (timer + batch + throttling)
- [x] WorkspaceViewModel (MVVM)
- [x] StartScreenWindow (tela inicial com workspaces recentes)
- [x] Converters WPF (BoolToVisibility, ZeroToVisibility)
- [x] Source Generator WorkspaceJsonContext
- [x] DI registrada (Application + Infrastructure)
- [x] Compilação: 0 warnings, 0 errors

## Pendências 🔴
- [ ] Criar `Directory.Build.props` (configurações compartilhadas)
- [ ] Criar `GlobalUsings.cs`
- [ ] Criar testes unitários
- [ ] Implementar WAL (Write-Ahead Log)
- [ ] Implementar Bloom Filter (global + por segmento)
- [ ] Implementar Cache LRU-2Q
- [ ] Implementar LZ4 compression
- [ ] Implementar Content-Addressable Store (dedup)
- [ ] Implementar Memory-Mapped Index (idx.bin)
- [ ] Implementar Vacuum (streaming + atomic rename)
