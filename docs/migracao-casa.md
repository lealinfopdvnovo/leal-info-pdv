# Casa do Leal — preparação de migração protegida

Partida confirmada pelo usuário: PDV 10.443, PET V1.1. Pacote publicado de referência: fonte 3907959a57b7fa032b8a58392f27d4962ab64f54, verificado pelo SHA-256 do canal.
Base comum consolidada: 599e308d7ee7534914ef9a0225096f5440955cc8.
Candidata isolada da transição: 10.444 com PET V1.1 independente. Não publicada.

O atualizador instalado aceita exatamente três arquivos. A primeira transição mantém esse protocolo: apphost, biblioteca comum neutra e dependências. O apphost leva uma cápsula de primeiro uso, com manifesto e biblioteca de perfil. O código comum não contém identidade de cliente ou negócios de vidraçaria.

Na abertura controlada pelo atualizador elevado, a cápsula é validada por hash, identidade, versão e API. Antes de ativar o perfil, é criada uma cópia SQLite online íntegra do banco local e dos arquivos locais de licença/configuração/identidade, com índice de hashes verificado. A primeira instalação do perfil usa preparação durável e ativação por renomeação da pasta na mesma unidade. Perfis existentes não são sobrescritos. Não há alteração de banco, licença ou configurações por essa instalação.

O rollback do atualizador original restaura os três arquivos legados reais. A versão legada usa seu perfil embarcado e ignora o novo diretório independente, inclusive após uma interrupção. Diretórios de preparação incompletos não se tornam perfis ativos. A base comum futura pode ser atualizada sem carregar outra identidade de cliente no executável.

Validações exigidas nesta preparação:
- download somente de leitura da versão realmente publicada, SHA-256 e dependências iguais;
- dados fictícios criados por execução das classes do binário legado verificado, incluindo projeto, recebimento, agenda e contrato;
- carregamento dos mesmos registros na biblioteca comum + perfil separado, sem alteração de qualquer linha;
- origem errada, corrupção, diretório existente, repetição e interrupção antes da ativação;
- aplicação com o worker original, backup dos executáveis reais, recuperação SYSTEM após interrupção e rejeição de hash;
- abertura do apphost real sem licença operacional: ausência de confirmação deve causar recuperação automática, jamais aprovação falsa de saúde;
- rollback executa o bootstrap legado real e confirma seu perfil original, sem consumir o módulo novo.

Limites: esta preparação não publica nem instala no computador do usuário; não altera Loja do Leal, Gelamac, Casa do Genilson ou canal GLOBAL. Não fornece instalador ou procedimentos manuais. O teste sem licença operacional não homologa abertura com a licença real, aprovação visual real ou operação real. O atualizador independente do PET continua uma etapa separada. A disponibilização no atualizador depende da conclusão e revisão destas evidências e da autorização de publicação aplicável.

Para continuar os testes sem publicar no canal GLOBAL, a base neutra aceita um manifesto comum em `clients/<identidade>/global/version.json`. Ele exige Component=GLOBAL, API compatível, identidade exata e pacote comum seguro. O canal antigo de executáveis completos e os manifestos de PET não são consumidos por essa rota. A seleção é genérica e o mesmo binário pode servir outras identidades; somente a Casa do Leal é alvo desta preparação. Nenhum manifesto foi criado ou modificado por estes testes.
