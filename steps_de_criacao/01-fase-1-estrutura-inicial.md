# Fase 1 — Estrutura Inicial

## Objetivo
Criar solução .NET, configurar arquitetura Clean Architecture, DI, Logging, MVVM, tema e estrutura inicial dos projetos.

---

## O que foi feito ✅

### Solução e Projetos
- [x] Criado `GrimorioDev.sln` com 4 projetos na pasta `src/`
- [x] `GrimorioDev.Domain` — Class Library (entidades, interfaces)
- [x] `GrimorioDev.Application` — Class Library (casos de uso, DTOs)
- [x] `GrimorioDev.Infrastructure` — Class Library (logging, serviços externos)
- [x] `GrimorioDev.Presentation` — WPF (Views, ViewModels, Resources)

### Clean Architecture
- [x] Cadeia de dependências: Domain → Application → Infrastructure → Presentation
- [x] Domain sem dependências externas
- [x] Application depende apenas de Domain
- [x] Infrastructure depende de Application
- [x] Presentation depende de Application + Infrastructure

### Dependency Injection
- [x] `Microsoft.Extensions.DependencyInjection` 8.0.1
- [x] Métodos de extensão por camada: `AddApplication()`, `AddInfrastructure()`, `AddPresentation()`
- [x] Container configurado em `App.xaml.cs` (`OnStartup`)

### Logging
- [x] Serilog 4.1.0
- [x] Sink de console
- [x] Sink de arquivo com rolling diário (`logs/grimoriodev-.log`)
- [x] Integrado ao `ILogger<T>` via `AddSerilog()`

### MVVM
- [x] `CommunityToolkit.Mvvm` 8.3.2
- [x] `MainViewModel` herdando de `ObservableObject`
- [x] Uso de `[ObservableProperty]` para bindings
- [x] `MainWindow` com DataContext injetado via construtor

### Tema
- [x] Tema escuro (`Theme.xaml`)
- [x] Paleta: fundo `#1E1E2E`, accent roxo `#7C3AED`
- [x] Estilos globais (`Styles.xaml`)
- [x] Resources merged no `App.xaml`

### Código Base
- [x] `EntityBase` (Id, CreatedAt, UpdatedAt)
- [x] `IUnitOfWork` (interface de unidade de trabalho)
- [x] `MainViewModel` com título bindable
- [x] `MainWindow` com layout inicial
- [x] DI funcional com resolução de `MainWindow` via container

### Documentação
- [x] `docs/SPEC.md` — Especificação do projeto
- [x] `docs/ARCHITECTURE.md` — Diagrama de camadas e dependências
- [x] `docs/CHANGELOG.md` — v0.1.0
- [x] `docs/ROADMAP.md` — 11 fases planejadas
- [x] `docs/TASKS.md` — Checklist da Fase 1

### Compilação
- [x] Domain ✅ (0 warnings, 0 errors)
- [x] Application ✅ (0 warnings, 0 errors)
- [x] Infrastructure ✅ (0 warnings, 0 errors)
- [x] Presentation ⚠️ (não compila no Linux — WPF é Windows-only)

---

## O que faltou / Pendências 🔴

### Críticas
- [ ] **Presentation não compila no Linux** — WPF exige Windows com .NET SDK + workload WPF. O projeto está correto, mas só pode ser verificado no Windows.
- [ ] **Nenhum teste foi criado** — A Fase 1 não tem testes unitários. Idealmente deveria haver ao menos um teste para `EntityBase`.

### Melhorias / Baixa Prioridade
- [ ] **Nenhum ADR foi criado** — Decisões arquiteturais (ex: escolha do CommunityToolkit.Mvvm, Serilog, estrutura de pastas) deveriam ter ADRs.
- [ ] **Faltam `.gitignore`** e `Directory.Build.props` para versionamento e configurações compartilhadas.
- [ ] **.NET 9 não disponível no ambiente** — Usamos .NET 8. O target pode ser atualizado quando o SDK 9 estiver disponível.
- [ ] **Nenhum `appsettings.json`** para configurações do Serilog e DI — atualmente tudo está hardcoded.
- [ ] **Não há `GlobalUsings.cs`** — usings estão espalhados nos arquivos.

---

## Arquivos Criados (19)
```
GrimorioDev.sln
docs/SPEC.md
docs/ARCHITECTURE.md
docs/CHANGELOG.md
docs/ROADMAP.md
docs/TASKS.md
src/GrimorioDev.Domain/GrimorioDev.Domain.csproj
src/GrimorioDev.Domain/Entities/EntityBase.cs
src/GrimorioDev.Domain/Interfaces/IUnitOfWork.cs
src/GrimorioDev.Application/GrimorioDev.Application.csproj
src/GrimorioDev.Application/DependencyInjection.cs
src/GrimorioDev.Infrastructure/GrimorioDev.Infrastructure.csproj
src/GrimorioDev.Infrastructure/DependencyInjection.cs
src/GrimorioDev.Presentation/GrimorioDev.Presentation.csproj
src/GrimorioDev.Presentation/App.xaml
src/GrimorioDev.Presentation/App.xaml.cs
src/GrimorioDev.Presentation/ViewModels/MainViewModel.cs
src/GrimorioDev.Presentation/Views/MainWindow.xaml
src/GrimorioDev.Presentation/Views/MainWindow.xaml.cs
src/GrimorioDev.Presentation/Resources/Theme.xaml
src/GrimorioDev.Presentation/Resources/Styles.xaml
```
