# Plano privado de backup histórico do 34

Nenhum desligamento, exclusão ou captura de dados reais foi executado. O 37 não deve consultar o backup em runtime.

1. Congelar referências Git das fontes/controladores/receitas, dependências e workflows; criar bundle Git e inventário por repositório/revisão. Conservar 34 intacto até aceite 6–7.
2. Categorias privadas separadas: código/receitas; APKs/instaladores/mídia; configurações de cada cliente; bancos/anexos; identidade/assinatura/segredos; infraestrutura e documentação de restauração. Não misturar dados de clientes nem elevar um PET a GLOBAL.
3. Encerrar acesso de escrita ou utilizar backup consistente SQLite, com WAL/SHM tratados pela API de backup; copiar anexos da mesma revisão. Inventariar quantidade, tamanho e SHA-256. Testar integridade SQLite na cópia; não compactar banco aberto como se fosse snapshot consistente.
4. Compactar por categoria e segmentos controlados. A ferramenta candidata `private_archive.py` protege o segmento com AES-256-GCM e inventário SHA-256 autenticado; rejeita links/caminhos inseguros e restaura só em diretório novo. Limite 256 MiB: dividir categorias maiores, não remover o limite silenciosamente. Não é um gerenciador completo de backup.
5. Gerar chave aleatória em cofre privado do proprietário, separada dos arquivos. Nunca incluir chave, senhas, tokens, bancos ou ZIP de assinatura em GitHub/relatórios públicos. Dois destinos privados com controle de acesso e retenção; mídia offline adicional se disponível. Não criar link público.
6. DPAPI é vinculado ao usuário/Windows: conservar estratégia de recuperação do perfil ou reprovisionar credenciais por autenticação segura no destino. Não prometer restaurar tokens DPAPI em outro computador copiando um .dat. Nunca revogar o vínculo original como parte do backup.
7. Restaurar em Windows QA novo e isolado; validar hashes, banco, anexos, assinaturas, licenciamento legítimo, versões GLOBAL/PET e atualizador/rollback. Integrações devem apontar a serviços QA. Testar restauração de arquivos por categoria e de uma instalação completa sem alterar o original.
8. Emitir recibo privado de inventário/criptografia/restauração e verificar acesso ao cofre. Só depois de transbordo/homologação final 6–7 e autorização expressa, planejar desativação do 34. Esta etapa não autoriza desativar nem apagar nada.

Validação desta ferramenta: três testes com arquivos sintéticos, round-trip íntegro, adulteração recusada antes da extração, link recusado e permissões privadas. O backup real do 34 e sua restauração operacional continuam NÃO EXECUTADOS.
