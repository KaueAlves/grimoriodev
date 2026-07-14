# Arquitetura — GrimórioDev

## Clean Architecture

```
┌─────────────────────────────────────┐
│         Presentation (WPF)          │
│   Views / ViewModels / Resources    │
├─────────────────────────────────────┤
│         Application (Use Cases)     │
│   Services / DTOs / Interfaces      │
├─────────────────────────────────────┤
│         Infrastructure              │
│   Persistence / Logging / Ext. APIs │
├─────────────────────────────────────┤
│         Domain (Core)               │
│   Entities / Value Objects / Enums  │
└─────────────────────────────────────┘
```

## Dependency Injection
- Container: Microsoft.Extensions.DependencyInjection
- Configuração no startup do Presentation
- Todos os serviços registrados via interfaces

## MVVM
- Framework: CommunityToolkit.Mvvm
- ViewModels herdam de ObservableObject
- Views vinculadas via DataContext

## Logging
- Serilog com sinks: arquivo + console

## Projetos
| Projeto | Tipo | Dependências | Pacotes Principais |
|---------|------|-------------|-------------------|
| Domain | Class Library | — | — |
| Application | Class Library | Domain | Logging.Abstractions |
| Infrastructure | Class Library | Application | Serilog, DI.Abstractions |
| Presentation | WPF | Application, Infrastructure | CommunityToolkit.Mvvm, DI |
