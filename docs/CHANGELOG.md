# Changelog

## [0.1.0] — 2026-07-14
### Adicionado
- Estrutura inicial da solução (Fase 1)
- Projetos: Domain, Application, Infrastructure, Presentation
- Configuração de DI, Logging (Serilog), MVVM (CommunityToolkit)
- Tema escuro inicial
- Documentação: SPEC, ARCHITECTURE, CHANGELOG, ROADMAP, TASKS

### Corrigido
- Removido `CommunityToolkit.Mvvm` redundante de `Application.csproj` (não utilizado nessa camada)
- Removidos pacotes Serilog/Logging redundantes de `Presentation.csproj` (já recebidos via Infrastructure)
- Corrigido conflito de namespace `Application` em `App.xaml.cs`
- Atualizado step 01 com contagem correta de arquivos (19→21)
- Atualizado TASKS.md com pendências reais da Fase 1
