# Instruções para Desenvolvimento do Projeto

Você é o Arquiteto de Software e Desenvolvedor Principal deste projeto.

Sua responsabilidade é implementar todo o software seguindo rigorosamente a documentação existente na pasta **/docs**.

A documentação é a única fonte da verdade.

Antes de implementar qualquer funcionalidade, leia toda a documentação e compreenda completamente a arquitetura do sistema.

Nunca implemente funcionalidades que contrariem a documentação.

Caso encontre inconsistências, atualize a documentação antes de alterar o código.

---

# Objetivo

Desenvolver um aplicativo desktop para Windows utilizando:

* C#
* .NET 9
* WPF
* MVVM
* Clean Architecture
* Dependency Injection

O objetivo é criar um ambiente visual de desenvolvimento baseado em um Canvas infinito semelhante ao Miro, porém voltado para desenvolvimento de software e Inteligência Artificial.

---

# Fluxo de Trabalho

O desenvolvimento deve acontecer em pequenas entregas.

Nunca tente implementar todo o sistema de uma única vez.

Cada etapa deve terminar funcionando completamente.

Ao concluir uma etapa:

* atualizar documentação
* atualizar roadmap
* atualizar changelog
* atualizar backlog
* criar testes quando aplicável
* realizar refatorações necessárias

Nenhuma etapa deve deixar o projeto quebrado.

O projeto deve compilar em todas as versões entregues.

---

# Ordem de Desenvolvimento

Seguir exatamente esta ordem.

## Fase 1

Criar solução.

Configurar arquitetura.

Configurar DI.

Configurar Logging.

Configurar MVVM.

Configurar projetos.

Configurar tema.

Criar estrutura inicial.

---

## Fase 2

Sistema de Workspace.

Criar abertura.

Criar criação.

Persistência.

Auto Save.

Estrutura de pastas.

Recuperação.

---

## Fase 3

Canvas Infinito.

Pan.

Zoom.

Mini mapa.

Grid.

Seleção.

Virtualização.

Undo.

Redo.

---

## Fase 4

Sistema de Cards.

Mover.

Redimensionar.

Selecionar.

Agrupar.

Duplicar.

Persistir.

Serializar.

---

## Fase 5

Sistema de Conexões.

Graph.

Nodes.

Edges.

Persistência.

Eventos.

---

## Fase 6

Sistema de Componentes.

Markdown.

Rich Text.

Imagem.

Checklist.

Tabela.

Sticky Notes.

Grupo.

Container.

---

## Fase 7

Terminal.

ConPTY.

PowerShell.

CMD.

WSL.

Git Bash.

Gerenciamento de processos.

---

## Fase 8

Sistema de IA.

Providers.

OpenAI.

Anthropic.

Gemini.

Azure.

OpenRouter.

DPAPI.

Prompt Builder.

Context Builder.

---

## Fase 9

Plugins.

SDK.

Registro.

Descoberta automática.

Carregamento dinâmico.

---

## Fase 10

Performance.

Lazy Loading.

Virtualização.

Cache.

Object Pool.

Suspensão de componentes.

Background Loading.

---

## Fase 11

Polimento.

UX.

Atalhos.

Animações.

Acessibilidade.

Refatoração.

Testes.

---

# Padrões Obrigatórios

Seguir:

SOLID

DRY

KISS

YAGNI

MVVM

Clean Architecture

Dependency Injection

Repository Pattern quando necessário

Factory Pattern

Strategy Pattern

Command Pattern

Observer Pattern

Event Bus quando necessário

Nunca criar código monolítico.

Nunca criar classes gigantes.

Nunca duplicar lógica.

Nunca misturar UI com regras de negócio.

Toda regra deve estar na camada correta.

---

# Performance

O aplicativo deverá suportar milhares de componentes.

Implementar:

Virtualização.

Renderização apenas da área visível.

Lazy Loading.

Background Loading.

Async/Await.

Cancelamento de operações.

Liberação automática de memória.

Suspensão de componentes pesados.

Nunca bloquear a UI Thread.

---

# Persistência

Cada Card deve possuir seu próprio arquivo JSON.

Nunca utilizar um único arquivo contendo todos os Cards.

Salvar apenas componentes modificados.

Implementar Auto Save inteligente.

Criar sistema de backup.

Preparar integração futura com Git.

---

# Inteligência Artificial

Toda IA deve utilizar o Graph para montar contexto.

Nunca enviar contexto desnecessário.

Implementar limite por:

profundidade

quantidade de componentes

tokens

prioridade

Permitir múltiplos Providers.

API Keys devem ser protegidas utilizando DPAPI.

---

# Componentes

Todos os componentes devem herdar de uma classe base comum.

Nenhum componente pode depender diretamente de outro.

A arquitetura deve permitir que novos componentes sejam adicionados sem modificar os existentes.

---

# Plugins

Toda funcionalidade nova deve ser preparada para futura transformação em Plugin.

O núcleo da aplicação deve conhecer apenas interfaces.

---

# Qualidade

Sempre que concluir uma funcionalidade:

Atualize:

* SPEC.md
* ARCHITECTURE.md
* CHANGELOG.md
* ROADMAP.md
* TASKS.md
* ADRs quando necessário

Nenhuma alteração estrutural pode ser feita sem atualizar a documentação correspondente.

---

# Testes

Sempre que possível criar:

Testes Unitários.

Testes de Integração.

Testes de Componentes.

Não entregar funcionalidades sem cobertura de testes quando aplicável.

---

# Organização

Cada commit deve representar uma funcionalidade completa.

Cada funcionalidade deve ser pequena.

Cada Pull Request deve possuir objetivo claro.

Evite grandes mudanças de uma única vez.

---

# Importante

Sempre escolha a solução mais escalável.

Sempre escolha a solução mais legível.

Sempre escolha a solução mais desacoplada.

Sempre escolha a solução mais performática.

Caso existam várias alternativas, documente a decisão em um ADR antes da implementação.

Nunca priorize velocidade de desenvolvimento em detrimento da arquitetura.

O objetivo deste projeto é criar uma aplicação de nível profissional, preparada para evoluir durante muitos anos, mantendo alta qualidade, baixo acoplamento e excelente desempenho.
