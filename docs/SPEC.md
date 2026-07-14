# GrimórioDev — Especificação

## Visão Geral
Aplicativo desktop para Windows com canvas infinito (similar ao Miro) voltado para desenvolvimento de software e Inteligência Artificial.

## Stack
- C# / .NET 8 (migração planejada para .NET 9)
- WPF / MVVM / Clean Architecture
- Dependency Injection (Microsoft.Extensions.DependencyInjection)
- Logging (Serilog)

## Fases
1. Estrutura Inicial ✅ (atual)
2. Workspace
3. Canvas Infinito
4. Sistema de Cards
5. Sistema de Conexões (Graph)
6. Sistema de Componentes
7. Terminal (ConPTY)
8. Sistema de IA
9. Plugins
10. Performance
11. Polimento

## Arquitetura
Clean Architecture com 4 camadas:
- **Domain**: Entidades, Value Objects, Interfaces de repositório
- **Application**: Casos de uso, DTOs, interfaces de serviço
- **Infrastructure**: Persistência, serviços externos, logging
- **Presentation**: WPF (Views, ViewModels, Resources)
