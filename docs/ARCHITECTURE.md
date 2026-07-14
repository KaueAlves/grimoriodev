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
| Projeto | Tipo | Dependências |
|---------|------|-------------|
| Domain | Class Library | — |
| Application | Class Library | Domain |
| Infrastructure | Class Library | Application |
| Presentation | WPF | Application, Infrastructure |
