Regras de Execução

Antes de iniciar qualquer implementação:

Leia toda a documentação presente na pasta /docs.
Compreenda completamente a arquitetura antes de escrever código.
Nunca faça suposições quando houver documentação disponível.
Processo de Desenvolvimento

Implemente apenas uma Feature por vez.

Para cada Feature:

Planeje a implementação.
Verifique impactos na arquitetura.
Implemente.
Garanta que o projeto compila sem erros.
Execute todos os testes.
Corrija eventuais problemas.
Atualize a documentação.
Atualize o CHANGELOG.
Atualize o ROADMAP.
Atualize o TASKS.
Faça uma autoavaliação do código buscando simplificações e oportunidades de refatoração.
Somente então inicie a próxima Feature.

Nunca deixe o projeto em estado inconsistente.

Qualidade

Antes de considerar qualquer tarefa concluída, verifique:

O projeto compila.
Não existem warnings relevantes.
Os testes existentes continuam passando.
Não houve regressão.
A documentação continua consistente.
O código segue SOLID e MVVM.
Nenhuma responsabilidade foi colocada na camada incorreta.
Refatoração

Sempre que identificar duplicação de código, alto acoplamento ou violação dos princípios da arquitetura:

Refatore imediatamente antes de continuar.
Atualize a documentação se necessário.
Registre decisões arquiteturais importantes em um ADR.
Evolução

Nunca implemente soluções temporárias ("gambiarras").

Sempre prefira soluções:

Escaláveis
Desacopladas
Testáveis
Reutilizáveis
Preparadas para expansão futura

O objetivo não é apenas entregar funcionalidades, mas construir uma plataforma que possa evoluir por muitos anos sem perda de qualidade arquitetural.