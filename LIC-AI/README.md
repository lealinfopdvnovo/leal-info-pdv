# LIC AI

Projeto independente de conversacao, separado funcionalmente do PDV.

## Objetivo
- Conversacao livre e natural.
- Palavra de chamada: `LIC`.
- Memoria persistente de conversas.
- Consolidacao gradual de contexto para melhorar continuidade.
- Nenhum modulo para abrir telas, clicar, alterar cadastros ou executar comandos no PDV.

## Estrutura inicial
- `Core/ConversationEngine.cs`: orquestracao da conversa.
- `Memory/ConversationMemory.cs`: historico persistente.
- `Memory/MemoryConsolidator.cs`: memoria resumida de longo prazo.
- `Models/ChatMessage.cs`: modelo de mensagens.
- `Prompts/LicIdentity.cs`: identidade e comportamento da LIC AI.

## Regra central
A LIC AI conversa. Ela nao controla o PDV.
