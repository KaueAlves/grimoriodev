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
- [x] Presentation ✅ (0 warnings, 0 errors — corrigido conflito de namespace `Application`)

---

## Correções realizadas nesta revisão 🔧

### NuGet redundantes removidos
- [x] Removido `CommunityToolkit.Mvvm` do `Application.csproj` (não era usado nessa camada)
- [x] Removido `Serilog`, `Serilog.Extensions.Logging`, `Serilog.Sinks.Console`, `Serilog.Sinks.File`, `Microsoft.Extensions.Logging` do `Presentation.csproj` (já recebidos via Infrastructure)
- [x] `Presentation.csproj` mantém apenas: `CommunityToolkit.Mvvm` e `Microsoft.Extensions.DependencyInjection`

### Correção de compilação
- [x] Corrigido conflito de namespace `GrimorioDev.Application` vs tipo `System.Windows.Application` em `App.xaml.cs` (usa fully-qualified name)

### Contagem de arquivos corrigida
- Step anterior listava 19 arquivos — realidade são **21 arquivos** (csproj de cada projeto + Styles.xaml não estavam na lista original)

---

## O que faltou / Pendências 🔴

### Críticas
- [ ] **Nenhum teste foi criado** — A Fase 1 não tem testes unitários. Idealmente deveria haver ao menos um teste para `EntityBase`.

### Melhorias / Baixa Prioridade
- [ ] **Nenhum ADR foi criado** — Decisões arquiteturais (escolha do CommunityToolkit.Mvvm, Serilog, estrutura de pastas) deveriam ter ADRs.
- [ ] **Falta `.gitignore`** — Diretórios `bin/` e `obj/` estão sendo versionados.
- [ ] **Falta `Directory.Build.props`** — Configurações compartilhadas entre projetos (TargetFramework, Nullable, ImplicitUsings).
- [ ] **Falta `GlobalUsings.cs`** — Usings comuns estão repetidos nos arquivos.
- [ ] **Falta `appsettings.json`** — Configurações do Serilog e DI estão hardcoded no `DependencyInjection.cs`.
- [ ] **`AddPresentation()` definida inline no `App.xaml.cs`** — Diferente de `AddApplication()` e `AddInfrastructure()` que possuem arquivos próprios. Considerar extrair para `Presentation/DependencyInjection.cs`.

---

## Arquivos Criados (21)
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
