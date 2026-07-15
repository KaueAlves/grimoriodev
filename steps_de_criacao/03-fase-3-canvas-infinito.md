# Fase 3 — Canvas Infinito

## Objetivo
Criar o canvas infinito (scroll + zoom + pan) como superfície principal do editor, com renderização eficiente, grid, integração com o workspace e suporte para milhares de cards sem perda de performance.

---

## Implementação Final

### Abordagem (vs planejado)
- **Renderização**: `DrawingVisual` (não `VirtualizingPanel` + `ScrollViewer`)
  - Cards renderizados como `DrawingVisual` filhos diretos do `InfiniteCanvas`
  - Viewport culling manual: apenas cards visíveis na viewport são desenhados
  - `DrawingVisualPool` para reuso de objetos visuais (evita alocação por frame)
  - Z-Order via `OrderBy(c.ZIndex)` durante renderização
- **Zoom**: `ScaleTransform` + `TranslateTransform` no `RenderTransform` do `Canvas`
  - Zoom centrado no mouse (recalcula pan para manter ponto sob cursor)
  - Range: 10%–1000%
- **Pan**: Arrastar com botão direito do mouse
- **Grid**: Linhas adaptativas desenhadas via `DrawingVisual`
  - Espaçamento escala com zoom (mínimo 5px)
- **Hit test**: Scan linear sobre cards cacheados (efetivo para centenas de cards)
  - Conversão screen→canvas coordinates: `(screenX - panX) / zoomLevel`
- **Drag**: Card selecionado via hit test, arrastado atualizando `CardRenderData`
  - Ghost translúcido na posição original durante drag
  - Ao soltar, persiste via `MoveCard` use case + LSM

### LOD (5 níveis de detalhe)

| Zoom | Nível | Renderização |
|------|-------|-------------|
| < 30% | Mini | Apenas retângulo colorido |
| 30%–50% | Compact | Título + header, sem conteúdo |
| 50%–150% | Normal | Título + header + até 3 linhas de conteúdo |
| 150%–500% | Detailed | Título + header + até 8 linhas de conteúdo |
| > 500% | MaxDetail | Conteúdo completo sem truncamento |

---

## Estrutura de Arquivos (real)

```
src/GrimorioDev.Domain/
  Entities/
    Card.cs              — Entidade (Title, Content, Position, Width, Height, IsPinned)
    CardPosition.cs      — readonly record struct (X, Y, ZIndex)

src/GrimorioDev.Application/
  Interfaces/
    IWorkspaceSessionService.cs
  UseCases/
    ICardRepository.cs       — Interface (workspace-scoped CRUD)
    CreateCard.cs            — Use case de criação
    LoadCanvasCards.cs       — Carrega todos os cards do workspace
    MoveCard.cs              — Move card e persiste
  DTOs/
    CardDto.cs               — FromDomain() mapper
    CreateCardRequest.cs     — Request DTO
    MoveCardRequest.cs       — Request DTO

src/GrimorioDev.Infrastructure/
  Repositories/
    CardRepository.cs        — LSM-based (DataFile + Index + WAL + Bloom + Cache + Dedup)
  MemoryMappedIndexRepository.cs  — Add: EnumerateAllEntries()

src/GrimorioDev.Presentation/
  Controls/
    InfiniteCanvas.cs        — Custom control (DrawingVisual: zoom/pan/grid/cards/LOD/drag)
    InfiniteCanvas.xaml      — Default style
    CardRenderData.cs        — sealed record (DTO de renderização)
    CardMovedEventArgs.cs    — Event args
    DrawingVisualPool.cs     — file-scoped pool
  ViewModels/
    CanvasViewModel.cs       — MVVM: cards, seleção, zoom, criação
  Views/
    CanvasPage.xaml          — Host InfiniteCanvas
    CanvasPage.xaml.cs       — Wire events → ViewModel
    MainWindow.xaml          — Frame para navegação
    MainWindow.xaml.cs       — NavigateToCanvas()
  Themes/
    Generic.xaml             — Merged dictionary
```

---

## Checklist

### Canvas — 13/13
- [x] InfiniteCanvas custom control (WPF) — `DrawingVisual` + viewport culling
- [x] Scroll/pan infinito (RenderTransform em vez de ScrollViewer)
- [x] Zoom (Ctrl+ScrollWheel, 10%–1000%, centrado no mouse)
- [x] Pan (arrastar botão direito)
- [x] Grid de fundo adaptativo (linhas, espaçamento escala com zoom)
- [x] Virtualização (viewport culling: só renderiza cards visíveis)
- [x] Camadas de renderização (Z-order via OrderBy + selection overlay + ghost drag)
- [x] DrawingVisual pool (reuso por frame)
- [x] Hit test (scan linear sobre cards — suficiente para centenas)
- [x] Zoom adaptativo (LOD: 5 níveis Mini→Compact→Normal→Detailed→MaxDetail)
- [x] Integração com WorkspaceSessionService
- [x] LoadCanvasCards use case
- [x] CanvasViewModel (MVVM: load, select, drag, create, zoom)

### Card Domain — 3/3
- [x] Entidade Card (Title, Content, Position, Width, Height, IsPinned)
- [x] CardPosition readonly record struct (X, Y, ZIndex) + Offset()
- [x] CardDto com FromDomain()

### Persistência — 2/2
- [x] CardRepository (LSM via DataFile + Index + WAL + Bloom + Cache + Dedup)
- [x] MoveCard use case

### UI — 2/2
- [x] CanvasPage (abriga InfiniteCanvas + wire events)
- [x] Navegação: MainWindow → CanvasPage (Frame.Navigate)

### Testes — ~/?
- [x] CardTests (13) — Create, Update, Move, Resize, TogglePin, Restore
- [x] CardPositionTests (5) — Offset, Equality, Deconstruct
- [x] CreateCardTests (6) — criação, validação, edge cases
- [x] LoadCanvasCardsTests (4) — load, empty, mapeamento
- [x] MoveCardTests (4) — move, not found, timestamps
- [ ] Testes de integração CardRepository LSM (requer workspace temp) — baixa prioridade
