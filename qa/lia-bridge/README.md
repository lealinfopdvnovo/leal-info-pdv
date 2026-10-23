# Ponte LIA exclusiva 001 — candidata não publicada

Base: pacote 001 10.453 SHA-256 2b18cfdd5437ce9e0b1f901e22f655f1c6640ac15198bb92d3317dbc3d3877dd.
Correção da janela: 5cd6696a0776279b413b00a7262d8b7774b3af9c.

## Restrição
O manifesto atual contém versão, URL, SHA-256 e identidade, sem uma lista de componentes.
O aplicador instalado valida exatamente três entradas do ZIP: LealInfoPDV.deps.json, LealInfoPDV.dll e LealInfoPDV.exe.
A LIA é iniciada pelo PDV por LIC-AI/LicAi.exe; não é substituída pelo pacote de três arquivos.

## Sequenciamento proposto
- Feed atual clients/001/version.json oferecerá somente a ponte candidata 10.454 (três arquivos).
- Apenas o código >=10.454 no 001 consulta clients/001/lia/version.json para a entrega candidata 10.455.
- O PDV e o notificador validam a mesma identidade e URLs de release client-001.
- Sem publicar nenhum feed neste trabalho. Não reaproveitar a versão 10.455 diretamente no feed antigo.
- As versões são candidatas locais aos artefatos de teste, sem modificar GLOBAL ou PET 1.2.

## Proteções
Lista completa de arquivos da LIA e hashes fixados no aplicador embutido; nenhuma permissão genérica de diretório.
Preserva os três arquivos do núcleo; aceita o formato original e o formato estendido completo somente no 001.
Backup durável por arquivo, inclusive registro de inexistência anterior; recuperação remove arquivos novos.
Executável principal restaurado por último; checagem de processos e pontos de junção.
Tarefa de recuperação SYSTEM persiste antes da substituição. Mesmo journal para núcleo e LIA.
A guarda de inicialização e a confirmação de saúde existentes permanecem.
Chaves, banco, configuração, licença e PET nunca entram no pacote.

## Evidências esperadas
Compilação Windows da ponte e entrega; dependências do núcleo comparadas ao pacote instalado.
Elegibilidade dos feeds via HttpMessageHandler controlado e testes nativos do notificador.
Aplicador extraído do DLL publicado 10.453 aplica a ponte de três arquivos.
Novo aplicador extraído do DLL da ponte aplica os arquivos reais compilados da LIA.
DLL entregue carregada do caminho LIC-AI; janela mascarada salva/cancela/fecha; DPAPI.
Rejeição de SHA do ZIP e de arquivo LIA adulterado mesmo com SHA do ZIP recalculado.
Interrupções no núcleo, LIA, arquivo novo e recuperação; tarefa SYSTEM recupera todos os hashes.
Preservação de arquivos sentinela de dados, financeiro, licença, configurações e PET.

## Limites
Testes isolados não equivalem a instalação ou homologação na loja.
O probe carrega a DLL entregue e sua janela; não aciona a sessão real autenticada do operador.
API, microfone, licença do cliente e chave real não são usados.
A publicação futura exige os resultados aprovados e autorização específica.
