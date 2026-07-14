# Tasks — Fase 1

## Concluídas ✅
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
- [x] Remover NuGet redundantes (CommunityToolkit.Mvvm do Application, Serilog/Sinks do Presentation)
- [x] Corrigir conflito de namespace Application em App.xaml.cs
- [x] Atualizar documentação de steps

## Pendências 🔴
- [ ] Criar `.gitignore` (bin/, obj/, etc.)
- [ ] Criar `Directory.Build.props` (configurações compartilhadas)
- [ ] Criar `GlobalUsings.cs`
- [ ] Criar `appsettings.json` (configuração do Serilog)
- [ ] Extrair `AddPresentation()` para `Presentation/DependencyInjection.cs`
- [ ] Criar ADRs (decisões arquiteturais)
- [ ] Criar testes unitários (EntityBase)
