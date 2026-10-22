# Consolidação GLOBAL e perfis — validação isolada

Base comum: 4d9128cf8a64b487f836af6c12db14d6a7eb7fd3.
Referências operacionais preservadas: LOJA 19cd2697fada9e444bef8f23a920aca468abd819; CASA 3907959a57b7fa032b8a58392f27d4962ab64f54.

## Implementação

- GLOBAL contém autenticação, licença, banco, caixa, cozinha, backup, restauração protegida e atualizador da base comum. Não contém implementações Glass/Angra nem identidade de cliente.
- PetHost oferece seleção de cadastro, temas, atalhos e serviços genéricos; módulos de negócio permanecem nas bibliotecas dos perfis.
- API 1 preservada; IPetStartup e IPetReady são contratos opcionais. Metadados de identidade/versão declarados no módulo devem coincidir com o manifesto e seu SHA-256.
- 001: LOJA LEAL INFO PET V1.2, logo e tema aprovados, barra de atalhos com acesso a todos os controles, recibo da proteção externa exclusivo deste perfil.
- 003: CASA LEAL INFO PET V1.1, apresentação e funções operacionais preservadas.
- 002 GELAMAC e 004 CASA GENILSON: perfis V1.1 de identificação, sem inclusão indevida de vidraçaria. Não representam migração das instalações reais.
- Login cristal comum e restauração protegida reutilizam as implementações aprovadas. O fluxo de recuperação do 001 foi preservado na base comum; seus avisos e regras de licença não foram removidos.

## Evidências produzidas pelo workflow

A execução Windows em `.github/workflows/consolidar_global.yml` deve concluir integralmente com sucesso antes de aceitar o código candidato.

1. Fontes de negócio extraídos iguais aos operacionais aprovados; autenticação, licença, banco, caixa comum e worker do atualizador sem alterações.
2. Fontes de restauração/rede iguais à implementação aprovada no 001; inicialização equivalente com chamadas genéricas para os perfis.
3. Pacotes realmente publicados dos quatro clientes: download somente para leitura, SHA-256 verificado e dependências iguais à compilação comum. SDK 10.0.401 fixado, sem avanço automático para patches do runner.
4. Quatro identidades, versões independentes, biblioteca comum inalterada, rejeição de manifesto cruzado, hash adulterado, caminho inválido e versão incompatível com o módulo.
5. 001/003: projeto, cliente, dimensões, financeiro sem recebimento duplicado, agenda, recibo A4, contrato e hash; reabertura em processo separado.
6. Relatório financeiro real: seis cores preservadas, descrição legível, referências numéricas e vazias na mesma grade, recarga sem alteração financeira, botões completos nas dimensões e escalas verificadas.
7. Login real: marca cristal, controles de acesso e autenticação existente preservados.
8. Repetição da inicialização com banco populado: todas as linhas preservadas; backup SQLite íntegro e estoque verificável na cópia.
9. Restauração: casos direcionados da implementação aprovada, incluindo interrupção de processo, WAL, exclusividade, espaço insuficiente, cancelamento, configurações/licença, reinício e abertura da tela de vendas.
10. Atualização comum: aplicação, interrupção/recuperação SYSTEM e hash inválido para 001/003, preservando bytes dos módulos reais e cópias de bancos populados.

Os artefatos contêm resultados por perfil, screenshots de renderização de teste, documentos A4 de dados fictícios, backups de fixtures, dependências, cenários de restauração/rollback e hashes da compilação. Não contêm dados de cliente.

## Limites de aprovação

- Nenhuma publicação, alteração de feed, instalação ou modificação de banco/licença/configuração de cliente foi executada.
- `CommonCandidateValidated` aprova exclusivamente a compilação e os cenários isolados registrados; não significa equivalência integral de toda instalação real.
- `LegacyMigrationApproved=false` e `PetSpecificUpdaterValidated=false` continuam explícitos nas evidências.
- Atualizador transacional independente do PET, primeira migração protegida, preservação das personalizações reais de 002/004 e homologação Windows das instalações existentes pertencem às etapas seguintes e permanecem pendentes.
- Não marcar o alinhamento completo GLOBAL/PET como 100% a partir apenas desta compilação. A aprovação real exige evidência das instalações e da migração, sem confundir publicação com instalação.
